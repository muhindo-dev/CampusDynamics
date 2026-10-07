/* General Ledger: voucher page. */
(function () {
    'use strict';
    var FA = window.FA, GL = window.GL, B = window.GL_BOOT || {}, PAGE = 'AccountsVoucher.aspx';
    FA.qs('vGo').onclick = function () { var n = parseInt(FA.val('vNo'), 10); if (n > 0) location.href = PAGE + '?v=' + n; };
    FA.qs('vNo').addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); FA.qs('vGo').click(); } });
    if (B.error) { FA.qs('vErr').innerHTML = '<div class="fa-notice fa-notice--bad">' + FA.esc(B.error) + '</div>'; return; }
    if (!B.voucher) { FA.qs('vMain').innerHTML = '<div class="fa-notice">Type a voucher number, or open one from a report.</div>'; FA.qs('vNo').focus(); return; }
    FA.qs('vNo').value = B.voucher;
    var t = B.totals, can = B.rights && B.rights.manage, canAdj = B.rights && B.rights.adjust;
    var h = '';

    // Balance and pattern
    h += '<div class="gl-summary">' +
        [['Voucher', String(B.voucher)], ['Lines', FA.money(B.shape.lines)], ['Debit', t.dr], ['Credit', t.cr], ['Difference', t.diff]].map(function (x) { return '<div><span>' + x[0] + '</span><b>' + FA.esc(x[1]) + '</b></div>'; }).join('') + '</div>';
    if (t.balanced && !B.issue) h += '<div class="fa-notice fa-notice--ok">This voucher balances: debit equals credit, on one date from one source.</div>';
    else if (B.issue) {
        h += '<div class="fa-notice ' + (t.balanced ? 'fa-notice--warn' : 'fa-notice--bad') + '"><b>' + FA.esc(B.issue.text) + '.</b> ' + FA.esc(B.issue.cause) +
            (t.balanced ? '' : ' Debit and credit differ by ' + FA.esc(t.diff) + '.') + '<div class="gl-warn__actions">';
        if (canAdj && !t.balanced) h += '<a class="fa-btn fa-btn--primary fa-btn--sm" href="AccountsAdjustments.aspx?new=1&voucher=' + B.voucher + '">' + FA.icon('edit') + ' Start an adjusting entry</a>';
        if (can && !B.acks.length) h += '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="vAccept">Accept as known</button>';
        h += '<a class="fa-btn fa-btn--secondary fa-btn--sm" href="AccountsWarnings.aspx?rule=' + (t.balanced ? 'W05' : 'W01') + '">Finance Warnings</a></div></div>';
    }
    B.acks.forEach(function (k) {
        h += '<div class="fa-notice">Accepted as known under ' + FA.esc(k.rule) + ' by ' + FA.esc(k.by) + ' on ' + FA.esc(k.when) + ': ' + FA.esc(k.reason) +
            (can ? ' <button type="button" class="fa-btn fa-btn--link" data-unack="' + k.id + '">Withdraw</button>' : '') + '</div>';
    });

    // Lines
    h += '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Lines<span class="fa-card__meta">' + B.shape.dates + ' date' + (B.shape.dates === 1 ? '' : 's') + ', ' + B.shape.sources + ' source' + (B.shape.sources === 1 ? '' : 's') + '</span></div></div>' +
        (B.truncated ? '<div class="fa-notice fa-notice--warn" style="margin:10px 14px">Only the first 2,000 lines are shown.</div>' : '') +
        '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Date</th><th>Account</th><th>Particulars</th><th>Source</th><th>Recorded</th><th class="fa-num">Debit</th><th class="fa-num">Credit</th></tr></thead><tbody>';
    B.lines.forEach(function (l) {
        var acct = '<a class="gl-link" href="AccountsAccount.aspx?code=' + encodeURIComponent(l.key) + (l.member ? '&m=' + encodeURIComponent(l.member) : '') + '&to=' + l.iso + '">' + FA.esc(l.kind === 'SUBLEDGER' ? l.code : l.code) + '</a>' +
            '<span class="fa-sub">' + FA.esc(l.kind === 'SUBLEDGER' ? l.accountName + ' (' + l.type + ')' : l.accountName) + (l.kind === 'PROVISIONAL' ? ', not in the chart' : l.kind === 'UNMAPPED' ? ', not mapped' : '') + '</span>';
        h += '<tr><td>' + FA.esc(l.date) + '</td><td>' + acct + '</td><td>' + FA.esc(l.particulars) + (l.refNo ? '<span class="fa-sub">Reference ' + FA.esc(l.refNo) + '</span>' : '') + '</td>' +
            '<td>' + FA.esc(l.source || '(none)') + '<span class="fa-sub">' + FA.esc(l.teller) + '</span></td><td>' + FA.esc(l.recorded) + '</td><td class="fa-num">' + FA.esc(l.dr) + '</td><td class="fa-num">' + FA.esc(l.cr) + '</td></tr>';
    });
    h += '</tbody><tfoot><tr><td colspan="5">Total</td><td class="fa-num">' + FA.esc(t.dr) + '</td><td class="fa-num">' + FA.esc(t.cr) + '</td></tr></tfoot></table></div></div>';

    // Sources
    function list(title, rows, cols, map) {
        if (!rows || !rows.length) return '';
        return '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">' + FA.esc(title) + '<span class="fa-card__meta">' + rows.length + '</span></div></div><div class="fa-table-wrap"><table class="fa-table"><thead><tr>' +
            cols.map(function (c) { return '<th' + (c[1] ? ' class="fa-num"' : '') + '>' + c[0] + '</th>'; }).join('') + '</tr></thead><tbody>' +
            rows.map(function (r) { return '<tr>' + map(r).map(function (v, i) { return '<td' + (cols[i][1] ? ' class="fa-num"' : '') + '>' + v + '</td>'; }).join('') + '</tr>'; }).join('') + '</tbody></table></div></div>';
    }
    h += list('Journal', B.journals, [['Journal'], ['Status'], ['Type'], ['Date'], ['Particulars'], ['By']], function (j) { return [FA.esc(j.no), FA.esc(j.status), FA.esc(j.type), FA.esc(j.date), FA.esc(j.particulars), FA.esc(j.by)]; });
    h += list('Fee tracking records behind these lines', B.tracking, [['Record'], ['Student'], ['Type'], ['Item'], ['Year and semester'], ['Detail'], ['Date'], ['Amount', 1]], function (r) {
        return [String(r.tid), '<a class="gl-link" href="AccountsAccount.aspx?code=SUB%3ASTUDENTS&m=' + encodeURIComponent(r.regno) + '">' + FA.esc(r.regno) + '</a>', FA.esc(r.type), FA.esc(r.item), FA.esc(r.year + ' / ' + r.sem), FA.esc(r.detail), FA.esc(r.date), FA.esc(r.amount)];
    });
    h += list('Requisitions', B.requisitions, [['Requisition'], ['Title'], ['Status'], ['Amount', 1]], function (r) { return ['<a class="gl-link" href="RequisitionDetail.aspx?id=' + r.id + '">' + FA.esc(r.no) + '</a>', FA.esc(r.title), FA.esc(r.status), FA.esc(r.amount)]; });
    h += list('Lines deleted from this voucher', B.deleted, [['Line'], ['Date'], ['Account'], ['Side'], ['Particulars'], ['Deleted'], ['By'], ['Amount', 1]], function (r) { return [String(r.tid), FA.esc(r.date), FA.esc(r.code), FA.esc(r.side), FA.esc(r.particulars), FA.esc(r.when), FA.esc(r.by), FA.esc(r.amount)]; });
    h += list('Lines edited after posting', B.edited, [['Line'], ['Account'], ['Before'], ['After'], ['Edited'], ['By']], function (r) { return [String(r.tid), FA.esc(r.code), FA.esc(r.before), FA.esc(r.after), FA.esc(r.when), FA.esc(r.by)]; });

    FA.qs('vMain').innerHTML = h;

    var acc = FA.qs('vAccept');
    if (acc) acc.onclick = function () {
        var rule = t.balanced ? 'W05' : 'W01';
        FA.reason({ title: 'Accept voucher ' + B.voucher + ' as known', sub: rule === 'W01' ? 'Unbalanced vouchers (W01)' : 'Voucher numbers reused (W05)', min: 15,
            message: 'The voucher stays as it is in the ledger. Accepting it only leaves it out of the warning count, with your reason on record. You can withdraw it later.', label: 'Why is this voucher acceptable?', ok: 'Accept' },
            function (reason, m, done) { FA.call(PAGE, 'SaveAcceptance', { rule: rule, voucher: B.voucher, reason: reason }, function (r) { if (!r.success) return done(r.message); done(); FA.toast(r.message); setTimeout(function () { location.reload(); }, 900); }); });
    };
    Array.prototype.forEach.call(document.querySelectorAll('[data-unack]'), function (b) {
        b.onclick = function () {
            FA.reason({ title: 'Withdraw the acceptance', min: 10, label: 'Reason', ok: 'Withdraw', danger: true },
                function (reason, m, done) { FA.call(PAGE, 'RemoveAcceptance', { id: parseInt(b.getAttribute('data-unack'), 10), reason: reason }, function (r) { if (!r.success) return done(r.message); done(); FA.toast(r.message); setTimeout(function () { location.reload(); }, 900); }); });
        };
    });
})();
