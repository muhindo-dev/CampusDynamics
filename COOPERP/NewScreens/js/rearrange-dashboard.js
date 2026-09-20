/* Student Course Rearrangement — dashboard. Read-only; every row links into the log. */
(function () {
'use strict';
var PAGE = location.pathname;
function qs(id) { return document.getElementById(id); }
function esc(s) {
    return s === null || s === undefined ? '' : String(s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
function call(m, p, cb) {
    var x = new XMLHttpRequest();
    x.open('POST', PAGE + '/' + m, true);
    x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
    x.onload = function () {
        try { var o = JSON.parse(x.responseText); cb(typeof o.d === 'string' ? JSON.parse(o.d) : o.d); }
        catch (e) { cb({ success: false, message: 'Your session may have expired — reload the page.' }); }
    };
    x.onerror = function () { cb({ success: false, message: 'Network error.' }); };
    x.send(JSON.stringify(p || {}));
}
var LOGS = 'StudentRearrangeLogs.aspx';

function err(tb, cols, msg) {
    qs(tb).innerHTML = '<tr><td colspan="' + cols + '" class="rx-state rx-state--err">' +
        '<div class="rx-state__t">Could not load</div>' + esc(msg) + '</td></tr>';
}
function empty(tb, cols, msg) {
    qs(tb).innerHTML = '<tr><td colspan="' + cols + '" class="rx-state">' + esc(msg) + '</td></tr>';
}
function stat(v, l, extra) {
    return '<div class="rx-stat' + (extra || '') + '"><div class="rx-stat__v">' +
           (v || 0).toLocaleString() + '</div><div class="rx-stat__l">' + l + '</div></div>';
}

/* The period lives in the URL so a dashboard view can be bookmarked and reloaded. */
function readUrl() {
    try {
        var p = new URLSearchParams(location.search);
        var d = p.get('days');
        if (d && qs('rx-days').querySelector('option[value="' + d + '"]')) qs('rx-days').value = d;
    } catch (e) { }
}
function pushUrl(replace) {
    var url = location.pathname + '?days=' + encodeURIComponent(qs('rx-days').value);
    try {
        if (replace) history.replaceState(null, '', url);
        else history.pushState(null, '', url);
    } catch (e) { }
}

function load() {
    var days = qs('rx-days').value;
    qs('rx-stats').innerHTML = '<div class="rx-stat"><div class="rx-spin"></div></div>';
    call('Dashboard', { days: +days }, function (d) {
        if (!d || !d.success) {
            qs('rx-stats').innerHTML = '';
            ['rx-byop', 'rx-byactor', 'rx-recent', 'rx-sessions', 'rx-blocked'].forEach(function (t) {
                err(t, 7, (d && d.message) || 'Unknown error');
            });
            return;
        }
        if (d.actor) {
            qs('rx-actor').textContent = d.actor.name || d.actor.user;
            qs('rx-role').textContent = d.actor.role || 'no role';
        }

        var t = d.totals;
        qs('rx-stats').innerHTML =
            stat(t.ops, 'Changes') + stat(t.sessions, 'Sessions') +
            stat(t.overrides, 'Lock overrides', t.overrides > 0 ? ' rx-stat--warn' : '') +
            stat(t.reversals, 'Reversals', t.reversals > 0 ? ' rx-stat--warn' : '');

        if (!d.byOp.length) empty('rx-byop', 2, 'No changes in this period.');
        else qs('rx-byop').innerHTML = d.byOp.map(function (o) {
            return '<tr><td><span class="rx-badge rx-badge--' + esc(o.opType) + '">' +
                   esc(o.opType.replace(/_/g, ' ')) + '</span></td>' +
                   '<td style="text-align:right"><b>' + o.count + '</b></td></tr>';
        }).join('');

        if (!d.byActor.length) empty('rx-byactor', 3, 'No activity in this period.');
        else qs('rx-byactor').innerHTML = d.byActor.map(function (a) {
            return '<tr><td>' + esc(a.name || a.actor) + '<div class="rx-sub2">' + esc(a.actor) + '</div></td>' +
                   '<td>' + esc(a.role || '—') + '</td><td style="text-align:right"><b>' + a.count + '</b></td></tr>';
        }).join('');

        if (!d.recent.length) empty('rx-recent', 7, 'Nothing has been changed through this module yet.');
        else qs('rx-recent').innerHTML = d.recent.map(function (r) {
            return '<tr><td>' + esc(r.at) + '</td><td>' + esc(r.regno) + '</td>' +
                   '<td><span class="rx-badge rx-badge--' + esc(r.opType) + '">' + esc(r.opType.replace(/_/g, ' ')) + '</span>' +
                   (r.isOverride ? ' <span class="rx-badge rx-badge--ovr">override</span>' : '') + '</td>' +
                   '<td>' + esc(r.course || '—') + '</td><td>' + esc(r.actor) +
                   '<div class="rx-sub2">' + esc(r.role) + '</div></td>' +
                   '<td style="max-width:300px">' + esc(r.reason || '—') + '</td>' +
                   '<td><a href="' + LOGS + '?regno=' + encodeURIComponent(r.regno) +
                   '&entry=' + r.id + '" style="color:#174DA4;font-weight:600">Open ›</a></td></tr>';
        }).join('');

        if (!d.sessions.length) empty('rx-sessions', 7, 'No sessions opened yet.');
        else qs('rx-sessions').innerHTML = d.sessions.map(function (s) {
            return '<tr class="rx-tbl__click" onclick="location.href=' + JSON.stringify(
                       LOGS + '?regno=' + encodeURIComponent(s.regno)) .replace(/"/g, '&quot;') + '">' +
                   '<td>' + esc(s.sref) + '</td>' +
                   '<td>' + esc(s.regno) + '<div class="rx-sub2">' + esc(s.student) + '</div></td>' +
                   '<td>' + esc(s.actor) + '<div class="rx-sub2">' + esc(s.role) + '</div></td>' +
                   '<td>' + esc(s.at) + '</td>' +
                   '<td style="max-width:320px">' + esc(s.reason) + '</td>' +
                   '<td style="text-align:right">' + s.ops + '</td>' +
                   '<td>' + esc(s.status) + '</td></tr>';
        }).join('');

        if (!d.blocked.length) empty('rx-blocked', 6, 'No refused or blocked attempts recorded.');
        else qs('rx-blocked').innerHTML = d.blocked.map(function (b) {
            var cls = b.outcome === 'DENIED' ? 'rx-badge--DELETE'
                    : b.outcome === 'CONFLICT' ? 'rx-badge--MARK_CHANGE' : 'rx-badge--RECALC';
            return '<tr><td>' + esc(b.at) + '</td>' +
                   '<td>' + esc(b.actor || '—') + '<div class="rx-sub2">' + esc(b.role) + '</div></td>' +
                   '<td>' + (b.regno
                       ? '<a href="' + LOGS + '?regno=' + encodeURIComponent(b.regno) +
                         '" style="color:#174DA4;font-weight:600">' + esc(b.regno) + '</a>'
                       : '—') + '</td><td>' + esc(b.action) + '</td>' +
                   '<td><span class="rx-badge ' + cls + '">' + esc(b.outcome) + '</span></td>' +
                   '<td style="max-width:340px">' + esc(b.detail) + '</td></tr>';
        }).join('');
    });
}

qs('rx-days').addEventListener('change', function () { pushUrl(false); load(); });
window.addEventListener('popstate', function () { readUrl(); load(); });

readUrl();
pushUrl(true);
load();
})();
