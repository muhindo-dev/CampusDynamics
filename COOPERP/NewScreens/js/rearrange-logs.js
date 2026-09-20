/* Student Course Rearrangement — the log.
   Reverse is offered from here, and only here. The before/after is rendered as a
   field-by-field comparison with the changed rows highlighted; a raw JSON dump
   is not something anyone can check a student's record against. */
(function () {
'use strict';

var PAGE = location.pathname;
var PAGE_NO = 1, PAGE_SIZE = 50;
var CURRENT = null;

function qs(id) { return document.getElementById(id); }
function esc(s) {
    return s === null || s === undefined ? '' : String(s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
function call(method, params, cb) {
    var x = new XMLHttpRequest();
    x.open('POST', PAGE + '/' + method, true);
    x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
    x.timeout = 290000;
    x.ontimeout = function () { cb({ success: false, message: 'The request timed out.' }); };
    x.onload = function () {
        try { var o = JSON.parse(x.responseText); cb(typeof o.d === 'string' ? JSON.parse(o.d) : o.d); }
        catch (e) { cb({ success: false, message: 'Your session may have expired — reload the page.' }); }
    };
    x.onerror = function () { cb({ success: false, message: 'Network error.' }); };
    x.send(JSON.stringify(params || {}));
}
function toast(m, err) {
    var t = document.createElement('div');
    t.className = 'rx-toast' + (err ? ' rx-toast--err' : '');
    t.textContent = m; document.body.appendChild(t);
    setTimeout(function () { t.style.transition = 'opacity .4s'; t.style.opacity = '0';
        setTimeout(function () { if (t.parentNode) t.parentNode.removeChild(t); }, 400); }, err ? 7000 : 3200);
}
function openModal(id) { qs(id).classList.add('is-open'); }
function closeModal(id) { qs(id).classList.remove('is-open'); }
document.addEventListener('click', function (e) {
    var c = e.target.getAttribute && e.target.getAttribute('data-close');
    if (c) closeModal(c);
});

function filters() {
    return {
        from: qs('f-from').value, to: qs('f-to').value, actor: qs('f-actor').value,
        role: qs('f-role').value, regno: qs('f-regno').value.trim(), opType: qs('f-op').value,
        reversed: qs('f-rev').value, q: qs('f-q').value.trim()
    };
}

function load() {
    var f = filters();
    qs('rx-body').innerHTML = '<tr><td colspan="9" class="rx-state"><div class="rx-spin"></div>Loading…</td></tr>';
    f.page = PAGE_NO; f.pageSize = PAGE_SIZE;
    call('Logs', f, function (d) {
        if (!d || !d.success) {
            qs('rx-body').innerHTML = '<tr><td colspan="9" class="rx-state rx-state--err">' +
                '<div class="rx-state__t">Could not load the log</div>' + esc(d && d.message || '') + '</td></tr>';
            return;
        }
        qs('rx-count').textContent = d.total.toLocaleString() + ' log entr' + (d.total === 1 ? 'y' : 'ies');
        if (!d.rows.length) {
            qs('rx-body').innerHTML = '<tr><td colspan="9" class="rx-state">' +
                '<div class="rx-state__t">Nothing matches these filters</div>' +
                'Widen the date range or clear a filter.</td></tr>';
            qs('rx-pager').innerHTML = ''; return;
        }
        var h = '';
        for (var i = 0; i < d.rows.length; i++) {
            var r = d.rows[i];
            h += '<tr class="rx-tbl__click" data-id="' + r.id + '">' +
                 '<td>' + r.id + '<div class="rx-sub2">' + esc(r.sref) + '</div></td>' +
                 '<td>' + esc(r.at) + '</td>' +
                 '<td>' + esc(r.regno) + '</td>' +
                 '<td><span class="rx-badge rx-badge--' + esc(r.opType) + '">' + esc(r.opType.replace(/_/g, ' ')) + '</span>' +
                    (r.isOverride ? ' <span class="rx-badge rx-badge--ovr">override</span>' : '') + '</td>' +
                 '<td>' + esc(r.course || '—') + '<div class="rx-sub2">' + esc(r.table) + ' #' + esc(r.pk) + '</div></td>' +
                 '<td>' + esc(r.actorName || r.actor) + '<div class="rx-sub2">' + esc(r.role) + ' · ' + esc(r.ip) + '</div></td>' +
                 '<td style="max-width:280px">' + esc(r.opReason || r.sessionReason || '—') +
                    (r.overrideReason ? '<div class="rx-sub2">override: ' + esc(r.overrideReason) + '</div>' : '') + '</td>' +
                 '<td>' + (r.isReversed ? '<span class="rx-badge rx-badge--rev">reversed</span>'
                          : (r.reversesLogId ? '<span class="rx-badge rx-badge--REVERSAL">undoes #' + r.reversesLogId + '</span>' : '')) + '</td>' +
                 '<td><span style="color:#174DA4;font-weight:600">View ›</span></td></tr>';
        }
        qs('rx-body').innerHTML = h;
        var rows = document.querySelectorAll('.rx-tbl__click');
        for (var j = 0; j < rows.length; j++)
            rows[j].addEventListener('click', function () { detail(+this.getAttribute('data-id')); });

        qs('rx-pager').innerHTML =
            '<span style="font-size:11px;color:#64748b">Page ' + d.page + ' of ' + d.pages + '</span>' +
            '<span style="margin-left:auto"></span>' +
            '<button type="button" class="rx-btn rx-btn--ghost rx-btn--sm" id="pg-prev"' + (d.page <= 1 ? ' disabled="disabled"' : '') + '>&laquo; Prev</button>' +
            '<button type="button" class="rx-btn rx-btn--ghost rx-btn--sm" id="pg-next"' + (d.page >= d.pages ? ' disabled="disabled"' : '') + '>Next &raquo;</button>';
        if (qs('pg-prev')) qs('pg-prev').addEventListener('click', function () { PAGE_NO--; load(); });
        if (qs('pg-next')) qs('pg-next').addEventListener('click', function () { PAGE_NO++; load(); });
        updateCsv();
    });
}

function updateCsv() {
    var f = filters(), parts = ['export=csv'];
    for (var k in f) if (f.hasOwnProperty(k) && f[k]) parts.push(encodeURIComponent(k) + '=' + encodeURIComponent(f[k]));
    qs('f-csv').href = location.pathname + '?' + parts.join('&');
}

function detail(id) {
    CURRENT = null;
    qs('rx-detail-body').innerHTML = '<div class="rx-state"><div class="rx-spin"></div>Loading…</div>';
    qs('rx-rev-one').style.display = 'none';
    qs('rx-rev-batch').style.display = 'none';
    openModal('rx-detail-modal');
    call('LogDetail', { id: id }, function (d) {
        if (!d || !d.success) {
            qs('rx-detail-body').innerHTML = '<div class="rx-err">' + esc(d && d.message || 'Not found') + '</div>'; return;
        }
        CURRENT = d.entry;
        var e = d.entry;
        var h = '<table class="rx-diff" style="margin-bottom:14px"><tbody>' +
            row2('Entry', '#' + e.id + ' &middot; session ' + esc(e.sref)) +
            row2('When', esc(e.at)) +
            row2('Student', esc(e.regno)) +
            row2('Operation', '<span class="rx-badge rx-badge--' + esc(e.opType) + '">' + esc(e.opType.replace(/_/g, ' ')) + '</span>' +
                 (e.isOverride ? ' <span class="rx-badge rx-badge--ovr">override: ' + esc(e.overrideKind || '') + '</span>' : '')) +
            row2('Record', esc(e.table) + ' #' + esc(e.pk) + (e.course ? ' &middot; ' + esc(e.course) : '')) +
            row2('Actor', esc(e.actorName || e.actor) + ' (' + esc(e.role) + ') &middot; ' + esc(e.ip)) +
            row2('Session reason', esc(e.sessionReason || '—')) +
            (e.opReason ? row2('Operation reason', esc(e.opReason)) : '') +
            (e.overrideReason ? row2('Override reason', '<b style="color:#b45309">' + esc(e.overrideReason) + '</b>') : '') +
            (e.lockStatus ? row2('Results status at the time', esc(e.lockStatus)) : '') +
            '</tbody></table>';

        if (e.isReversed)
            h += '<div class="rx-warn">This change has already been reversed. Reverse the reversal if you want it back.</div>';

        var changed = d.fields.filter(function (f) { return f.changed; }).length;
        h += '<div class="rx-review__gh">Before and after' +
             (changed ? ' — ' + changed + ' field' + (changed === 1 ? '' : 's') + ' changed' : '') + '</div>';
        h += '<table class="rx-diff"><thead><tr><th>Field</th><th>Before</th><th>After</th></tr></thead><tbody>';
        for (var i = 0; i < d.fields.length; i++) {
            var f = d.fields[i];
            h += '<tr class="' + (f.changed ? 'is-changed' : '') + '">' +
                 '<td class="rx-diff__f">' + esc(f.field) + '</td>' +
                 '<td class="rx-diff__b">' + (f.before === '' ? '<span style="color:#cbd5e1">—</span>' : esc(f.before)) + '</td>' +
                 '<td class="rx-diff__a">' + (f.after === '' ? '<span style="color:#cbd5e1">—</span>' : esc(f.after)) + '</td></tr>';
        }
        h += '</tbody></table>';
        qs('rx-detail-body').innerHTML = h;

        if (!e.isReversed && e.opType !== 'RECALC') {
            qs('rx-rev-one').style.display = '';
            qs('rx-rev-batch').style.display = '';
        }
    });
}
function row2(k, v) { return '<tr><td class="rx-diff__f" style="width:190px">' + k + '</td><td colspan="2">' + v + '</td></tr>'; }

/* ── reversal ─────────────────────────────────────────────────────────── */
var revMode = 'one';
function askReverse(mode) {
    if (!CURRENT) return;
    revMode = mode;
    qs('rx-reason-title').textContent = mode === 'one' ? 'Reverse this change' : 'Reverse the whole session';
    qs('rx-reason-context').innerHTML =
        '<div class="rx-warn">' +
        (mode === 'one'
            ? 'Entry <b>#' + CURRENT.id + '</b> will be restored to its state before the change.'
            : 'Every change saved in the same batch as entry <b>#' + CURRENT.id + '</b> will be reversed, ' +
              'newest first, in one transaction.') +
        '<br /><br />If the record has been altered since, the reversal is <b>refused</b> rather than overwriting that newer work. ' +
        'The reversal is itself logged and can be reversed in turn. GPA and CGPA are recalculated afterwards.</div>';
    qs('rx-reason-text').value = '';
    qs('rx-reason-hint2').className = 'rx-hint';
    qs('rx-reason-hint2').textContent = 'At least 10 characters.';
    qs('rx-reason-ok').disabled = true;
    openModal('rx-reason-modal');
    setTimeout(function () { qs('rx-reason-text').focus(); }, 60);
}
qs('rx-rev-one').addEventListener('click', function () { askReverse('one'); });
qs('rx-rev-batch').addEventListener('click', function () { askReverse('batch'); });
qs('rx-reason-text').addEventListener('input', function () {
    var v = this.value.trim(), h = qs('rx-reason-hint2');
    qs('rx-reason-ok').disabled = v.length < 10;
    if (v.length === 0) { h.className = 'rx-hint'; h.textContent = 'At least 10 characters.'; }
    else if (v.length < 10) { h.className = 'rx-hint rx-hint--bad'; h.textContent = (10 - v.length) + ' more character(s).'; }
    else { h.className = 'rx-hint rx-hint--ok'; h.textContent = 'Reason accepted.'; }
});
qs('rx-reason-ok').addEventListener('click', function () {
    var reason = qs('rx-reason-text').value.trim();
    if (reason.length < 10 || !CURRENT) return;
    var btn = this; btn.disabled = true; btn.textContent = 'Reversing…';
    var method = revMode === 'one' ? 'ReverseEntry' : 'ReverseBatch';
    var args = revMode === 'one' ? { logId: CURRENT.id, reason: reason } : { batchId: CURRENT.batchId, reason: reason };
    call(method, args, function (d) {
        btn.disabled = false; btn.textContent = 'Reverse';
        if (!d || !d.success) { toast(d && d.message || 'The reversal failed.', true); return; }
        closeModal('rx-reason-modal'); closeModal('rx-detail-modal');
        var m = d.message;
        if (d.recalculated && d.recalculated.cgpaBefore !== d.recalculated.cgpaAfter)
            m += ' CGPA ' + d.recalculated.cgpaBefore + ' → ' + d.recalculated.cgpaAfter + '.';
        toast(m);
        load();
    });
});

/* ── boot ─────────────────────────────────────────────────────────────── */
qs('f-apply').addEventListener('click', function () { PAGE_NO = 1; load(); });
qs('f-reset').addEventListener('click', function () {
    ['f-from', 'f-to', 'f-regno', 'f-q'].forEach(function (i) { qs(i).value = ''; });
    ['f-actor', 'f-role', 'f-op', 'f-rev'].forEach(function (i) { qs(i).value = ''; });
    PAGE_NO = 1; load();
});
qs('f-q').addEventListener('keydown', function (e) {
    if (e.key === 'Enter' || e.keyCode === 13) { e.preventDefault(); PAGE_NO = 1; load(); }
});

call('Filters', {}, function (d) {
    if (!d || !d.success) return;
    if (d.actor) {
        qs('rx-actor').textContent = d.actor.name || d.actor.user;
        qs('rx-role').textContent = d.actor.role || 'no role';
    }
    for (var i = 0; i < d.actors.length; i++) {
        var a = d.actors[i], o = document.createElement('option');
        o.value = a.user; o.textContent = (a.name || a.user);
        qs('f-actor').appendChild(o);
    }
    for (var j = 0; j < d.roles.length; j++) {
        var o2 = document.createElement('option'); o2.value = d.roles[j]; o2.textContent = d.roles[j];
        qs('f-role').appendChild(o2);
    }
    for (var k = 0; k < d.ops.length; k++) {
        var o3 = document.createElement('option'); o3.value = d.ops[k];
        o3.textContent = d.ops[k].replace(/_/g, ' '); qs('f-op').appendChild(o3);
    }
});
load();

})();
