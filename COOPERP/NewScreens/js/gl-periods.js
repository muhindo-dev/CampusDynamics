/* General Ledger: periods and close. */
(function () {
    'use strict';
    var FA = window.FA, GL = window.GL, B = window.GL_BOOT || {}, PAGE = 'AccountsPeriods.aspx';
    if (B.error) { FA.qs('pErr').innerHTML = '<div class="fa-notice fa-notice--bad">' + FA.esc(B.error) + '</div>'; return; }
    var R = B.rights || {}, st = { year: '', sel: '' };
    var ACT = { REVIEWED: ['Reviewed', 'info'], SIGNED_OFF: ['Signed off', 'ok'], REOPENED: ['Reopened', 'warn'] };
    function badge(a) { if (!a) return '<span class="fa-badge fa-badge--neutral">Not reviewed</span>'; var x = ACT[a]; return '<span class="fa-badge fa-badge--' + x[1] + '">' + x[0] + '</span>'; }

    var cur = (B.years || []).filter(function (y) { return y.current; })[0] || (B.years || [])[0];
    FA.qs('pYear').innerHTML = (B.years || []).map(function (y) { return '<option value="' + FA.esc(y.key) + '"' + (cur && y.key === cur.key ? ' selected' : '') + '>' + FA.esc(y.label + ' (' + y.dates + ')' + (y.status === 'Closed' ? ', marked Closed' : '')) + '</option>'; }).join('');
    FA.qs('pYear').onchange = function () { st.year = FA.qs('pYear').value; st.sel = ''; load(); };
    st.year = FA.qs('pYear').value;

    function load() {
        FA.qs('pList').innerHTML = '<div class="fa-loading">Loading</div>';
        FA.call(PAGE, 'LoadPeriods', { yearKey: st.year }, function (r) {
            if (!r.success) { FA.qs('pList').innerHTML = '<div class="fa-empty">' + FA.esc(r.message) + '</div>'; return; }
            var rows = [r.year].concat(r.months);
            FA.qs('pList').innerHTML = '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Period</th><th class="fa-num">Lines</th><th class="fa-num">Difference</th><th class="fa-num">Unbalanced vouchers</th><th>Review</th></tr></thead><tbody>' +
                rows.map(function (p, i) {
                    return '<tr class="is-click' + (i === 0 ? ' gl-s' : '') + (p.key === st.sel ? ' is-selected' : '') + '" data-k="' + FA.esc(p.key) + '"' + (p.started ? '' : ' style="opacity:.5"') + '><td>' + (i === 0 ? 'Whole year ' : '') + FA.esc(p.label) + '</td><td class="fa-num">' + p.lines + '</td>' +
                        '<td class="fa-num' + (p.balanced ? '' : ' gl-neg') + '">' + p.diff + '</td><td class="fa-num">' + FA.money(p.unbalanced) + '</td><td>' + badge(p.status) + (p.statusBy ? '<span class="fa-sub">' + FA.esc(p.statusBy + ', ' + p.statusWhen) + '</span>' : '') + '</td></tr>';
                }).join('') + '</tbody></table></div>';
            Array.prototype.forEach.call(FA.qs('pList').querySelectorAll('tr[data-k]'), function (tr) { tr.onclick = function () { st.sel = tr.getAttribute('data-k'); Array.prototype.forEach.call(FA.qs('pList').querySelectorAll('tr'), function (x) { x.classList.remove('is-selected'); }); tr.classList.add('is-selected'); panel(); }; });
            if (!st.sel) { st.sel = r.year.key; FA.qs('pList').querySelector('tr[data-k]').classList.add('is-selected'); }
            panel();
        });
    }

    function panel() {
        FA.qs('pPanel').innerHTML = '<div class="fa-card"><div class="fa-loading">Running the checklist</div></div>';
        FA.call(PAGE, 'LoadPeriodChecklist', { key: st.sel }, function (r) {
            if (!r.success) { FA.qs('pPanel').innerHTML = '<div class="fa-card"><div class="fa-empty">' + FA.esc(r.message) + '</div></div>'; return; }
            var fails = r.items.filter(function (i) { return i.status === 'fail'; }).length;
            var last = r.history.length ? r.history[0].action : '';
            var h = '<div class="fa-card"><div class="fa-card__head"><div><div class="fa-card__title">' + FA.esc(r.label) + '</div><div class="fa-hint">' + FA.esc(r.from + ' to ' + r.to) + '</div></div>' + badge(last) + '</div>' +
                '<div id="pChecks" style="padding:10px 14px 0"></div>';
            if (R.periodsManage) {
                h += '<div class="fa-card__foot" style="justify-content:flex-end;gap:8px">';
                h += '<button type="button" class="fa-btn fa-btn--secondary" data-a="REVIEWED">Record a review</button>';
                if (last === 'SIGNED_OFF') h += '<button type="button" class="fa-btn fa-btn--secondary" data-a="REOPENED">Reopen</button>';
                else if (r.ended) h += '<button type="button" class="fa-btn fa-btn--primary" data-a="SIGNED_OFF">Sign off</button>';
                h += '</div>';
            }
            h += '</div>';
            if (r.close) {
                h += '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Year-end closing entry</div></div><div class="fa-card__body">' +
                    '<p style="margin:0 0 10px;line-height:1.6">At ' + FA.esc(r.close.date) + ', ' + FA.plural(r.close.accounts, 'income or expense account holds', 'income and expense accounts hold') + ' a net <b>' + (r.close.surplus ? 'surplus' : 'deficit') + ' of ' + FA.esc(r.close.net) + '</b> that was never closed to retained earnings. ' +
                    'The closing entry moves every one of those balances to retained earnings. It is prepared as a draft adjusting entry for you to check; it is posted only when another approver approves it.</p>' +
                    (R.periodsManage && R.adjust && r.ended ? '<div class="fa-row"><select class="fa-select" id="pRe" style="max-width:340px">' + (B.equity || []).map(function (a) { return '<option value="' + FA.esc(a.code) + '"' + (/AC7008/.test(a.code) ? ' selected' : '') + '>' + FA.esc(a.name) + '</option>'; }).join('') + '</select>' +
                        '<button type="button" class="fa-btn fa-btn--primary" id="pClose">Prepare the closing entry</button></div>' : '<div class="fa-hint">' + (r.ended ? 'Preparing it needs the periods and adjusting-entry permissions.' : 'Available after the year ends.') + '</div>') + '</div></div>';
            }
            h += '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Review record</div></div><div class="fa-card__body">' +
                (r.history.length ? '<ul class="gl-events">' + r.history.map(function (x) { return '<li><b>' + FA.esc(x.when) + '</b> ' + FA.esc((ACT[x.action] || [x.action])[0]) + ' by ' + FA.esc(x.by) + (x.role ? ' (' + FA.esc(x.role) + ')' : '') + (x.fails ? ', with ' + x.fails + ' failing check' + (x.fails === 1 ? '' : 's') : ', all checks passing') + (x.note ? ': ' + FA.esc(x.note) : '') + '</li>'; }).join('') + '</ul>' : '<div class="fa-muted">No review recorded yet.</div>') + '</div></div>';
            FA.qs('pPanel').innerHTML = h;
            GL.checks(FA.qs('pChecks'), r.items.map(function (i) { return { code: i.code, title: i.title, status: i.status, amount: i.amount, count: i.count, cause: i.detail, fix: i.fix }; }));
            Array.prototype.forEach.call(FA.qs('pPanel').querySelectorAll('[data-a]'), function (b) {
                b.onclick = function () {
                    var a = b.getAttribute('data-a');
                    var min = a === 'REVIEWED' ? 0 : (a === 'SIGNED_OFF' && fails ? 30 : 10);
                    FA.reason({ title: { REVIEWED: 'Record a review', SIGNED_OFF: 'Sign off ' + r.label, REOPENED: 'Reopen ' + r.label }[a], min: min, label: 'Note', ok: { REVIEWED: 'Record', SIGNED_OFF: 'Sign off', REOPENED: 'Reopen' }[a],
                        warn: a === 'SIGNED_OFF' && fails > 0,
                        message: a === 'SIGNED_OFF' ? (fails ? fails + ' check' + (fails === 1 ? '' : 's') + ' fail. You can still sign off, but explain why the figures can be accepted; the checklist as it stands now is kept with your sign-off.' : 'Every check passes. The checklist as it stands now is kept with your sign-off.') : a === 'REOPENED' ? 'The period stays open for review again. The earlier sign-off remains on record.' : 'The checklist as it stands now is kept with your review.' },
                        function (note, m, done) { FA.call(PAGE, 'SavePeriodAction', { key: r.key, action: a, note: note }, function (q) { if (!q.success) return done(q.message); done(); FA.toast(q.message); load(); }); });
                };
            });
            var pc = FA.qs('pClose');
            if (pc) pc.onclick = function () {
                FA.confirm({ title: 'Prepare the closing entry', message: 'A draft adjusting entry dated ' + r.close.date + ' will be prepared with one line per income and expense account and the balance to ' + FA.qs('pRe').value + '. Nothing is posted.', ok: 'Prepare draft' }, function () {
                    FA.busy(pc, true, 'Preparing');
                    FA.call(PAGE, 'CreateYearEndDraft', { key: r.key, retained: FA.qs('pRe').value }, function (q) { FA.busy(pc, false); if (!q.success) { FA.toast(q.message, true); return; } FA.toast(q.message); setTimeout(function () { location.href = 'AccountsAdjustments.aspx?id=' + q.id; }, 900); });
                });
            };
        });
    }

    load();
})();
