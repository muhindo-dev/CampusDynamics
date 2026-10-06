/* Student Disciplinary module: case types, sanctions, letter templates, committee and settings. ES5. */
(function () {
    'use strict';
    var PAGE = 'DisciplinarySettings.aspx', esc = FA.esc, qs = FA.qs, S = null, tab = FA.readUrl().tab || 'types';
    var TABS = [['types', 'Case types'], ['sanctions', 'Sanctions'], ['letters', 'Letter templates'], ['committee', 'Committee'], ['settings', 'Settings']];

    function load() {
        FA.call(PAGE, 'Load', {}, function (r) {
            if (!r.success) { qs('sBody').innerHTML = '<div class="fa-notice fa-notice--bad">' + esc(r.message) + '</div>'; return; }
            S = r; draw();
        });
    }

    function draw() {
        qs('sTabs').innerHTML = TABS.map(function (t) { return '<button type="button" class="fa-subtab' + (t[0] === tab ? ' is-active' : '') + '" data-t="' + t[0] + '">' + t[1] + '</button>'; }).join('');
        Array.prototype.forEach.call(qs('sTabs').querySelectorAll('[data-t]'), function (b) { b.onclick = function () { tab = b.getAttribute('data-t'); FA.writeUrl({ tab: tab }); draw(); }; });
        var h = S.canEdit ? '' : '<div class="fa-notice">You can read these settings. Changing them needs the settings permission.</div>';
        if (tab === 'types') h += typesTab();
        else if (tab === 'sanctions') h += sanctionsTab();
        else if (tab === 'letters') h += lettersTab();
        else if (tab === 'committee') h += committeeTab();
        else h += settingsTab();
        qs('sBody').innerHTML = h;
        Array.prototype.forEach.call(qs('sBody').querySelectorAll('[data-edit]'), function (b) { b.onclick = function () { edit(b.getAttribute('data-edit'), b.getAttribute('data-id')); }; });
    }

    function add(kind, label) { return S.canEdit ? '<div class="fa-row" style="margin-bottom:10px"><span class="fa-spacer"></span><button type="button" class="fa-btn fa-btn--primary" data-edit="' + kind + '" data-id="0">' + FA.icon('plus') + ' ' + esc(label) + '</button></div>' : ''; }
    function editBtn(kind, id) { return S.canEdit ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-edit="' + kind + '" data-id="' + id + '">Edit</button>' : ''; }
    function yes(b) { return b ? 'Yes' : '<span class="fa-muted">No</span>'; }
    function active(b) { return b ? '<span class="fa-badge fa-badge--ok">Active</span>' : '<span class="fa-badge fa-badge--neutral">Inactive</span>'; }
    function find(list, id) { var x = null; list.forEach(function (i) { if (String(i.id) === String(id)) x = i; }); return x; }
    function sanctionName(id) { var s = find(S.sanctionTypes, id); return s ? s.name : '#' + id; }

    function typesTab() {
        return add('type', 'Add a case type') + '<div class="fa-card"><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Code</th><th>Case type</th><th>Severity</th><th>Exam</th><th>Restricted</th><th>Usual sanctions</th><th>Status</th><th></th></tr></thead><tbody>' +
            S.caseTypes.map(function (t) {
                return '<tr><td class="fa-code">' + esc(t.code) + '</td><td><b>' + esc(t.name) + '</b>' + (t.description ? '<span class="fa-sub">' + esc(t.description) + '</span>' : '') + '</td>' +
                    '<td>' + DC.sev(t.severity, t.severity.charAt(0) + t.severity.slice(1).toLowerCase()) + '</td><td>' + yes(t.exam) + '</td><td>' + yes(t.restricted) + '</td>' +
                    '<td style="max-width:320px">' + esc(t.sanctions.map(sanctionName).join(', ')) + '</td><td>' + active(t.active) + '</td><td>' + editBtn('type', t.id) + '</td></tr>';
            }).join('') + '</tbody></table></div></div>';
    }

    function sanctionsTab() {
        return add('sanction', 'Add a sanction') + '<div class="fa-card"><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Code</th><th>Sanction</th><th>Outcome</th><th>What it does in the system</th><th>Needs</th><th>Interim</th><th>Status</th><th></th></tr></thead><tbody>' +
            S.sanctionTypes.map(function (t) {
                var needs = [];
                if (t.needsAmount) needs.push('amount');
                if (t.needsDates === 'FROM') needs.push('start date');
                if (t.needsDates === 'FROM_TO') needs.push('start and end dates');
                if (t.needsCourse) needs.push('course');
                if (t.needsSemester) needs.push('year and semester');
                return '<tr><td class="fa-code">' + esc(t.code) + '</td><td><b>' + esc(t.name) + '</b>' + (t.description ? '<span class="fa-sub">' + esc(t.description) + '</span>' : '') + '</td>' +
                    '<td>' + esc(t.outcome.charAt(0) + t.outcome.slice(1).toLowerCase()) + '</td><td>' + esc(t.effectsText || 'Recorded only') + '</td><td>' + esc(needs.join(', ') || 'Nothing') + '</td>' +
                    '<td>' + yes(t.interim) + '</td><td>' + active(t.active) + '</td><td>' + editBtn('sanction', t.id) + '</td></tr>';
            }).join('') + '</tbody></table></div></div>';
    }

    function lettersTab() {
        return add('template', 'Add a template') + '<div class="fa-notice">Merge fields: ' + S.mergeFields.map(function (f) { return '<span class="fa-code">{{' + esc(f) + '}}</span>'; }).join(' ') +
            '. The letterhead, reference, date, addressee and the Registrar\'s signature block are added to every letter.</div>' +
            S.templates.map(function (t) {
                return '<div class="fa-card"><div class="fa-card__head"><div><div class="fa-card__title">' + esc(t.name) + ' <span class="fa-card__meta fa-code">' + esc(t.code) + '</span></div>' +
                    '<div class="fa-hint">' + (t.studentCopy ? 'Student copy on the portal' : 'Internal') + '</div></div><div class="fa-row">' + active(t.active) + editBtn('template', t.id) + '</div></div>' +
                    '<div class="fa-card__body"><div class="dc-letter" style="max-height:220px"><h4>RE: ' + esc(t.subject) + '</h4>' +
                    t.body.split(/\n\s*\n/).map(function (p) { return '<p>' + esc(p.trim()) + '</p>'; }).join('') + '</div></div></div>';
            }).join('');
    }

    function committeeTab() {
        var years = {}; S.members.forEach(function (m) { (years[m.year] = years[m.year] || []).push(m); });
        if (!years[S.year]) years[S.year] = [];
        return add('member', 'Add a member') + Object.keys(years).sort().reverse().map(function (y) {
            return '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Students Disciplinary Committee, ' + esc(y) + '</div></div>' +
                (years[y].length ? '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Member</th><th>Role</th><th>Login</th><th>Status</th><th></th></tr></thead><tbody>' +
                years[y].map(function (m) {
                    return '<tr><td><b>' + esc(m.name) + '</b></td><td>' + esc(m.roleText) + '</td><td>' + esc(m.username || 'None') + '</td><td>' + active(m.active) + '</td><td>' + editBtn('member', m.id) + '</td></tr>';
                }).join('') + '</tbody></table></div>' : '<div class="fa-empty">No members recorded for this year.</div>') + '</div>';
        }).join('') + '<p class="fa-hint">The chair of the current year sees restricted cases. Members need the Disciplinary Committee role in User Roles to record decisions.</p>';
    }

    var LABELS = { appeal_window_days: 'Appeal window (days)', overdue_no_update_days: 'Overdue after no update for (days)', appeal_closing_days: 'Warn when the appeal window closes within (days)',
                   contact_office: 'Office the student contacts', contact_details: 'Contact details', appellate_authority: 'Body that decides appeals', letter_office: 'Office line on letters',
                   summon_notice_days: 'Minimum notice for a summons (days)', attachment_max_mb: 'Largest file (MB)' };
    function settingsTab() {
        return '<div class="fa-card"><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Setting</th><th>Value</th><th>Last changed</th><th></th></tr></thead><tbody>' +
            S.settings.map(function (s) {
                return '<tr><td><b>' + esc(LABELS[s.key] || s.key) + '</b><span class="fa-sub">' + esc(s.description) + '</span></td><td>' + esc(s.value) + '</td><td>' + esc(s.at) + '<span class="fa-sub">' + esc(s.by) + '</span></td>' +
                    '<td>' + (S.canEdit ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-edit="setting" data-id="' + esc(s.key) + '">Change</button>' : '') + '</td></tr>';
            }).join('') + '</tbody></table></div></div>';
    }

    function field(label, input, hint, full, req) {
        return '<div class="fa-field' + (full ? ' fa-full' : '') + '"><label class="fa-label">' + esc(label) + (req ? ' <span class="fa-req">*</span>' : '') + '</label>' + input + (hint ? '<span class="fa-hint">' + hint + '</span>' : '') + '</div>';
    }
    function check(id, label, on) { return '<label class="fa-check-line"><input type="checkbox" id="' + id + '"' + (on ? ' checked' : '') + '/> ' + esc(label) + '</label>'; }

    function edit(kind, id) {
        var body = '', item = null, what = '', title = '', staff = null;
        if (kind === 'type') {
            item = find(S.caseTypes, id) || { id: 0, code: '', name: '', description: '', severity: 'SERIOUS', exam: false, restricted: false, sort: 0, active: true, sanctions: [], version: 0 };
            what = 'caseType'; title = item.id ? 'Edit case type' : 'Add a case type';
            body = '<div class="fa-form">' + field('Code', '<input id="xCode" class="fa-input" maxlength="20" value="' + esc(item.code) + '"/>', 'Capital letters, for example EXAM_MALPRACTICE.', false, true) +
                field('Name', '<input id="xName" class="fa-input" maxlength="150" value="' + esc(item.name) + '"/>', '', false, true) +
                field('Description', '<textarea id="xDesc" class="fa-textarea" rows="2" maxlength="600">' + esc(item.description) + '</textarea>', '', true) +
                field('Usual severity', '<select id="xSev" class="fa-select"><option value="MINOR">Minor</option><option value="SERIOUS">Serious</option><option value="GROSS">Gross</option></select>', 'The officer can change it on each case.', false, true) +
                field('Sort order', '<input type="number" id="xSort" class="fa-input" value="' + item.sort + '"/>') +
                '<div class="fa-field fa-full">' + check('xExam', 'Examination related (case form asks for the paper; the examination officer manages these cases)', item.exam) +
                check('xRes', 'Restricted by default (only officers allowed to see restricted cases can see them)', item.restricted) + (item.id ? check('xAct', 'Active', item.active) : '') + '</div>' +
                '<div class="fa-field fa-full"><span class="fa-label">Usual sanctions</span><div class="fa-cols" id="xSanc">' + S.sanctionTypes.filter(function (s) { return s.active; }).map(function (s) {
                    return '<label class="fa-check-line"><input type="checkbox" value="' + s.id + '"' + (item.sanctions.indexOf(s.id) >= 0 ? ' checked' : '') + '/> ' + esc(s.name) + '</label>';
                }).join('') + '</div><span class="fa-hint">Offered first when deciding a case of this type. Any sanction can still be chosen.</span></div></div>';
        } else if (kind === 'sanction') {
            item = find(S.sanctionTypes, id) || { id: 0, code: '', name: '', description: '', outcome: 'SANCTION', effects: '', needsAmount: false, needsDates: 'NONE', needsCourse: false, needsSemester: false, interim: false, sort: 0, active: true, version: 0 };
            what = 'sanctionType'; title = item.id ? 'Edit sanction' : 'Add a sanction';
            var eff = (item.effects || '').split(',');
            body = (item.id ? '<div class="fa-notice">Changes apply to sanctions recorded from now on. Sanctions already recorded keep the effects they were given.</div>' : '') +
                '<div class="fa-form">' + field('Code', '<input id="xCode" class="fa-input" maxlength="20" value="' + esc(item.code) + '"/>', '', false, true) +
                field('Name', '<input id="xName" class="fa-input" maxlength="150" value="' + esc(item.name) + '"/>', '', false, true) +
                field('Description', '<textarea id="xDesc" class="fa-textarea" rows="2" maxlength="600">' + esc(item.description) + '</textarea>', '', true) +
                field('Outcome', '<select id="xOut" class="fa-select"><option value="SANCTION">Sanction</option><option value="DISMISSAL">Dismissal</option><option value="ACQUITTAL">Acquittal</option></select>', '', false, true) +
                field('Dates it needs', '<select id="xDates" class="fa-select"><option value="NONE">None</option><option value="FROM">A start date</option><option value="FROM_TO">Start and end dates</option></select>', '', false, true) +
                '<div class="fa-field fa-full"><span class="fa-label">What it does in the system</span><div class="fa-cols" id="xEff">' + S.effects.map(function (e) {
                    var lbl = { PORTAL_BLOCK: 'Blocks the student portal', RESULTS_WITHHELD: 'Withholds results, exam card and transcript', SUSPENSION: 'Suspends: no registration or examinations',
                                EXPULSION: 'Expels', GRADUATION_BAR: 'Bars graduation', CANCEL_PAPER: 'Marks office cancels a paper result', CANCEL_SEMESTER: 'Marks office cancels a semester',
                                FINE: 'Bursar bills a fine', RESTITUTION: 'Bursar bills restitution' }[e] || e;
                    return '<label class="fa-check-line"><input type="checkbox" value="' + e + '"' + (eff.indexOf(e) >= 0 ? ' checked' : '') + '/> ' + esc(lbl) + '</label>';
                }).join('') + '</div></div>' +
                '<div class="fa-field fa-full">' + check('xAmt', 'Needs an amount', item.needsAmount) + check('xCourse', 'Needs a course code', item.needsCourse) +
                check('xSem', 'Needs an academic year and semester', item.needsSemester) + check('xInt', 'May be applied as an interim measure before a decision', item.interim) +
                (item.id ? check('xAct', 'Active', item.active) : '') + '</div>' + field('Sort order', '<input type="number" id="xSort" class="fa-input" value="' + item.sort + '"/>') + '</div>';
        } else if (kind === 'template') {
            item = find(S.templates, id) || { id: 0, code: '', name: '', subject: '', body: '', studentCopy: true, sort: 0, active: true, version: 0 };
            what = 'template'; title = item.id ? 'Edit letter template' : 'Add a letter template';
            body = (item.id ? '<div class="fa-notice">Letters already issued keep their own text. Changes apply to letters issued from now on.</div>' : '') +
                '<div class="fa-form">' + field('Code', '<input id="xCode" class="fa-input" maxlength="20" value="' + esc(item.code) + '"/>', '', false, true) +
                field('Name', '<input id="xName" class="fa-input" maxlength="150" value="' + esc(item.name) + '"/>', '', false, true) +
                field('Subject', '<input id="xSubj" class="fa-input" maxlength="250" value="' + esc(item.subject) + '"/>', '', true, true) +
                field('Text', '<textarea id="xBody" class="fa-textarea" rows="12" maxlength="20000">' + esc(item.body) + '</textarea>', 'Leave a blank line between paragraphs. Use the merge fields listed on the page.', true, true) +
                '<div class="fa-field fa-full">' + check('xCopy', 'The student gets a copy on the portal', item.studentCopy) + (item.id ? check('xAct', 'Active', item.active) : '') + '</div>' +
                field('Sort order', '<input type="number" id="xSort" class="fa-input" value="' + item.sort + '"/>') + '</div>';
        } else if (kind === 'member') {
            item = find(S.members, id) || { id: 0, year: S.year, empId: 0, username: '', name: '', role: 'MEMBER', active: true };
            what = 'member'; title = item.id ? 'Edit committee member' : 'Add a committee member';
            body = '<div class="fa-form">' + field('Academic year', '<input id="xYear" class="fa-input" value="' + esc(item.year) + '" placeholder="2026/2027"/>', '', false, true) +
                field('Role on the panel', '<select id="xRole" class="fa-select"><option value="CHAIR">Chair</option><option value="SECRETARY">Secretary</option><option value="MEMBER">Member</option>' +
                      '<option value="STUDENT_REP">Student representative</option><option value="APPELLATE">Appellate authority</option></select>', '', false, true) +
                field('Staff member', '<div id="xStaff"></div>', 'Pick from the staff list, or type a name for someone who is not staff (for example the student representative).', true, true) +
                (item.id ? '<div class="fa-field fa-full">' + check('xAct', 'Active', item.active) + '</div>' : '') + '</div>';
        } else if (kind === 'setting') {
            item = null; S.settings.forEach(function (s) { if (s.key === id) item = s; });
            what = 'setting'; title = LABELS[item.key] || item.key;
            body = '<p class="fa-hint" style="margin:0 0 8px">' + esc(item.description) + '</p>' + field('Value', '<input id="xVal" class="fa-input" maxlength="1000" value="' + esc(item.value) + '"/>', '', true, true);
        }
        var m = FA.modal({ title: title, size: kind === 'template' || kind === 'type' || kind === 'sanction' ? 'mid' : '', sticky: true, body: body + '<div class="fa-error" id="xErr" style="margin-top:10px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Save</button>' });
        if (qs('xSev')) FA.set('xSev', item.severity);
        if (qs('xOut')) FA.set('xOut', item.outcome);
        if (qs('xDates')) FA.set('xDates', item.needsDates);
        if (qs('xRole')) FA.set('xRole', item.role);
        if (kind === 'member') staff = FA.typeahead('xStaff', { source: DC.staffSource(PAGE), render: DC.staffRender, free: true, value: item.name ? { id: item.empId, name: item.name, username: item.username } : null });
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        var ok = m.foot.querySelector('[data-ok]');
        ok.onclick = function () {
            var p = { id: item && item.id || 0, version: item && item.version || 0 };
            function checked(host) { return Array.prototype.map.call(qs(host).querySelectorAll('input:checked'), function (i) { return i.value; }); }
            if (kind === 'type') { p.code = FA.val('xCode'); p.name = FA.val('xName'); p.description = FA.val('xDesc'); p.severity = FA.val('xSev'); p.sort = FA.val('xSort'); p.exam = FA.val('xExam'); p.restricted = FA.val('xRes'); p.active = qs('xAct') ? FA.val('xAct') : true; p.sanctions = checked('xSanc'); }
            if (kind === 'sanction') { p.code = FA.val('xCode'); p.name = FA.val('xName'); p.description = FA.val('xDesc'); p.outcome = FA.val('xOut'); p.needsDates = FA.val('xDates'); p.effects = checked('xEff').join(','); p.needsAmount = FA.val('xAmt'); p.needsCourse = FA.val('xCourse'); p.needsSemester = FA.val('xSem'); p.interim = FA.val('xInt'); p.sort = FA.val('xSort'); p.active = qs('xAct') ? FA.val('xAct') : true; }
            if (kind === 'template') { p.code = FA.val('xCode'); p.name = FA.val('xName'); p.subject = FA.val('xSubj'); p.body = qs('xBody').value; p.studentCopy = FA.val('xCopy'); p.sort = FA.val('xSort'); p.active = qs('xAct') ? FA.val('xAct') : true; }
            if (kind === 'member') { var s = staff.get(); p.year = FA.val('xYear'); p.role = FA.val('xRole'); p.name = s ? s.name : ''; p.empId = s && s.id ? s.id : ''; p.username = s && s.username ? s.username : ''; p.active = qs('xAct') ? FA.val('xAct') : true; }
            if (kind === 'setting') { p = { key: item.key, value: FA.val('xVal') }; }
            DC.save(PAGE, 'Save', { what: what, json: JSON.stringify(p) }, ok, function (err) {
                if (err) { qs('xErr').textContent = err; return; }
                m.close(); FA.toast('Saved.'); load();
            });
        };
    }

    load();
})();
