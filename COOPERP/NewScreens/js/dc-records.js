/* Student Disciplinary module: Disciplinary Records. The case list, filters, batch hearing and the new-case form. ES5. */
(function () {
    'use strict';
    var B = window.DC_BOOT || {}, R = B.rights || {}, PAGE = 'DisciplinaryRecords.aspx', esc = FA.esc, qs = FA.qs;
    var state = { page: 1, size: 25 }, selected = {}, lastRows = [];

    // ── Filters ───────────────────────────────────────────────────────
    FA.fill('fStatus', [{ id: 'open', name: 'All open cases' }, { id: 'awaiting_hearing', name: 'Awaiting hearing' }, { id: 'awaiting_decision', name: 'Awaiting decision' }]
        .concat(DC.pairs(DC.STATUSES)), { all: 'All statuses' });
    FA.fill('fType', B.types, { all: 'All types' });
    FA.fill('fSev', DC.pairs(DC.SEVERITIES), { all: 'All severities' });
    FA.fill('fYear', (B.years || []).map(function (y) { return { id: y, name: y }; }), { all: 'All years' });
    FA.fill('fCampus', B.campuses, { all: 'All campuses' });
    FA.fill('fFac', B.faculties, { all: 'All faculties' });
    FA.fill('fFlag', [{ id: 'overdue', name: 'No update for a while' }, { id: 'hearing_passed', name: 'Hearing date passed' }, { id: 'appeal_closing', name: 'Appeal window closing' },
        { id: 'follow_marks', name: 'Marks action owed' }, { id: 'follow_fees', name: 'Fees action owed' }, { id: 'sanction', name: 'Sanction in force' },
        { id: 'unread', name: 'Notice not read' }, { id: 'email_failed', name: 'Email not delivered' }]
        .concat(R.seeRestricted ? [{ id: 'restricted', name: 'Restricted cases' }] : []), { all: 'Any' });
    FA.fill('fEffect', DC.pairs(DC.EFFECTS.slice(0, 5)), { all: 'Any' });
    function depts() {
        var f = qs('fFac').value;
        FA.fill('fDept', (B.departments || []).filter(function (d) { return !f || d.faculty === f; }), { all: 'All departments' });
        progs();
    }
    function progs() {
        var f = qs('fFac').value, d = qs('fDept').value;
        FA.fill('fProg', (B.programmes || []).filter(function (p) { return (!f || p.faculty === f) && (!d || String(p.dept) === d); }), { all: 'All programmes' });
    }
    qs('fFac').addEventListener('change', depts);
    qs('fDept').addEventListener('change', progs);
    depts();

    var MAP = { q: 'fQ', status: 'fStatus', type: 'fType', severity: 'fSev', year: 'fYear', campus: 'fCampus', faculty: 'fFac', dept: 'fDept', prog: 'fProg', flag: 'fFlag', effect: 'fEffect', sort: 'fSort' };
    var u = FA.readUrl();
    Object.keys(MAP).forEach(function (k) { if (u[k] !== undefined) { if (k === 'faculty') { FA.set(MAP[k], u[k]); depts(); } else if (k === 'dept') { FA.set(MAP[k], u[k]); progs(); } else FA.set(MAP[k], u[k]); } });
    if (u.regno) state.regno = u.regno;
    if (u.incident) state.incident = u.incident;
    if (u.page) state.page = parseInt(u.page, 10) || 1;

    function cfg() {
        var o = {};
        Object.keys(MAP).forEach(function (k) { o[k] = FA.val(MAP[k]); });
        if (state.regno) o.regno = state.regno;
        if (state.incident) o.incident = state.incident;
        return o;
    }

    Object.keys(MAP).forEach(function (k) {
        var el = qs(MAP[k]);
        el.addEventListener(k === 'q' ? 'input' : 'change', k === 'q' ? FA.debounce(function () { state.page = 1; load(); }, 350) : function () { state.page = 1; load(); });
    });
    qs('btnReset').onclick = function () {
        Object.keys(MAP).forEach(function (k) { FA.set(MAP[k], ''); });
        state.regno = ''; state.incident = ''; depts(); state.page = 1; load();
    };

    function chips() {
        var h = '';
        if (state.regno) h += '<span class="fa-chip">Student ' + esc(state.regno) + '<button type="button" data-x="regno" aria-label="Remove">&times;</button></span>';
        if (state.incident) h += '<span class="fa-chip">One incident<button type="button" data-x="incident" aria-label="Remove">&times;</button></span>';
        qs('rChips').innerHTML = h;
        Array.prototype.forEach.call(qs('rChips').querySelectorAll('[data-x]'), function (b) { b.onclick = function () { state[b.getAttribute('data-x')] = ''; load(); }; });
    }

    // ── List ──────────────────────────────────────────────────────────
    function load() {
        var c = cfg(); c.page = state.page; FA.writeUrl(c); chips();
        qs('rBody').innerHTML = '<tr><td colspan="8" class="fa-loading">Loading</td></tr>';
        FA.call(PAGE, 'GetCases', { configJson: JSON.stringify(cfg()), page: state.page, size: state.size }, function (r) {
            if (!r.success) { qs('rBody').innerHTML = '<tr><td colspan="8" class="fa-empty">' + esc(r.message) + '</td></tr>'; return; }
            lastRows = r.rows;
            qs('rCount').textContent = FA.plural(r.total, 'case', 'cases');
            if (!r.rows.length) qs('rBody').innerHTML = '<tr><td colspan="8" class="fa-empty">No cases match these filters.</td></tr>';
            else qs('rBody').innerHTML = r.rows.map(row).join('');
            Array.prototype.forEach.call(qs('rBody').querySelectorAll('tr[data-id]'), function (tr) {
                tr.onclick = function (e) {
                    if (e.target.tagName === 'INPUT') return;
                    location.href = DC.caseLink(tr.getAttribute('data-id'));
                };
                var cb = tr.querySelector('input');
                if (cb) cb.onchange = function () { if (cb.checked) selected[tr.getAttribute('data-id')] = true; else delete selected[tr.getAttribute('data-id')]; batch(); };
            });
            FA.pager('rPager', { total: r.total, page: state.page, size: state.size }, function (p) { state.page = p; load(); });
            batch();
        });
    }

    function row(x) {
        return '<tr class="is-click" data-id="' + x.id + '">' +
            '<td class="fa-check"><input type="checkbox"' + (selected[x.id] ? ' checked' : '') + ' aria-label="Select ' + esc(x.caseNo) + '"/></td>' +
            '<td><span class="fa-code">' + esc(x.caseNo) + '</span>' + (x.restricted ? ' ' + DC.restricted() : '') + '<span class="fa-sub">Reported ' + esc(x.opened) + '</span></td>' +
            '<td><b>' + esc(x.name) + '</b><span class="fa-sub">' + esc(x.regno) + (x.programme ? ', ' + esc(x.programme) : '') + '</span></td>' +
            '<td>' + esc(x.type) + '<span class="fa-sub">' + DC.sev(x.severity, x.severityText) + '</span></td>' +
            '<td>' + DC.status(x.status, x.statusText) + (x.followUps ? '<span class="fa-sub" style="color:#8a6500">' + x.followUps + ' follow-up' + (x.followUps === 1 ? '' : 's') + ' owed</span>' : '') +
            (x.appealDeadline && x.status === 'DECIDED' ? '<span class="fa-sub">Appeal by ' + esc(x.appealDeadline) + '</span>' : '') + '</td>' +
            '<td>' + (x.hearing && (x.status === 'HEARING_SCHEDULED' || x.status === 'SUMMONED') ? esc(x.hearing) + '<span class="fa-sub">' + esc(x.venue) + '</span>' : '<span class="fa-muted">None</span>') + '</td>' +
            '<td>' + (x.inForce ? '<div class="dc-force"><span>' + esc(x.inForce) + '</span></div>' : '<span class="fa-muted">None</span>') + '</td>' +
            '<td>' + esc(x.updated) + '</td></tr>';
    }

    qs('rAll').onchange = function () {
        var on = qs('rAll').checked;
        lastRows.forEach(function (x) { if (on) selected[x.id] = true; else delete selected[x.id]; });
        Array.prototype.forEach.call(qs('rBody').querySelectorAll('input[type=checkbox]'), function (c) { c.checked = on; });
        batch();
    };
    function ids() { return Object.keys(selected).map(function (k) { return parseInt(k, 10); }); }
    function batch() {
        var n = ids().length;
        qs('rBatch').className = 'fa-batch' + (n && R.hearing ? ' is-on' : '');
        qs('rBatchText').textContent = FA.plural(n, 'case selected', 'cases selected');
    }
    qs('btnBatchClear').onclick = function () { selected = {}; qs('rAll').checked = false; load(); };
    qs('btnBatchHearing').onclick = function () { hearingDialog(ids()); };

    function hearingDialog(caseIds) {
        var m = FA.modal({
            title: 'Schedule one hearing', sub: FA.plural(caseIds.length, 'case', 'cases') + ' heard together; each student is summoned separately', size: 'mid',
            body: '<div class="fa-form">' +
                '<div class="fa-field"><label class="fa-label" for="jhAt">Date and time <span class="fa-req">*</span></label><input type="datetime-local" id="jhAt" class="fa-input"/></div>' +
                '<div class="fa-field"><label class="fa-label" for="jhVenue">Venue <span class="fa-req">*</span></label><input id="jhVenue" class="fa-input" maxlength="200"/></div>' +
                '<div class="fa-field fa-full"><label class="fa-label" for="jhPanel">Panel</label><input id="jhPanel" class="fa-input" maxlength="1000" placeholder="Names of the panel members"/></div>' +
                '<div class="fa-field fa-full"><label class="fa-check-line"><input type="checkbox" id="jhSummon" checked/> Summon the students now (issues each summons letter and notifies them)</label></div>' +
                '<div class="fa-field fa-full"><label class="fa-label" for="jhShort">Reason for short notice</label><input id="jhShort" class="fa-input" maxlength="300" placeholder="Needed only when the hearing is less than 3 days away"/></div>' +
                '</div><div class="fa-error" id="jhErr" style="margin-top:10px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Schedule</button>'
        });
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        var ok = m.foot.querySelector('[data-ok]');
        ok.onclick = function () {
            DC.save(PAGE, 'ScheduleJointHearing', { json: JSON.stringify({ caseIds: caseIds, at: FA.val('jhAt'), venue: FA.val('jhVenue'), panel: FA.val('jhPanel'), summon: FA.val('jhSummon'), shortNoticeReason: FA.val('jhShort') }) }, ok, function (err, r) {
                if (err) { qs('jhErr').textContent = err; return; }
                if (r.failed && r.failed.length) { qs('jhErr').innerHTML = 'Scheduled ' + r.done + '. Not scheduled:<br/>' + r.failed.map(esc).join('<br/>'); load(); return; }
                m.close(); FA.toast('Hearing scheduled for ' + FA.plural(r.done, 'case', 'cases') + '.'); selected = {}; load();
            });
        };
    }

    // ── Export ────────────────────────────────────────────────────────
    qs('btnExport').innerHTML = FA.icon('download') + ' Export';
    qs('btnExport').onclick = function () {
        FA.exportDialog({ page: PAGE, report: 'list', title: 'Disciplinary records (current filters)', cfg: cfg(), countMethod: 'CountExport',
            cols: [{ k: 'case_no', t: 'Case no', on: true }, { k: 'opened', t: 'Reported', on: true }, { k: 'student', t: 'Student', on: true }, { k: 'regno', t: 'Reg no', on: true },
                   { k: 'programme', t: 'Programme', on: true }, { k: 'campus', t: 'Campus', on: true }, { k: 'type', t: 'Type', on: true }, { k: 'severity', t: 'Severity', on: true },
                   { k: 'status', t: 'Status', on: true }, { k: 'hearing', t: 'Next hearing', on: true }, { k: 'in_force', t: 'In force', on: true }, { k: 'updated', t: 'Updated', on: true },
                   { k: 'officer', t: 'Officer', on: false }],
            note: R.seeRestricted ? 'Restricted cases in the list are included, and each export of one is logged.' : '' });
    };

    // ── New case ──────────────────────────────────────────────────────
    if (R.report || R.manage || R.admin) {
        var nb = document.createElement('button');
        nb.type = 'button'; nb.className = 'fa-btn fa-btn--inverse'; nb.innerHTML = FA.icon('plus') + ' Report a case';
        nb.onclick = newCase;
        qs('faHeaderActions').appendChild(nb);
    }

    function newCase() {
        var picked = [], step = 1, files = [], ticket = null, paper = null, reporter = null;
        var typeOpts = '<option value="">Choose the type</option>' + (B.types || []).map(function (t) { return '<option value="' + t.id + '">' + esc(t.name) + '</option>'; }).join('');
        var measures = (B.interim || []).map(function (x) {
            return '<div class="dc-sanction" data-code="' + esc(x.code) + '"><label class="dc-sanction__head"><input type="checkbox"/><span class="dc-sanction__name">' + esc(x.name) +
                   '</span><span class="dc-sanction__fx">' + esc(x.effects) + '</span></label><div class="dc-sanction__body"><div class="fa-form">' +
                   '<div class="fa-field fa-full"><label class="fa-label">Reason <span class="fa-req">*</span></label><textarea class="fa-textarea" rows="2" data-f="reason" maxlength="1000"></textarea></div>' +
                   '<div class="fa-field"><label class="fa-label">Until (optional)</label><input type="date" class="fa-input" data-f="to" min="' + FA.today() + '"/><span class="fa-hint">Leave blank to last until the case is decided.</span></div>' +
                   '</div></div></div>';
        }).join('');
        var m = FA.modal({
            title: 'Report a case', sub: 'One incident; a separate case is opened for each student', size: 'wide', sticky: true,
            body: '<ol class="fa-steps"><li data-s="1"><b>Step 1</b>Students</li><li data-s="2"><b>Step 2</b>What happened</li><li data-s="3"><b>Step 3</b>Evidence and measures</li></ol>' +
                '<div data-p="1"><div class="fa-field"><label class="fa-label">Find a student by name or registration number</label><div id="ncFind"></div></div>' +
                '<div id="ncPicked" style="margin-top:12px"></div></div>' +
                '<div data-p="2" style="display:none"><div class="fa-form">' +
                '<div class="fa-field"><label class="fa-label" for="ncType">Case type <span class="fa-req">*</span></label><select id="ncType" class="fa-select">' + typeOpts + '</select><span class="fa-hint" id="ncTypeHint"></span></div>' +
                '<div class="fa-field"><label class="fa-label" for="ncSev">Severity <span class="fa-req">*</span></label><select id="ncSev" class="fa-select"><option value="MINOR">Minor</option><option value="SERIOUS">Serious</option><option value="GROSS">Gross</option></select></div>' +
                '<div class="fa-field"><label class="fa-label" for="ncAt">Date and time <span class="fa-req">*</span></label><input type="datetime-local" id="ncAt" class="fa-input" max="' + esc(B.now) + '"/></div>' +
                '<div class="fa-field"><label class="fa-label" for="ncPlace">Place</label><input id="ncPlace" class="fa-input" maxlength="200" placeholder="For example Main Hall, Kakeeka"/></div>' +
                '<div class="fa-field"><label class="fa-label" for="ncCampus">Campus</label><select id="ncCampus" class="fa-select"></select></div>' +
                '<div class="fa-field"><label class="fa-label">Reporting officer</label><div id="ncReporter"></div><span class="fa-hint">Defaults to you.</span></div>' +
                '<div class="fa-field fa-full"><label class="fa-label" for="ncDesc">What happened <span class="fa-req">*</span></label><textarea id="ncDesc" class="fa-textarea" rows="5" maxlength="8000"></textarea><span class="fa-hint">Facts only: who, what, when, where, and what was seen or found.</span></div>' +
                '<div class="fa-field fa-full"><label class="fa-label" for="ncWit">Witnesses</label><textarea id="ncWit" class="fa-textarea" rows="2" maxlength="2000" placeholder="Names and how to reach them"></textarea></div>' +
                '<div class="fa-field"><label class="fa-label">Related complaint ticket</label><div id="ncTicket"></div></div>' +
                '<div class="fa-field" id="ncPaperWrap" style="display:none"><label class="fa-label">Examination paper</label><div id="ncPaper"></div><span class="fa-hint">The timetable entry: course, session and invigilator.</span></div>' +
                '</div></div>' +
                '<div data-p="3" style="display:none">' +
                '<div class="fa-section">Evidence</div><div class="fa-field"><input type="file" id="ncFiles" multiple accept=".pdf,.jpg,.jpeg,.png,.docx,.xlsx"/>' +
                '<span class="fa-hint">PDF, photos, Word or Excel, up to 15 MB each. Each file is added to every student\'s case.</span></div><div id="ncFileList" style="margin-top:8px"></div>' +
                (measures ? '<div class="fa-section">Interim measures</div>' + (R.restrict ? '<p class="fa-hint" style="margin:0 0 8px">These apply at once and are shown to the student.</p>'
                         : '<div class="fa-notice fa-notice--warn">You can recommend a measure. An officer with permission to apply interim measures must confirm it before it takes effect.</div>') + measures : '') +
                '<div class="fa-section">Confidentiality</div><label class="fa-check-line"><input type="checkbox" id="ncRestricted"/> Restricted case: only officers allowed to see restricted cases can see it</label>' +
                '<span class="fa-hint" id="ncRestrictedHint" style="display:block;margin-top:4px"></span>' +
                '</div><div class="fa-error" id="ncErr" style="margin-top:10px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-back>Back</button><span class="fa-spacer"></span>' +
                  '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-next>Next</button>',
            beforeClose: function () { return picked.length === 0 && step === 1 ? true : confirm('Close the form? What you entered will be lost.'); }
        });
        var body = m.body;
        FA.fill('ncCampus', B.campuses, { all: 'Choose' });
        FA.set('ncAt', B.now);
        FA.typeahead('ncFind', {
            source: DC.studentSource(PAGE), render: DC.studentRender, placeholder: 'At least 2 letters', free: false,
            onPick: function (s) {
                if (!s) return;
                if (picked.some(function (p) { return p.regno === s.regno; })) { FA.toast('That student is already added.', true); }
                else picked.push(s);
                drawPicked();
                setTimeout(function () { var i = qs('ncFind').querySelector('button'); if (i) i.click(); }, 0);
            }
        });
        reporter = FA.typeahead('ncReporter', { source: DC.staffSource(PAGE), render: DC.staffRender, placeholder: 'You (' + (B.me || '') + ')' });
        ticket = FA.typeahead('ncTicket', {
            source: function (q, cb) { FA.call(PAGE, 'SearchTickets', { q: q }, function (r) { cb(r.success ? r.rows : []); }); },
            render: function (t) { return esc(t.name) + '<small>' + esc([t.by, t.type, t.date].filter(Boolean).join(', ')) + '</small>'; }, placeholder: 'Ticket number or subject'
        });
        var ut = FA.readUrl().ticket;
        if (ut) ticket.set({ id: ut, name: 'Complaint ticket #' + ut });
        paper = FA.typeahead('ncPaper', {
            source: function (q, cb) { FA.call(PAGE, 'SearchExamPapers', { q: q }, function (r) { cb(r.success ? r.rows : []); }); },
            render: function (p) { return esc(p.name) + '<small>' + esc([p.date, p.time, p.venue, p.acadYear + ' sem ' + p.semester].filter(Boolean).join(', ')) + '</small>'; },
            pickedText: function (p) { return p.name + ', ' + p.date; }, placeholder: 'Course code or title'
        });

        function drawPicked() {
            qs('ncPicked').innerHTML = picked.length ? picked.map(function (s, i) {
                return '<div class="dc-picked"><div class="dc-student">' + DC.photo(s.regno) + '<div><div class="dc-student__name">' + esc(s.name) + '</div>' +
                    '<div class="dc-student__meta">' + esc(s.regno) + '<br/>' + esc(s.programme) + (s.campus ? ', ' + esc(s.campus) : '') + (s.status ? '<br/>Status: ' + esc(s.status) : '') + '</div>' +
                    (s.cases && s.cases.length ? '<div class="dc-picked__warn">Other cases: ' + s.cases.map(function (c) { return esc(c.caseNo) + ' (' + esc(c.statusText) + ')'; }).join(', ') + '</div>' : '') +
                    (s.restrictions ? '<div class="dc-picked__warn">In force: ' + esc(s.restrictions) + '</div>' : '') +
                    '</div></div><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-rm="' + i + '">Remove</button></div>';
            }).join('') : '<p class="fa-hint">No student added yet. Add every student involved in the incident.</p>';
            Array.prototype.forEach.call(qs('ncPicked').querySelectorAll('[data-rm]'), function (b) { b.onclick = function () { picked.splice(parseInt(b.getAttribute('data-rm'), 10), 1); drawPicked(); }; });
        }
        drawPicked();

        qs('ncType').onchange = function () {
            var t = null; (B.types || []).forEach(function (x) { if (String(x.id) === qs('ncType').value) t = x; });
            if (!t) return;
            FA.set('ncSev', t.severity);
            qs('ncTypeHint').textContent = t.description || '';
            qs('ncPaperWrap').style.display = t.exam ? '' : 'none';
            qs('ncRestricted').checked = t.restricted;
            qs('ncRestricted').disabled = t.restricted && !R.seeRestricted;
            qs('ncRestrictedHint').textContent = t.restricted ? 'Cases of this type are restricted by default.' : '';
        };
        Array.prototype.forEach.call(body.querySelectorAll('.dc-sanction'), function (s) {
            var cb = s.querySelector('input[type=checkbox]');
            cb.onchange = function () { s.className = 'dc-sanction' + (cb.checked ? ' is-on' : ''); };
        });
        qs('ncFiles').onchange = function () {
            files = Array.prototype.slice.call(qs('ncFiles').files || []);
            qs('ncFileList').innerHTML = files.map(function (f) { return '<span class="dc-file">' + DC.icon('paperclip', 12) + esc(f.name) + ' (' + Math.ceil(f.size / 1024) + ' KB)</span>'; }).join(' ');
        };

        function show() {
            Array.prototype.forEach.call(body.querySelectorAll('[data-p]'), function (p) { p.style.display = p.getAttribute('data-p') === String(step) ? '' : 'none'; });
            Array.prototype.forEach.call(body.querySelectorAll('.fa-steps li'), function (li) {
                var s = parseInt(li.getAttribute('data-s'), 10); li.className = s === step ? 'is-now' : s < step ? 'is-done' : '';
            });
            m.foot.querySelector('[data-back]').style.visibility = step === 1 ? 'hidden' : 'visible';
            m.foot.querySelector('[data-next]').textContent = step === 3 ? (picked.length > 1 ? 'Open ' + picked.length + ' cases' : 'Open the case') : 'Next';
            qs('ncErr').textContent = '';
        }
        function check() {
            if (step === 1 && !picked.length) return 'Add at least one student.';
            if (step === 2) {
                if (!qs('ncType').value) return 'Choose the case type.';
                if (!FA.val('ncAt')) return 'Give the date and time of the incident.';
                if (FA.val('ncDesc').length < 15) return 'Describe what happened in at least 15 characters.';
            }
            if (step === 3) {
                var bad = '';
                Array.prototype.forEach.call(body.querySelectorAll('.dc-sanction.is-on'), function (s) {
                    if (s.querySelector('[data-f=reason]').value.trim().length < 10) bad = 'Give a reason of at least 10 characters for each interim measure.';
                });
                if (bad) return bad;
                for (var i = 0; i < files.length; i++) if (files[i].size > 15 * 1024 * 1024) return files[i].name + ' is larger than 15 MB.';
            }
            return '';
        }
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        m.foot.querySelector('[data-back]').onclick = function () { if (step > 1) { step--; show(); } };
        var next = m.foot.querySelector('[data-next]'), opId = DC.op();
        next.onclick = function () {
            var err = check(); if (err) { qs('ncErr').textContent = err; return; }
            if (step < 3) { step++; show(); return; }
            var rep = reporter.get(), tk = ticket.get(), pp = paper.get();
            var ms = Array.prototype.map.call(body.querySelectorAll('.dc-sanction.is-on'), function (s) {
                return { code: s.getAttribute('data-code'), reason: s.querySelector('[data-f=reason]').value.trim(), to: s.querySelector('[data-f=to]').value };
            });
            var payload = {
                students: picked.map(function (p) { return p.regno; }), caseTypeId: qs('ncType').value, severity: qs('ncSev').value, occurredAt: FA.val('ncAt'),
                place: FA.val('ncPlace'), campusId: FA.val('ncCampus'), description: FA.val('ncDesc'), witnesses: FA.val('ncWit'),
                reporterUser: rep ? rep.username : '', reporterName: rep ? rep.name : '', reporterEmpId: rep ? rep.id : '',
                ticketId: tk ? tk.id : '', examTimetableId: pp ? pp.id : '', courseCode: pp ? pp.course : '', examAcadYear: pp ? pp.acadYear : '', examSemester: pp ? pp.semester : '',
                restricted: qs('ncRestricted').checked, measures: ms, opId: opId
            };
            DC.save(PAGE, 'CreateIncident', { json: JSON.stringify(payload) }, next, function (e, r) {
                if (e) { qs('ncErr').textContent = e; return; }
                var ids = r.caseIds || [];
                uploadAll(ids, function (fails) {
                    picked = []; step = 1; m.close();
                    if (fails.length) FA.toast('Case opened, but some files were not attached: ' + fails.join('; '), true);
                    if (payload.restricted && !R.seeRestricted) { FA.toast('Case opened. It is restricted, so only authorised officers can open it.'); load(); }
                    else if (ids.length === 1) location.href = DC.caseLink(ids[0]);
                    else { FA.toast(ids.length + ' cases opened.'); load(); }
                });
            });
        };
        function uploadAll(ids, cb) {
            var jobs = [], fails = [];
            ids.forEach(function (id) { files.forEach(function (f) { jobs.push({ id: id, f: f }); }); });
            (function nextJob() {
                var j = jobs.shift(); if (!j) { cb(fails); return; }
                var fd = new FormData(); fd.append('caseId', j.id); fd.append('kind', 'EVIDENCE'); fd.append('file', j.f);
                var x = new XMLHttpRequest(); x.open('POST', 'DcUpload.ashx', true); x.setRequestHeader('X-CSRF-Token', FA.token());
                x.onreadystatechange = function () {
                    if (x.readyState !== 4) return;
                    var ok = false; try { ok = JSON.parse(x.responseText).success; } catch (e) { }
                    if (!ok) { var msg = j.f.name; try { msg += ': ' + JSON.parse(x.responseText).message; } catch (e2) { } if (fails.indexOf(msg) < 0) fails.push(msg); }
                    nextJob();
                };
                x.send(fd);
            })();
        }
        show();
    }

    load();
    if (FA.readUrl().new === '1' && (R.report || R.manage || R.admin)) newCase();
})();
