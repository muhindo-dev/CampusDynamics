/* General Ledger: Finance Warnings. */
(function () {
    'use strict';
    var FA = window.FA, GL = window.GL, B = window.GL_BOOT || {}, PAGE = 'AccountsWarnings.aspx';
    if (B.error) { FA.qs('wErr').innerHTML = '<div class="fa-notice fa-notice--bad">' + FA.esc(B.error) + '</div>'; return; }
    var R = B.rights || {}, can = !!R.manage, me = R.user || '';
    var rules = {}; (B.rules || []).forEach(function (r) { rules[r.code] = r; });
    var u = FA.readUrl();
    var st = { tab: u.tab || 'OPEN', rows: [], open: u.id ? parseInt(u.id, 10) : 0 };
    if (u.rule) FA.qs('wQ').value = u.rule;

    // ── Summary ──────────────────────────────────────────────────────
    function drawSummary(s) {
        var run = s.running ? 'Detection is running now' : s.lastRun ? 'Last checked ' + s.lastRun : 'Not checked yet';
        FA.qs('wKpis').innerHTML =
            kpi('Open critical', s.critical, s.critical ? 'fa-kpi--warn' : '', 'Needs action first') +
            kpi('Open high', s.high, '', s.medium + ' medium, ' + s.info + ' information') +
            kpi('Acknowledged', s.acknowledged, '', 'Accepted with a reason') +
            kpi('Fixed this month', s.fixedThisMonth, '', 'No longer found') +
            kpi('Oldest open', s.oldest || 'None', '', 'First detected') +
            '<div class="fa-kpi"><div class="fa-kpi__label">Checks</div><div class="fa-kpi__sub" style="margin-top:6px">' + FA.esc(run) + '. Every ' + s.interval + ' hours automatically.</div>' +
            (can ? '<button type="button" class="fa-btn fa-btn--primary fa-btn--sm" id="wRun" style="margin-top:8px">' + GL.icon('refresh', 12) + ' Run the checks now</button>' : '') + '</div>';
        if (FA.qs('wRun')) FA.qs('wRun').onclick = runNow;
        FA.qs('wHealth').innerHTML = s.health < 0 ? '<div class="fa-muted">The checks have not run yet.</div>' :
            '<div class="gl-health">' + GL.ring(s.health) + '<div><div class="gl-health__num">' + s.health + '<span style="font-size:13px;color:var(--fa-muted)"> / 100</span></div>' +
            '<div class="gl-health__sub">100 less 15 for each critical rule, 6 for each high and 2 for each medium rule with an open warning. Acknowledged warnings do not count.</div></div></div>';
        FA.qs('wLost').innerHTML = !s.lost.length ? '<div class="fa-empty">No points lost.</div>' :
            '<table class="fa-table"><tbody>' + s.lost.map(function (l) {
                return '<tr class="is-click" data-rule="' + l.rule + '"><td>' + GL.sev(l.severity) + '</td><td><b>' + l.rule + '</b> ' + FA.esc(l.title) + '<span class="fa-sub">' + l.open + ' open, ' + FA.esc(l.amount) + '</span></td><td class="fa-num"><b>-' + l.points + '</b></td></tr>';
            }).join('') + '</tbody></table>';
        Array.prototype.forEach.call(FA.qs('wLost').querySelectorAll('[data-rule]'), function (tr) { tr.onclick = function () { FA.qs('wQ').value = tr.getAttribute('data-rule'); st.tab = 'OPEN'; drawTabs(); load(); }; });
    }
    function kpi(label, val, cls, sub) { return '<div class="fa-kpi ' + cls + '"><div class="fa-kpi__label">' + label + '</div><div class="fa-kpi__value">' + FA.esc(String(val)) + '</div><div class="fa-kpi__sub">' + FA.esc(sub) + '</div></div>'; }

    function runNow() {
        var b = FA.qs('wRun'); FA.busy(b, true, 'Checking the ledger');
        FA.call(PAGE, 'RunDetectionNow', {}, function (r) { FA.busy(b, false); FA.toast(r.message, !r.success); if (r.success) load(); });
    }

    FA.qs('wRules').innerHTML = (B.rules || []).map(function (r) {
        return '<div style="padding:6px 0;border-bottom:1px solid var(--fa-border)"><div class="fa-row">' + GL.sev(r.severity) + '<b>' + r.code + '</b> ' + FA.esc(r.title) + '</div><div class="fa-hint" style="margin-top:3px">' + FA.esc(r.detection) + '</div></div>';
    }).join('');

    // ── Tabs and list ────────────────────────────────────────────────
    var TABS = [['OPEN', 'Open'], ['ACKNOWLEDGED', 'Acknowledged'], ['FIXED', 'Fixed'], ['HISTORY', 'History']];
    function drawTabs() {
        FA.qs('wTabs').innerHTML = TABS.map(function (t) { return '<button type="button" class="fa-subtab' + (st.tab === t[0] ? ' is-active' : '') + '" data-t="' + t[0] + '">' + t[1] + '</button>'; }).join('');
        Array.prototype.forEach.call(FA.qs('wTabs').querySelectorAll('[data-t]'), function (b) { b.onclick = function () { st.tab = b.getAttribute('data-t'); FA.writeUrl({ tab: st.tab }); drawTabs(); load(); }; });
        FA.qs('wFilters').style.display = st.tab === 'HISTORY' ? 'none' : '';
    }

    function load() {
        if (st.tab === 'HISTORY') return history();
        FA.qs('wList').innerHTML = '<div class="fa-loading">Loading</div>';
        FA.call(PAGE, 'LoadWarnings', { status: st.tab }, function (r) {
            if (!r.success) { FA.qs('wList').innerHTML = '<div class="fa-empty">' + FA.esc(r.message) + '</div>'; return; }
            st.rows = r.rows; drawSummary(r.summary); list();
        });
    }

    function filtered() {
        var q = FA.val('wQ').toLowerCase(), sev = FA.val('wSev'), mine = FA.val('wMine');
        return st.rows.filter(function (w) {
            if (sev && w.severity !== sev) return false;
            if (mine === 'me' && w.assigned !== me) return false;
            if (mine === 'none' && w.assigned) return false;
            if (q && (w.rule + ' ' + w.title + ' ' + w.scope + ' ' + w.cause + ' ' + (rules[w.rule] || {}).title).toLowerCase().indexOf(q) < 0) return false;
            return true;
        });
    }

    function list() {
        var rows = filtered();
        if (!rows.length) { FA.qs('wList').innerHTML = '<div class="fa-card"><div class="fa-empty">' + (st.rows.length ? 'No warning matches the filters.' : st.tab === 'OPEN' ? 'No open warnings.' : 'Nothing here.') + '</div></div>'; return; }
        FA.qs('wList').innerHTML = rows.map(function (w) {
            var rl = rules[w.rule] || {};
            var meta = 'First found ' + w.first + (st.tab === 'FIXED' ? ', fixed ' + w.fixedAt : ', last seen ' + w.last) + (w.assigned ? '. Assigned to ' + w.assigned : '') + (w.status === 'ACKNOWLEDGED' ? '. Acknowledged by ' + w.ackBy + ' on ' + w.ackAt : '');
            return '<div class="gl-warn gl-warn--' + w.severity + (st.open === w.id ? ' is-open' : '') + '" id="w' + w.id + '"><div class="gl-warn__head" data-open="' + w.id + '">' +
                '<div>' + GL.sev(w.severity) + '</div><div class="gl-warn__main"><div class="gl-warn__title">' + FA.esc(w.title) + '</div>' +
                '<div class="gl-warn__meta"><b>' + w.rule + '</b> ' + FA.esc(rl.title || '') + (w.status === 'REAPPEARED' ? ' &middot; ' + GL.status(w.status) : '') + ' &middot; ' + FA.esc(meta) + '</div></div>' +
                '<div class="gl-warn__amt"><b>' + FA.esc(w.amount) + '</b><small>' + FA.plural(w.count, 'record', 'records') + '</small></div></div>' +
                '<div class="gl-warn__body" data-body="' + w.id + '"></div></div>';
        }).join('');
        Array.prototype.forEach.call(FA.qs('wList').querySelectorAll('[data-open]'), function (h) {
            h.onclick = function () { var id = parseInt(h.getAttribute('data-open'), 10), el = FA.qs('w' + id); var open = !el.classList.contains('is-open'); el.classList.toggle('is-open'); if (open) detail(id); };
        });
        if (st.open && FA.qs('w' + st.open)) { detail(st.open); FA.qs('w' + st.open).scrollIntoView(); st.open = 0; }
    }
    ['wQ', 'wSev', 'wMine'].forEach(function (id) { FA.qs(id).addEventListener(id === 'wQ' ? 'input' : 'change', FA.debounce(list, 200)); });

    function find(id) { for (var i = 0; i < st.rows.length; i++) if (st.rows[i].id === id) return st.rows[i]; return null; }

    // ── One warning ──────────────────────────────────────────────────
    function detail(id) {
        var w = find(id), rl = rules[w.rule] || {}, body = document.querySelector('[data-body="' + id + '"]');
        body.innerHTML = '<div class="fa-loading">Loading</div>';
        FA.call(PAGE, 'LoadWarningDetail', { id: id }, function (r) {
            if (!r.success) { body.innerHTML = '<div class="fa-error">' + FA.esc(r.message) + '</div>'; return; }
            var h = '<div class="fa-grid-2"><div><div class="fa-section">What was found</div><div style="line-height:1.6">' + FA.esc(w.cause || '') + '</div>' +
                (w.where ? '<div class="fa-hint" style="margin-top:6px">Where: ' + FA.esc(w.where) + '</div>' : '') +
                '<div class="fa-section">Likely cause</div><div style="line-height:1.6">' + FA.esc(rl.cause || '') + '</div>' +
                (w.ackReason ? '<div class="fa-notice fa-notice--warn" style="margin-top:10px">Acknowledged: ' + FA.esc(w.ackReason) + '</div>' : '') + '</div>' +
                '<div><div class="fa-section">Trend</div>' + (r.trend.length > 1 ? GL.spark(r.trend.map(function (t) { return t.count; })) + '<div class="fa-hint">' + FA.plural(r.trend[0].count, 'record', 'records') + ' on ' + FA.esc(r.trend[0].when) + ', ' + FA.plural(r.trend[r.trend.length - 1].count, 'record', 'records') + ' now</div>' : '<div class="fa-hint">Shown after the next run.</div>') +
                '<div class="fa-section">History</div><ul class="gl-events">' + r.events.slice(0, 8).map(function (e) { return '<li><b>' + FA.esc(e.when) + '</b> ' + FA.esc(e.type.charAt(0) + e.type.slice(1).toLowerCase()) + ' by ' + FA.esc(e.actor) + (e.detail ? ': ' + FA.esc(e.detail) : '') + '</li>'; }).join('') + '</ul></div></div>';
            if (r.sample && r.sample.length) {
                h += '<div class="fa-section">Largest records</div><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Reference</th><th>Date</th><th>Detail</th><th class="fa-num">Amount</th></tr></thead><tbody>' +
                    r.sample.slice(0, 10).map(function (s) { return '<tr' + (s.link ? ' class="is-click" data-l="' + FA.esc(s.link) + '"' : '') + '><td>' + FA.esc(s.reference) + '<span class="fa-sub">' + FA.esc(s.account || '') + '</span></td><td class="gl-nowrap">' + FA.esc(GL.date(s.date)) + '</td><td>' + FA.esc(s.text || '') + '</td><td class="fa-num">' + GL.money(s.amount) + '</td></tr>'; }).join('') + '</tbody></table></div>';
            }
            h += '<div class="gl-warn__actions"><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-a="records">' + FA.icon('list') + ' View all records</button>';
            (rl.fixes || []).forEach(function (f) { h += '<a class="fa-btn fa-btn--secondary fa-btn--sm" href="' + FA.esc(GL.fixUrl(f)) + '">' + (f.kind === 'wizard' ? FA.icon('edit') : GL.icon('ext', 12)) + ' ' + FA.esc(f.label) + '</a>'; });
            if (can) {
                if (w.status === 'OPEN' || w.status === 'REAPPEARED') h += '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-a="ack">' + GL.icon('check', 12) + ' Acknowledge</button>';
                if (w.status === 'ACKNOWLEDGED') h += '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-a="reopen">' + FA.icon('undo') + ' Reopen</button>';
                if (w.status !== 'FIXED') h += '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-a="assign">' + GL.icon('user', 12) + ' Assign</button>';
                h += '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-a="note">' + GL.icon('note', 12) + ' Add a note</button>';
            }
            h += '</div>';
            body.innerHTML = h;
            Array.prototype.forEach.call(body.querySelectorAll('tr[data-l]'), function (tr) { tr.onclick = function () { location.href = tr.getAttribute('data-l'); }; });
            Array.prototype.forEach.call(body.querySelectorAll('[data-a]'), function (b) { b.onclick = function () { act(b.getAttribute('data-a'), w); }; });
        });
    }

    function act(a, w) {
        if (a === 'records') return records(w);
        if (a === 'ack') return FA.reason({ title: 'Acknowledge ' + w.rule, sub: w.title, min: 15, label: 'Why is this accepted for now?', ok: 'Acknowledge',
            message: 'Nothing in the ledger changes. The warning leaves the open list and the health score with your reason on record. It comes back on its own if it grows.' },
            function (reason, m, done) { FA.call(PAGE, 'SaveAcknowledgement', { id: w.id, reason: reason }, function (r) { if (!r.success) return done(r.message); done(); FA.toast(r.message); load(); }); });
        if (a === 'reopen') return FA.reason({ title: 'Reopen ' + w.rule, sub: w.title, min: 10, label: 'Reason', ok: 'Reopen' },
            function (reason, m, done) { FA.call(PAGE, 'ReopenWarning', { id: w.id, reason: reason }, function (r) { if (!r.success) return done(r.message); done(); FA.toast(r.message); load(); }); });
        if (a === 'note') return FA.reason({ title: 'Add a note', sub: w.title, min: 5, label: 'Note', ok: 'Add note' },
            function (text, m, done) { FA.call(PAGE, 'AddWarningNote', { id: w.id, text: text }, function (r) { if (!r.success) return done(r.message); done(); FA.toast(r.message); detail(w.id); }); });
        if (a === 'assign') {
            var m = FA.modal({ title: 'Assign ' + w.rule, sub: w.title,
                body: '<div class="fa-field"><label class="fa-label" for="asUser">Person</label><select class="fa-select" id="asUser"><option value="">Nobody</option>' + (B.users || []).map(function (x) { return '<option value="' + FA.esc(x.id) + '"' + (x.id === w.assigned ? ' selected' : '') + '>' + FA.esc(x.name) + '</option>'; }).join('') + '</select></div>' +
                    '<div class="fa-field" style="margin-top:10px"><label class="fa-label" for="asNote">Note</label><input class="fa-input" id="asNote" maxlength="300"/></div><div class="fa-error" id="asErr" style="margin-top:8px"></div>',
                foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Save</button>' });
            m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
            m.foot.querySelector('[data-ok]').onclick = function () {
                FA.call(PAGE, 'AssignWarning', { id: w.id, user: FA.val('asUser'), note: FA.val('asNote') }, function (r) { if (!r.success) { FA.qs('asErr').textContent = r.message; return; } m.close(); FA.toast(r.message); load(); });
            };
        }
    }

    function records(w) {
        var rs = { page: 1, size: 25, search: '' };
        var m = FA.modal({ title: w.rule + ' records', sub: w.title, size: 'wide', body: '<div class="fa-row" style="margin-bottom:10px"><input type="search" class="fa-input" id="rcQ" placeholder="Search the records" style="max-width:300px"/><span class="fa-spacer"></span><span class="fa-muted" id="rcCount"></span></div><div id="rcBody"><div class="fa-loading">Finding the records now</div></div><div id="rcPager" style="margin-top:10px"></div>' });
        function go() {
            FA.call(PAGE, 'LoadRecords', { id: w.id, page: rs.page, size: rs.size, search: rs.search }, function (r) {
                if (!r.success) { FA.qs('rcBody').innerHTML = '<div class="fa-error">' + FA.esc(r.message) + '</div>'; return; }
                FA.qs('rcCount').textContent = FA.plural(r.total, 'record', 'records') + ', found again just now';
                if (!r.rows.length) { FA.qs('rcBody').innerHTML = '<div class="fa-empty">' + FA.esc(r.note || 'No records.') + '</div>'; FA.qs('rcPager').innerHTML = ''; return; }
                FA.qs('rcBody').innerHTML = '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Reference</th><th>Date</th><th>Account or kind</th><th>Detail</th><th class="fa-num">Amount</th>' + (r.recordAck && can ? '<th></th>' : '') + '</tr></thead><tbody>' +
                    r.rows.map(function (x, i) {
                        return '<tr><td>' + (x.link ? '<a class="gl-link" href="' + FA.esc(x.link) + '">' + FA.esc(x.reference) + '</a>' : FA.esc(x.reference)) + '</td><td class="gl-nowrap">' + FA.esc(GL.date(x.date)) + '</td><td>' + FA.esc(x.account || '') + '</td><td>' + FA.esc(x.text || '') + '</td><td class="fa-num">' + FA.esc(x.amount) + '</td>' +
                            (r.recordAck && can ? '<td>' + (x.ackId ? '<button type="button" class="fa-btn fa-btn--link" data-un="' + x.ackId + '">Withdraw acceptance</button>' : '<button type="button" class="fa-btn fa-btn--link" data-ok="' + i + '">Accept</button>') + '</td>' : '') + '</tr>';
                    }).join('') + '</tbody></table></div>';
                FA.pager(FA.qs('rcPager'), { page: r.page, size: r.size, total: r.total }, function (p) { rs.page = p; go(); });
                Array.prototype.forEach.call(FA.qs('rcBody').querySelectorAll('[data-ok]'), function (b) {
                    b.onclick = function () {
                        var x = r.rows[parseInt(b.getAttribute('data-ok'), 10)];
                        FA.reason({ title: 'Accept ' + x.reference, sub: w.rule + ' ' + (rules[w.rule] || {}).title, min: 15, label: 'Why is this record acceptable?', ok: 'Accept',
                            message: 'The ledger is not changed. This one record is left out of ' + w.rule + ' from the next run, with your reason on record.' },
                            function (reason, mm, done) { FA.call(PAGE, 'SaveRecordAcceptance', { id: w.id, key: x.key, reason: reason, amount: x.amount }, function (q) { if (!q.success) return done(q.message); done(); FA.toast(q.message); go(); }); });
                    };
                });
                Array.prototype.forEach.call(FA.qs('rcBody').querySelectorAll('[data-un]'), function (b) {
                    b.onclick = function () {
                        FA.reason({ title: 'Withdraw the acceptance', min: 10, label: 'Reason', ok: 'Withdraw', danger: true },
                            function (reason, mm, done) { FA.call(PAGE, 'RemoveRecordAcceptance', { ackId: parseInt(b.getAttribute('data-un'), 10), reason: reason }, function (q) { if (!q.success) return done(q.message); done(); FA.toast(q.message); go(); }); });
                    };
                });
            });
        }
        FA.qs('rcQ').addEventListener('input', FA.debounce(function () { rs.search = FA.val('rcQ'); rs.page = 1; go(); }, 350));
        go();
    }

    // ── History ──────────────────────────────────────────────────────
    function history() {
        FA.qs('wList').innerHTML = '<div class="fa-loading">Loading</div>';
        FA.call(PAGE, 'LoadHistory', {}, function (r) {
            if (!r.success) { FA.qs('wList').innerHTML = '<div class="fa-empty">' + FA.esc(r.message) + '</div>'; return; }
            var runs = r.runs.slice().reverse();
            var h = '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Open warnings and health over the last ' + runs.length + ' runs</div></div><div class="fa-card__body fa-grid-2">' +
                '<div><div class="fa-label">Open warnings</div>' + GL.spark(runs.map(function (x) { return x.open; }), '#b42318') + '</div>' +
                '<div><div class="fa-label">Health score</div>' + GL.spark(runs.map(function (x) { return x.health || 0; }), '#166534') + '</div></div></div>';
            h += '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Is each problem falling?</div><span class="fa-hint">Records found by each rule, last 30 runs</span></div><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Rule</th><th style="width:200px">Records</th><th class="fa-num">Now</th><th class="fa-num">Amount now</th></tr></thead><tbody>' +
                r.byRule.map(function (x) { return '<tr><td><b>' + x.rule + '</b> ' + FA.esc(x.title) + '</td><td>' + (x.counts.length > 1 ? GL.spark(x.counts) : '') + '</td><td class="fa-num">' + x.lastCount + '</td><td class="fa-num">' + x.last + '</td></tr>'; }).join('') + '</tbody></table></div></div>';
            h += '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Detection runs</div></div><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Run</th><th>Started</th><th>By</th><th>How</th><th class="fa-num">Seconds</th><th class="fa-num">Rules</th><th class="fa-num">Open</th><th class="fa-num">Critical</th><th class="fa-num">High</th><th class="fa-num">Health</th><th>Errors</th></tr></thead><tbody>' +
                r.runs.map(function (x) { return '<tr><td>' + x.id + '</td><td class="gl-nowrap">' + FA.esc(x.started) + '</td><td>' + FA.esc(x.by) + '</td><td>' + FA.esc({ MANUAL: 'On request', AUTO: 'Page opened', SCHEDULE: 'Scheduled' }[x.trigger] || x.trigger) + '</td><td class="fa-num">' + x.seconds + '</td><td class="fa-num">' + x.rules + '</td><td class="fa-num">' + x.open + '</td><td class="fa-num">' + x.critical + '</td><td class="fa-num">' + x.high + '</td><td class="fa-num">' + (x.health === null ? '' : x.health) + '</td><td>' + FA.esc(x.errors ? x.errors.split('\n').length + ' rule(s) failed' : '') + '</td></tr>'; }).join('') + '</tbody></table></div></div>';
            FA.qs('wList').innerHTML = h;
        });
    }

    drawSummary(B.summary || { lost: [], health: -1 });
    drawTabs(); load();
})();
