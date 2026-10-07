/* General Ledger: adjusting entries. List, wizard (details, lines, preview), detail with maker-checker actions. */
(function () {
    'use strict';
    var FA = window.FA, GL = window.GL, B = window.GL_BOOT || {}, PAGE = 'AccountsAdjustments.aspx';
    var R = B.rights || {}, st = { tab: 'PENDING' };
    var STATUS = { DRAFT: ['Draft', 'neutral'], PENDING: ['Waiting for approval', 'warn'], POSTED: ['Posted', 'ok'], REJECTED: ['Rejected', 'bad'], CANCELLED: ['Cancelled', 'neutral'] };
    function badge(s) { var x = STATUS[s] || [s, 'neutral']; return '<span class="fa-badge fa-badge--' + x[1] + '">' + x[0] + '</span>'; }

    FA.qs('jNote').innerHTML = '<b>How it works.</b> A balanced entry posts debit and credit lines of equal value under its own voucher number (from ' + FA.money(B.base + 1) + ' upwards). ' +
        'An entry that completes a voucher adds the missing side under that voucher\'s own number, so the voucher balances: this is what reduces the trial balance difference. ' +
        'The maker cannot approve their own entry. ' + (R.approve ? 'You can approve entries made by others.' : 'Approval is by the University Bursar.');

    // ── List ─────────────────────────────────────────────────────────
    var TABS = [['PENDING', 'Waiting for approval'], ['DRAFT', 'Drafts'], ['POSTED', 'Posted'], ['REJECTED', 'Rejected'], ['CANCELLED', 'Cancelled'], ['', 'All']];
    function load() {
        FA.call(PAGE, 'LoadAdjustments', { status: st.tab }, function (r) {
            if (!r.success) { FA.qs('jList').innerHTML = '<div class="fa-empty">' + FA.esc(r.message) + '</div>'; return; }
            FA.qs('jTabs').innerHTML = TABS.map(function (t) { var n = t[0] ? (r.counts[t[0]] || 0) : ''; return '<button type="button" class="fa-subtab' + (st.tab === t[0] ? ' is-active' : '') + '" data-t="' + t[0] + '">' + t[1] + (n !== '' ? ' (' + n + ')' : '') + '</button>'; }).join('');
            Array.prototype.forEach.call(FA.qs('jTabs').querySelectorAll('[data-t]'), function (b) { b.onclick = function () { st.tab = b.getAttribute('data-t'); load(); }; });
            FA.qs('jList').innerHTML = !r.rows.length ? '<div class="fa-empty">No adjusting entries here.</div>' :
                '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Number</th><th>Status</th><th>Purpose</th><th>Kind</th><th>Entry date</th><th class="fa-num">Lines</th><th class="fa-num">Amount</th><th>Made by</th><th>Decided by</th><th>Voucher</th></tr></thead><tbody>' +
                r.rows.map(function (x) {
                    return '<tr class="is-click" data-id="' + x.id + '"><td class="fa-code">' + FA.esc(x.no) + '</td><td>' + badge(x.status) + '</td><td>' + FA.esc(x.purpose) + '</td><td>' + (x.mode === 'COMPLETE' ? 'Completes ' + FA.esc(x.voucher) : 'Balanced') + '</td>' +
                        '<td class="gl-nowrap">' + FA.esc(x.entryDate) + '</td><td class="fa-num">' + x.lines + '</td><td class="fa-num">' + FA.esc(x.total) + '</td><td>' + FA.esc(x.by) + '<span class="fa-sub">' + FA.esc(x.created) + '</span></td><td>' + FA.esc(x.decidedBy) + '</td><td class="fa-code">' + FA.esc(x.posted) + '</td></tr>';
                }).join('') + '</tbody></table></div>';
            Array.prototype.forEach.call(FA.qs('jList').querySelectorAll('tr[data-id]'), function (tr) { tr.onclick = function () { detail(parseInt(tr.getAttribute('data-id'), 10)); }; });
        });
    }

    // ── Wizard ───────────────────────────────────────────────────────
    function wizard(entry, prefill) {
        var e = entry || { id: 0, version: 0, mode: 'BALANCED', purpose: '', reason: '', entryDate: B.today, voucher: null, warningId: null, lines: [] };
        if (prefill) Object.keys(prefill).forEach(function (k) { e[k] = prefill[k]; });
        var lines = (e.lines || []).map(function (l) { return { key: l.key, name: l.name, member: l.member || '', side: l.side, amount: l.amount ? String(l.amount) : '', particulars: l.particulars || '' }; });
        if (!lines.length) lines = [blank('DR'), blank('CR')];
        var step = 1, vinfo = null;
        var m = FA.modal({ title: e.id ? 'Edit ' + e.no : 'New adjusting entry', size: 'wide', sticky: true, body: '<ol class="fa-steps" id="zSteps"><li><b>Step 1</b>Purpose and reason</li><li><b>Step 2</b>Lines</li><li><b>Step 3</b>Check and save</li></ol><div id="zBody"></div><div class="fa-error" id="zErr" style="margin-top:10px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Close</button><span class="fa-spacer"></span><button type="button" class="fa-btn fa-btn--secondary" data-back>Back</button><button type="button" class="fa-btn fa-btn--primary" data-next>Next</button>' });
        function blank(side) { return { key: '', name: '', member: '', side: side, amount: '', particulars: '' }; }

        function draw() {
            Array.prototype.forEach.call(FA.qs('zSteps').children, function (li, i) { li.className = i + 1 === step ? 'is-now' : i + 1 < step ? 'is-done' : ''; });
            m.foot.querySelector('[data-back]').style.display = step > 1 ? '' : 'none';
            m.foot.querySelector('[data-next]').textContent = step < 3 ? 'Next' : 'Save as draft';
            FA.qs('zErr').textContent = '';
            if (step === 1) step1(); else if (step === 2) step2(); else step3();
        }

        function step1() {
            FA.qs('zBody').innerHTML = '<div class="fa-form">' +
                '<div class="fa-field fa-full"><span class="fa-label">Kind of entry</span><div class="fa-radios">' +
                '<label><input type="radio" name="zMode" value="BALANCED"' + (e.mode !== 'COMPLETE' ? ' checked' : '') + '/> Balanced entry: debit equals credit, under its own voucher number</label>' +
                '<label><input type="radio" name="zMode" value="COMPLETE"' + (e.mode === 'COMPLETE' ? ' checked' : '') + '/> Complete an unbalanced voucher: add its missing side under its own number</label></div></div>' +
                '<div class="fa-field" id="zVField"><label class="fa-label" for="zV">Voucher to complete <span class="fa-req">*</span></label><div class="fa-row"><input type="number" class="fa-input" id="zV" style="max-width:180px" value="' + (e.voucher || '') + '"/><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="zVLoad">Show it</button></div></div>' +
                '<div class="fa-field"><label class="fa-label" for="zDate">Entry date <span class="fa-req">*</span></label><input type="date" class="fa-input" id="zDate" max="' + B.today + '" value="' + FA.esc(e.entryDate) + '"/><span class="fa-hint">The date the correction belongs to. A date in a signed-off period needs the approver\'s reason.</span></div>' +
                '<div class="fa-field fa-full"><div id="zVInfo"></div></div>' +
                '<div class="fa-field"><label class="fa-label" for="zPurpose">Purpose <span class="fa-req">*</span></label><select class="fa-select" id="zPurpose">' + B.purposes.map(function (p) { return '<option' + (p === e.purpose ? ' selected' : '') + '>' + FA.esc(p) + '</option>'; }).join('') + '</select></div>' +
                '<div class="fa-field"><label class="fa-label" for="zWarn">Finance warning (optional)</label><input type="number" class="fa-input" id="zWarn" value="' + (e.warningId || '') + '" placeholder="Warning number"/></div>' +
                '<div class="fa-field fa-full"><label class="fa-label" for="zReason">Reason <span class="fa-req">*</span></label><textarea class="fa-textarea" id="zReason" rows="4" maxlength="2000" placeholder="What is wrong, how you know, and what this entry does. The approver and the auditors read this.">' + FA.esc(e.reason) + '</textarea></div></div>';
            function sync() { var c = FA.radio('zMode') === 'COMPLETE'; FA.qs('zVField').style.display = c ? '' : 'none'; FA.qs('zVInfo').style.display = c ? '' : 'none'; if (c && FA.qs('zPurpose').value === B.purposes[1]) FA.qs('zPurpose').value = B.purposes[0]; }
            Array.prototype.forEach.call(document.querySelectorAll('input[name=zMode]'), function (r) { r.onchange = sync; });
            FA.qs('zVLoad').onclick = loadVoucher;
            sync();
            if (e.mode === 'COMPLETE' && e.voucher) loadVoucher();
        }

        function loadVoucher(cb) {
            var v = parseInt(FA.val('zV'), 10);
            if (!(v > 0)) { FA.qs('zVInfo').innerHTML = ''; return; }
            FA.call(PAGE, 'LoadVoucherForAdjust', { voucher: v }, function (r) {
                if (!r.success) { FA.qs('zVInfo').innerHTML = '<div class="fa-notice fa-notice--bad">' + FA.esc(r.message) + '</div>'; vinfo = null; return; }
                vinfo = r;
                FA.qs('zVInfo').innerHTML = '<div class="fa-notice ' + (r.balanced ? 'fa-notice--ok' : 'fa-notice--warn') + '">Voucher ' + r.voucher + ' has ' + FA.plural(r.count, 'line', 'lines') + ' and is ' + (r.balanced ? 'balanced already.' : 'out by <b>' + FA.esc(r.diffText) + '</b> (debit less credit). The entry must add a net ' + (r.diff > 0 ? 'credit' : 'debit') + ' of ' + GL.money(Math.abs(r.diff)) + '.') + '</div>' +
                    '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Date</th><th>Account</th><th>Particulars</th><th>Side</th><th class="fa-num">Amount</th></tr></thead><tbody>' +
                    r.lines.map(function (l) { return '<tr><td class="gl-nowrap">' + FA.esc(l.date) + '</td><td class="fa-code">' + FA.esc(l.code) + '<span class="fa-sub">' + FA.esc(l.name) + '</span></td><td>' + FA.esc(l.particulars) + '</td><td>' + l.side + '</td><td class="fa-num">' + FA.esc(l.amount) + '</td></tr>'; }).join('') + '</tbody></table></div>';
                if (typeof cb === 'function') cb();
            });
        }

        function readStep1() {
            e.mode = FA.radio('zMode') || 'BALANCED'; e.voucher = parseInt(FA.val('zV'), 10) || null; e.entryDate = FA.val('zDate');
            e.purpose = FA.val('zPurpose'); e.reason = FA.val('zReason'); e.warningId = parseInt(FA.val('zWarn'), 10) || null;
            if (e.mode === 'COMPLETE' && !e.voucher) return 'Choose the voucher to complete.';
            if (!e.entryDate) return 'Choose the entry date.';
            if (e.reason.length < 20) return 'Explain the reason in at least 20 characters.';
            return null;
        }

        function step2() {
            if (e.mode === 'COMPLETE' && vinfo && !vinfo.balanced && lines.length === 2 && !lines[0].key && !lines[1].key) {
                lines = [{ key: '', name: '', member: '', side: vinfo.diff > 0 ? 'CR' : 'DR', amount: String(Math.abs(vinfo.diff)), particulars: 'Missing side of voucher ' + vinfo.voucher }];
            }
            var h = '<div class="fa-table-wrap"><table class="fa-table gl-lines"><thead><tr><th style="width:34%">Account</th><th style="width:15%">Member</th><th style="width:9%">Side</th><th style="width:14%" class="fa-num">Amount</th><th>Particulars</th><th></th></tr></thead><tbody id="zLines"></tbody></table></div>' +
                '<div class="fa-row" style="margin-top:8px"><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="zAdd">' + FA.icon('plus') + ' Add a line</button><span class="fa-spacer"></span></div><div class="gl-balance" id="zBal" style="margin-top:10px"></div>';
            FA.qs('zBody').innerHTML = h;
            var tb = FA.qs('zLines');
            lines.forEach(function (l, i) {
                var tr = document.createElement('tr');
                tr.innerHTML = '<td><div data-ta></div></td><td><input class="fa-input" data-m value="' + FA.esc(l.member) + '" placeholder="Reg. no or code"/></td>' +
                    '<td><select class="fa-select" data-s><option value="DR"' + (l.side === 'DR' ? ' selected' : '') + '>Debit</option><option value="CR"' + (l.side === 'CR' ? ' selected' : '') + '>Credit</option></select></td>' +
                    '<td><input class="fa-input fa-num-input" data-a inputmode="numeric" value="' + FA.esc(l.amount ? GL.money(l.amount) : '') + '"/></td><td><input class="fa-input" data-p maxlength="300" value="' + FA.esc(l.particulars) + '"/></td>' +
                    '<td><button type="button" class="fa-btn fa-btn--link" data-rm title="Remove this line">' + FA.icon('x') + '</button></td>';
                tb.appendChild(tr);
                var ta = FA.typeahead(tr.querySelector('[data-ta]'), {
                    placeholder: 'Code or name',
                    source: function (q, cb) { FA.call(PAGE, 'SearchAccounts', { q: q }, function (r) { cb(r.success ? r.rows : []); }); },
                    render: function (a) { return '<b>' + FA.esc(a.code) + '</b> ' + FA.esc(a.title) + '<small>' + FA.esc(a.kind) + '</small>'; },
                    pickedText: function (a) { return a.name; },
                    value: l.key ? { id: l.key, name: l.name || l.key } : null,
                    onPick: function (a) { l.key = a ? a.id : ''; l.name = a ? a.name : ''; memberState(); }
                });
                function memberState() { var sub = /^SUB:/.test(l.key); var mi = tr.querySelector('[data-m]'); mi.disabled = !sub; if (!sub) mi.value = ''; mi.placeholder = sub ? (l.key === 'SUB:STUDENTS' ? 'Registration number' : 'Member code') : 'Not needed'; }
                memberState();
                tr.querySelector('[data-m]').oninput = function () { l.member = this.value.trim(); };
                tr.querySelector('[data-s]').onchange = function () { l.side = this.value; totals(); };
                tr.querySelector('[data-a]').oninput = function () { l.amount = this.value.replace(/[^0-9]/g, ''); totals(); };
                tr.querySelector('[data-a]').onblur = function () { this.value = l.amount ? GL.money(l.amount) : ''; };
                tr.querySelector('[data-p]').oninput = function () { l.particulars = this.value; };
                tr.querySelector('[data-rm]').onclick = function () { lines.splice(i, 1); step2(); };
            });
            FA.qs('zAdd').onclick = function () { lines.push(blank(lines.length % 2 ? 'CR' : 'DR')); step2(); };
            totals();
        }

        function totals() {
            var dr = 0, cr = 0;
            lines.forEach(function (l) { var a = parseInt(l.amount, 10) || 0; if (l.side === 'DR') dr += a; else cr += a; });
            var ok, msg;
            if (e.mode === 'COMPLETE') {
                var need = vinfo ? -vinfo.diff : null;
                ok = need !== null && dr - cr === need;
                msg = vinfo ? 'The voucher is out by ' + GL.money(vinfo.diff) + '; these lines add ' + GL.money(dr - cr) + ' (debit less credit). ' + (ok ? 'After posting it balances.' : 'They must add exactly ' + GL.money(need) + '.') : 'Show the voucher on step 1 to check the amounts.';
            } else { ok = dr === cr && dr > 0; msg = ok ? 'Debit equals credit.' : 'Debit and credit differ by ' + GL.money(dr - cr) + '.'; }
            var el = FA.qs('zBal'); if (!el) return;
            el.className = 'gl-balance ' + (ok ? 'is-ok' : 'is-bad');
            el.innerHTML = '<span>Debit <b>' + GL.money(dr) + '</b></span><span>Credit <b>' + GL.money(cr) + '</b></span><span>' + FA.esc(msg) + '</span>';
        }

        function payload() {
            return { id: e.id || 0, version: e.version || 0, mode: e.mode, voucher: e.voucher, purpose: e.purpose, reason: e.reason, entryDate: e.entryDate, warningId: e.warningId,
                lines: lines.map(function (l) { return { key: l.key, member: l.member, side: l.side, amount: l.amount, particulars: l.particulars }; }) };
        }

        function step3() {
            FA.qs('zBody').innerHTML = '<div class="fa-loading">Checking the effect</div>';
            FA.call(PAGE, 'PreviewAdjustment', { json: JSON.stringify(payload()) }, function (r) {
                if (!r.success) { FA.qs('zBody').innerHTML = '<div class="fa-error">' + FA.esc(r.message) + '</div>'; return; }
                var p = r.preview;
                var h = (p.problems.length ? '<div class="fa-notice fa-notice--bad">' + p.problems.map(FA.esc).join(' ') + ' You can still save the draft and correct it later; it cannot be submitted like this.</div>' : '<div class="fa-notice fa-notice--ok">The entry is complete and can be submitted after saving.</div>') +
                    '<div class="fa-dl fa-dl--4" style="margin-bottom:12px"><div><dt>Purpose</dt><dd>' + FA.esc(e.purpose) + '</dd></div><div><dt>Entry date</dt><dd>' + FA.esc(GL.date(e.entryDate)) + '</dd></div><div><dt>Debit and credit</dt><dd>' + FA.esc(p.dr) + ' and ' + FA.esc(p.cr) + '</dd></div>' +
                    '<div><dt>Trial balance difference</dt><dd>' + FA.esc(p.tbNow) + ' now, ' + FA.esc(p.tbAfter) + ' after</dd></div></div>' +
                    (p.voucher ? '<div class="fa-notice">Voucher ' + p.voucher.no + ': out by ' + FA.esc(p.voucher.now) + ' now, ' + FA.esc(p.voucher.after) + ' after.</div>' : '') +
                    '<div class="fa-section">Effect on each account (balance debit positive)</div><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Account</th><th class="fa-num">Balance now</th><th class="fa-num">This entry</th><th class="fa-num">After</th></tr></thead><tbody>' +
                    p.rows.map(function (x) { return '<tr><td>' + FA.esc(x.account) + '</td><td class="fa-num">' + FA.esc(x.now) + '</td><td class="fa-num">' + FA.esc(x.move) + '</td><td class="fa-num">' + FA.esc(x.after) + '</td></tr>'; }).join('') + '</tbody></table></div>' +
                    '<div class="fa-section">Reason</div><div style="line-height:1.6">' + FA.esc(e.reason) + '</div>';
                FA.qs('zBody').innerHTML = h;
            });
        }

        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        m.foot.querySelector('[data-back]').onclick = function () { step--; draw(); };
        m.foot.querySelector('[data-next]').onclick = function () {
            if (step === 1) { var er = readStep1(); if (er) { FA.qs('zErr').textContent = er; return; } if (e.mode === 'COMPLETE' && (!vinfo || vinfo.voucher !== e.voucher)) { loadVoucher(function () { step = 2; draw(); }); return; } step = 2; draw(); return; }
            if (step === 2) {
                for (var i = 0; i < lines.length; i++) { var l = lines[i]; if (!l.key) { FA.qs('zErr').textContent = 'Line ' + (i + 1) + ': choose an account.'; return; } if (!(parseInt(l.amount, 10) > 0)) { FA.qs('zErr').textContent = 'Line ' + (i + 1) + ': enter an amount.'; return; } if ((l.particulars || '').trim().length < 5) { FA.qs('zErr').textContent = 'Line ' + (i + 1) + ': write particulars of at least 5 characters.'; return; } if (/^SUB:/.test(l.key) && !l.member) { FA.qs('zErr').textContent = 'Line ' + (i + 1) + ': enter the member.'; return; } }
                step = 3; draw(); return;
            }
            var b = m.foot.querySelector('[data-next]'); FA.busy(b, true);
            FA.call(PAGE, 'SaveAdjustmentDraft', { json: JSON.stringify(payload()) }, function (r) {
                FA.busy(b, false);
                if (!r.success) { FA.qs('zErr').textContent = r.message; return; }
                FA.toast(r.message + ' ' + r.entry.no + '.'); m.close(); st.tab = 'DRAFT'; load(); detail(r.id);
            });
        };
        draw();
    }

    // ── Detail and actions ───────────────────────────────────────────
    function detail(id) {
        FA.call(PAGE, 'LoadAdjustment', { id: id }, function (r) {
            if (!r.success) { FA.toast(r.message, true); return; }
            var x = r.entry, mine = x.mine;
            var h = '<div class="fa-row" style="margin-bottom:10px">' + badge(x.status) + '<b>' + FA.esc(x.purpose) + '</b><span class="fa-spacer"></span><span class="fa-muted">' + (x.mode === 'COMPLETE' ? 'Completes voucher ' + x.voucher : 'Balanced entry') + '</span></div>' +
                '<dl class="fa-dl fa-dl--4"><div><dt>Entry date</dt><dd>' + FA.esc(x.entryDateText) + '</dd></div><div><dt>Amount</dt><dd>' + FA.esc(x.total) + '</dd></div><div><dt>Made by</dt><dd>' + FA.esc(x.createdBy) + ', ' + FA.esc(x.createdAt) + '</dd></div>' +
                '<div><dt>' + (x.status === 'POSTED' ? 'Posted' : 'Decided') + '</dt><dd>' + (x.decidedBy ? FA.esc(x.decidedBy) + ', ' + FA.esc(x.decidedAt) : 'Not yet') + '</dd></div></dl>' +
                (x.postedVoucher ? '<div class="fa-notice fa-notice--ok" style="margin-top:10px">Posted under voucher <a class="gl-link" href="AccountsVoucher.aspx?v=' + x.postedVoucher + '">' + x.postedVoucher + '</a> on ' + FA.esc(x.postedAt) + '.</div>' : '') +
                (x.decisionNote ? '<div class="fa-notice" style="margin-top:10px">' + (x.status === 'REJECTED' ? 'Rejected: ' : x.status === 'CANCELLED' ? 'Cancelled: ' : 'Approver\'s note: ') + FA.esc(x.decisionNote) + '</div>' : '') +
                (x.overrideNote ? '<div class="fa-notice fa-notice--warn" style="margin-top:10px">Posted into a signed-off period: ' + FA.esc(x.overrideNote) + '</div>' : '') +
                '<div class="fa-section">Reason</div><div style="line-height:1.6">' + FA.esc(x.reason) + '</div>' +
                '<div class="fa-section">Lines</div><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Account</th><th>Particulars</th><th class="fa-num">Debit</th><th class="fa-num">Credit</th></tr></thead><tbody>' +
                x.lines.map(function (l) { return '<tr><td>' + FA.esc(l.name) + (l.member ? '<span class="fa-sub">' + FA.esc(l.member) + '</span>' : '') + '</td><td>' + FA.esc(l.particulars) + '</td><td class="fa-num">' + (l.side === 'DR' ? FA.esc(l.amountText) : '') + '</td><td class="fa-num">' + (l.side === 'CR' ? FA.esc(l.amountText) : '') + '</td></tr>'; }).join('') + '</tbody></table></div>' +
                '<div class="fa-section">Audit trail</div><ul class="gl-events">' + x.audit.map(function (a) { return '<li><b>' + FA.esc(a.when) + '</b> ' + FA.esc(a.action.replace('_', ' ').toLowerCase()) + ' by ' + FA.esc(a.by) + (a.reason ? ': ' + FA.esc(a.reason) : '') + '</li>'; }).join('') + '</ul>';
            var foot = '<button type="button" class="fa-btn fa-btn--secondary" data-a="close">Close</button><span class="fa-spacer"></span>';
            if (x.status === 'DRAFT' && mine) foot += '<button type="button" class="fa-btn fa-btn--danger" data-a="cancel">Cancel</button><button type="button" class="fa-btn fa-btn--secondary" data-a="edit">Edit</button><button type="button" class="fa-btn fa-btn--primary" data-a="submit">Submit for approval</button>';
            if (x.status === 'PENDING' && mine) foot += '<button type="button" class="fa-btn fa-btn--danger" data-a="cancel">Cancel</button><button type="button" class="fa-btn fa-btn--secondary" data-a="takeback">Take back to draft</button>';
            if (x.status === 'PENDING' && !mine && R.approve) foot += '<button type="button" class="fa-btn fa-btn--danger" data-a="reject">Reject</button><button type="button" class="fa-btn fa-btn--primary" data-a="approve">Approve and post</button>';
            if (x.status === 'POSTED' && R.adjust) foot += '<button type="button" class="fa-btn fa-btn--secondary" data-a="reverse">Prepare a reversal</button>';
            var m = FA.modal({ title: x.no, sub: 'Adjusting entry', size: 'wide', body: h, foot: foot });
            Array.prototype.forEach.call(m.foot.querySelectorAll('[data-a]'), function (b) { b.onclick = function () { act(b.getAttribute('data-a'), x, m); }; });
        });
    }

    function done(m) { return function (r, fin) { if (!r.success) return fin(r.message); fin(); FA.toast(r.message); m.close(); load(); detail(r.entry ? r.entry.id : r.id); }; }

    function act(a, x, m) {
        if (a === 'close') return m.close();
        if (a === 'edit') { m.close(); return wizard(x); }
        if (a === 'submit') return FA.confirm({ title: 'Submit ' + x.no, message: 'Send it to an approver. You cannot change it while it waits; you can take it back.', ok: 'Submit' }, function () {
            FA.call(PAGE, 'SubmitAdjustment', { id: x.id, version: x.version }, function (r) { if (!r.success) { FA.toast(r.message, true); return; } FA.toast(r.message); m.close(); st.tab = 'PENDING'; load(); });
        });
        if (a === 'takeback') return FA.reason({ title: 'Take ' + x.no + ' back to draft', min: 10, label: 'Reason', ok: 'Take back' }, function (reason, mm, fin) { FA.call(PAGE, 'TakeBackAdjustment', { id: x.id, version: x.version, reason: reason }, function (r) { done(m)(r, fin); }); });
        if (a === 'cancel') return FA.reason({ title: 'Cancel ' + x.no, min: 10, label: 'Reason', ok: 'Cancel the entry', danger: true, message: 'A cancelled entry stays on record. Nothing is posted.' }, function (reason, mm, fin) { FA.call(PAGE, 'CancelAdjustment', { id: x.id, version: x.version, reason: reason }, function (r) { done(m)(r, fin); }); });
        if (a === 'reject') return FA.reason({ title: 'Reject ' + x.no, min: 15, label: 'Reason for the maker', ok: 'Reject', danger: true }, function (reason, mm, fin) { FA.call(PAGE, 'RejectAdjustment', { id: x.id, version: x.version, reason: reason }, function (r) { done(m)(r, fin); }); });
        if (a === 'reverse') return FA.confirm({ title: 'Prepare a reversal of ' + x.no, message: 'A new draft with every line on the opposite side is prepared for you to check and submit. Nothing is posted yet.', ok: 'Prepare draft' }, function () {
            FA.call(PAGE, 'CreateReversalDraft', { id: x.id }, function (r) { if (!r.success) { FA.toast(r.message, true); return; } FA.toast(r.message); m.close(); st.tab = 'DRAFT'; load(); detail(r.id); });
        });
        if (a === 'approve') {
            var mm = FA.modal({ title: 'Approve and post ' + x.no, sub: x.purpose, size: 'mid',
                body: '<div class="fa-notice fa-notice--warn">Approving adds ' + FA.plural(x.lines.length, 'line', 'lines') + ' to the general ledger' + (x.mode === 'COMPLETE' ? ' under voucher ' + x.voucher + ', which must then balance' : ' under a new voucher number') + '. Posted lines are never edited; a mistake is corrected by a reversal.</div>' +
                    '<div class="fa-field"><label class="fa-label" for="apNote">Note (optional)</label><textarea class="fa-textarea" id="apNote" rows="2" maxlength="1000"></textarea></div>' +
                    '<div class="fa-field" style="margin-top:10px"><label class="fa-label" for="apOver">If the entry date is in a signed-off period: why it must be posted there</label><textarea class="fa-textarea" id="apOver" rows="2" maxlength="1000" placeholder="Leave blank unless asked"></textarea></div><div class="fa-error" id="apErr" style="margin-top:8px"></div>',
                foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Approve and post</button>' });
            mm.foot.querySelector('[data-x]').onclick = function () { mm.close(); };
            var ok = mm.foot.querySelector('[data-ok]');
            ok.onclick = function () {
                FA.busy(ok, true, 'Posting');
                FA.call(PAGE, 'ApproveAdjustment', { id: x.id, version: x.version, note: FA.val('apNote'), overrideNote: FA.val('apOver') }, function (r) {
                    FA.busy(ok, false);
                    if (!r.success) { FA.qs('apErr').textContent = r.message; return; }
                    mm.close(); m.close(); FA.toast(r.message); st.tab = 'POSTED'; load(); detail(x.id);
                });
            };
        }
    }

    // ── Start ────────────────────────────────────────────────────────
    FA.qs('jNew').style.display = R.adjust ? '' : 'none';
    FA.qs('jNew').onclick = function () { wizard(null); };
    var u = FA.readUrl();
    if (!R.approve) st.tab = 'DRAFT';
    load();
    if (u.id) detail(parseInt(u.id, 10));
    else if (u['new'] === '1' && R.adjust) {
        var pre = {};
        if (u.voucher) { pre.mode = 'COMPLETE'; pre.voucher = parseInt(u.voucher, 10); pre.purpose = B.purposes[0]; pre.reason = 'Voucher ' + u.voucher + ' does not balance. '; }
        if (u.warning) pre.warningId = parseInt(u.warning, 10);
        wizard(null, pre);
    }
})();
