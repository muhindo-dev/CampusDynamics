/* Fixed Assets: Asset Categories screen. Two-level tree with drag and drop (native HTML5, as in the
   course rearrangement module), a keyboard alternative, pending changes saved together, and an editor. ES5. */
(function () {
    'use strict';
    var PAGE = 'AssetCategories.aspx', esc = FA.esc, money = FA.money, qs = FA.qs;
    var nodes = [], original = {}, canManage = false, gl = [], defaults = {}, current = null, open = {};

    function load(keepId) {
        FA.call(PAGE, 'GetTree', {}, function (r) {
            if (!r.success) { qs('cTreeWrap').innerHTML = '<div class="fa-empty">' + esc(r.message) + '</div>'; return; }
            nodes = r.nodes; canManage = r.canManage; gl = r.glAccounts; defaults = r.defaults;
            original = {};
            nodes.forEach(function (c) { original[c.id] = { p: 0, s: c.sort }; (c.children || []).forEach(function (s) { original[s.id] = { p: c.id, s: s.sort }; }); });
            if (!Object.keys(open).length) nodes.forEach(function (c) { open[c.id] = false; });
            qs('cHelp').style.display = canManage ? '' : 'none';
            header();
            draw();
            if (keepId) select(keepId);
        });
    }

    function header() {
        qs('faHeaderActions').innerHTML = canManage ? '<button type="button" class="fa-btn fa-btn--inverse" id="cNew">' + FA.icon('plus') + ' New category</button>' : '';
        if (qs('cNew')) qs('cNew').onclick = function () { if (blockIfPending()) return; editor({ id: 0, parentId: 0, active: true, children: [] }); };
    }

    function find(id) {
        var f = null;
        nodes.forEach(function (c) { if (c.id === id) f = c; (c.children || []).forEach(function (s) { if (s.id === id) f = s; }); });
        return f;
    }
    function parentOf(id) { var p = null; nodes.forEach(function (c) { (c.children || []).forEach(function (s) { if (s.id === id) p = c; }); }); return p; }

    function meta(n) {
        var e = n.effective;
        var m = e.method === 'SL' ? 'SL ' + (e.lifeYears || '') + ' yrs' : e.method === 'RB' ? 'RB ' + (e.ratePct || '') + '%' : 'Not depreciated';
        return m + ' &middot; ' + FA.plural(n.assetCount, 'asset', 'assets');
    }

    // ── Drawing ───────────────────────────────────────────────────────
    function draw() {
        var showInactive = qs('cInactive').checked;
        var catCount = 0, subCount = 0;
        var h = '<ul class="fa-tree" id="cTree">';
        nodes.forEach(function (c, ci) {
            if (!showInactive && !c.active) return;
            catCount++;
            h += '<li class="fa-node' + (c.active ? '' : ' fa-inactive') + (current && current.id === c.id ? ' is-current' : '') + '" data-id="' + c.id + '" data-kind="cat"' + (canManage ? ' draggable="true"' : '') + '>' +
                '<div class="fa-node__row" data-sel="' + c.id + '">' +
                (canManage ? '<span class="fa-grip" title="Drag to reorder">' + FA.icon('grip') + '</span>' : '') +
                '<button type="button" class="fa-node__toggle" data-tg="' + c.id + '" aria-label="Show sub-categories">' + FA.icon(open[c.id] ? 'chevron' : 'right') + '</button>' +
                '<span class="fa-node__code">' + esc(c.code) + '</span><span class="fa-node__name">' + esc(c.name) + '</span>' +
                '<span class="fa-node__meta">' + meta(c) + '</span>' +
                (canManage ? '<span class="fa-row" style="gap:2px"><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-up="' + c.id + '"' + (ci === 0 ? ' disabled' : '') + ' aria-label="Move up">Up</button>' +
                             '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-dn="' + c.id + '"' + (ci === nodes.length - 1 ? ' disabled' : '') + ' aria-label="Move down">Down</button></span>' : '') +
                '</div><ul class="fa-kids' + (open[c.id] ? '' : ' is-closed') + '" data-cat="' + c.id + '">';
            (c.children || []).forEach(function (s) {
                if (!showInactive && !s.active) return;
                subCount++;
                h += '<li class="fa-kid' + (s.active && c.active ? '' : ' fa-inactive') + (current && current.id === s.id ? ' is-current' : '') + '" data-id="' + s.id + '" data-kind="sub"' + (canManage ? ' draggable="true"' : '') + '>' +
                    '<div class="fa-node__row" data-sel="' + s.id + '">' + (canManage ? '<span class="fa-grip" title="Drag to move">' + FA.icon('grip') + '</span>' : '') +
                    '<span class="fa-node__code">' + esc(s.code) + '</span><span class="fa-node__name">' + esc(s.name) + '</span><span class="fa-node__meta">' + meta(s) + '</span>' +
                    (canManage ? '<select data-mv="' + s.id + '" aria-label="Move to category">' + nodes.map(function (o) { return '<option value="' + o.id + '"' + (o.id === c.id ? ' selected' : '') + '>' + (o.id === c.id ? 'In ' : 'Move to ') + esc(o.code) + '</option>'; }).join('') + '</select>' : '') +
                    '</div></li>';
            });
            if (canManage && c.id) h += '<li style="list-style:none;margin-top:6px"><button type="button" class="fa-btn fa-btn--link" data-add="' + c.id + '">' + FA.icon('plus', 12) + ' Add sub-category</button></li>';
            h += '</ul></li>';
        });
        h += '</ul>';
        qs('cTreeWrap').innerHTML = catCount ? h : '<div class="fa-empty">No categories yet.</div>';
        qs('cCount').textContent = FA.plural(catCount, 'category', 'categories') + ', ' + FA.plural(subCount, 'sub-category', 'sub-categories');
        wire();
        pending();
    }

    function wire() {
        var tree = qs('cTree'); if (!tree) return;
        Array.prototype.forEach.call(tree.querySelectorAll('[data-sel]'), function (row) {
            row.onclick = function (e) {
                var t = e.target;
                if (t.closest && (t.closest('button') || t.closest('select'))) return;
                select(parseInt(row.getAttribute('data-sel'), 10));
            };
        });
        Array.prototype.forEach.call(tree.querySelectorAll('[data-tg]'), function (b) {
            b.onclick = function () { var id = parseInt(b.getAttribute('data-tg'), 10); open[id] = !open[id]; draw(); };
        });
        Array.prototype.forEach.call(tree.querySelectorAll('[data-add]'), function (b) {
            b.onclick = function () { if (blockIfPending()) return; var p = find(parseInt(b.getAttribute('data-add'), 10)); editor({ id: 0, parentId: p.id, active: true, parent: p }); };
        });
        Array.prototype.forEach.call(tree.querySelectorAll('[data-up],[data-dn]'), function (b) {
            b.onclick = function () {
                var up = b.hasAttribute('data-up'), id = parseInt(b.getAttribute(up ? 'data-up' : 'data-dn'), 10);
                var i = nodes.indexOf(find(id)), j = up ? i - 1 : i + 1;
                if (j < 0 || j >= nodes.length) return;
                var t = nodes[i]; nodes[i] = nodes[j]; nodes[j] = t;
                renumber(); draw();
            };
        });
        Array.prototype.forEach.call(tree.querySelectorAll('[data-mv]'), function (s) {
            s.onchange = function () { moveSub(parseInt(s.getAttribute('data-mv'), 10), parseInt(s.value, 10), null); };
        });
        if (!canManage) return;

        // Native drag and drop
        var dragId = null, dragKind = null;
        Array.prototype.forEach.call(tree.querySelectorAll('[draggable=true]'), function (el) {
            el.addEventListener('dragstart', function (e) {
                e.stopPropagation();
                dragId = parseInt(el.getAttribute('data-id'), 10); dragKind = el.getAttribute('data-kind');
                el.className += ' fa-is-dragging';
                try { e.dataTransfer.setData('text/plain', String(dragId)); e.dataTransfer.effectAllowed = 'move'; } catch (x) { }
            });
            el.addEventListener('dragend', function () { el.className = el.className.replace(' fa-is-dragging', ''); clearMarks(); });
            el.addEventListener('dragover', function (e) {
                var kind = el.getAttribute('data-kind'), id = parseInt(el.getAttribute('data-id'), 10);
                if (!dragId || id === dragId) return;
                if (dragKind === 'cat' && kind !== 'cat') return;
                e.preventDefault(); e.stopPropagation();
                clearMarks();
                el.className += (dragKind === 'sub' && kind === 'cat') ? ' fa-drop-target' : ' fa-drop-before';
            });
            el.addEventListener('dragleave', function () { el.className = el.className.replace(' fa-drop-target', '').replace(' fa-drop-before', ''); });
            el.addEventListener('drop', function (e) {
                e.preventDefault(); e.stopPropagation();
                var kind = el.getAttribute('data-kind'), id = parseInt(el.getAttribute('data-id'), 10);
                clearMarks();
                if (!dragId || id === dragId) return;
                if (dragKind === 'cat' && kind === 'cat') {
                    var from = nodes.indexOf(find(dragId)), moving = nodes.splice(from, 1)[0];
                    nodes.splice(nodes.indexOf(find(id)), 0, moving);
                    renumber(); draw();
                } else if (dragKind === 'sub' && kind === 'cat') moveSub(dragId, id, null);
                else if (dragKind === 'sub' && kind === 'sub') moveSub(dragId, parentOf(id).id, id);
                dragId = null;
            });
        });
        function clearMarks() {
            Array.prototype.forEach.call(tree.querySelectorAll('.fa-drop-target,.fa-drop-before'), function (x) { x.className = x.className.replace(' fa-drop-target', '').replace(' fa-drop-before', ''); });
        }
    }

    function moveSub(subId, toCatId, beforeId) {
        var from = parentOf(subId), to = find(toCatId), s = find(subId);
        if (!from || !to) return;
        if (from.id !== to.id && (to.children || []).some(function (k) { return k.code === s.code; })) {
            FA.toast(to.name + ' already has a sub-category coded ' + s.code + '. Change one of the codes first.', true); draw(); return;
        }
        from.children.splice(from.children.indexOf(s), 1);
        to.children = to.children || [];
        var at = beforeId ? to.children.indexOf(find(beforeId)) : -1;
        if (at < 0) to.children.push(s); else to.children.splice(at, 0, s);
        open[to.id] = true;
        renumber(); draw();
    }

    function renumber() {
        nodes.forEach(function (c, i) { c.sort = (i + 1) * 10; (c.children || []).forEach(function (s, j) { s.sort = (j + 1) * 10; s.parentId = c.id; }); });
    }

    // ── Pending changes ───────────────────────────────────────────────
    function changes() {
        var l = [];
        nodes.forEach(function (c) {
            var o = original[c.id]; if (o && o.s !== c.sort) l.push({ id: c.id, parentId: 0, sort: c.sort, n: c, moved: false });
            (c.children || []).forEach(function (s) {
                var os = original[s.id];
                if (os && (os.p !== c.id || os.s !== s.sort)) l.push({ id: s.id, parentId: c.id, sort: s.sort, n: s, moved: os.p !== c.id, from: find(os.p), to: c });
            });
        });
        return l;
    }
    function blockIfPending() {
        if (changes().length) { FA.toast('Save or discard the changes to the order first.', true); return true; }
        return false;
    }
    function pending() {
        var ch = changes(), bar = qs('cPending');
        if (!ch.length) { bar.className = 'fa-pending'; bar.innerHTML = ''; return; }
        bar.className = 'fa-pending is-on';
        bar.innerHTML = '<strong>' + FA.plural(ch.length, 'change', 'changes') + ' not saved</strong><span class="fa-spacer"></span>' +
            '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" id="pDiscard">Discard</button><button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" id="pSave">Review and save</button>';
        qs('pDiscard').onclick = function () { load(current ? current.id : 0); };
        qs('pSave').onclick = review;
    }
    function review() {
        var ch = changes();
        var moves = ch.filter(function (c) { return c.moved; }), needReason = moves.some(function (c) { return c.n.assetCount > 0; });
        var list = '<table class="fa-table"><tbody>' + ch.map(function (c) {
            return '<tr><td>' + (c.moved ? 'Move <strong>' + esc(c.n.code + ' ' + c.n.name) + '</strong> from ' + esc(c.from ? c.from.name : '') + ' to <strong>' + esc(c.to.name) + '</strong>' +
                   (c.n.assetCount ? '<span class="fa-sub">' + FA.plural(c.n.assetCount, 'asset keeps', 'assets keep') + ' its number and settings; only new assets follow the new defaults.</span>' : '')
                   : 'Reorder <strong>' + esc(c.n.code + ' ' + c.n.name) + '</strong>') + '</td></tr>';
        }).join('') + '</tbody></table>';
        var send = function (reason, done) {
            var ops = ch.map(function (c) { return { id: c.id, parentId: c.parentId, sort: c.sort, reason: c.moved ? reason : '' }; });
            FA.call(PAGE, 'SaveLayout', { opsJson: JSON.stringify(ops) }, function (r) {
                if (!r.success) { done(r.message); return; }
                done(); FA.toast(FA.plural(r.changed, 'change', 'changes') + ' saved.'); load(current ? current.id : 0);
            });
        };
        if (needReason) FA.reason({ title: 'Save changes', size: 'mid', extra: list, label: 'Reason for moving sub-categories that hold assets', min: 10, ok: 'Save' }, function (reason, m, done) { send(reason, done); });
        else {
            var m = FA.modal({ title: 'Save changes', size: 'mid', body: list, foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Save</button>' });
            m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
            m.foot.querySelector('[data-ok]').onclick = function () { var b = this; FA.busy(b, true); send('', function (err) { FA.busy(b, false); if (err) FA.toast(err, true); else m.close(); }); };
        }
    }

    // ── Editor ────────────────────────────────────────────────────────
    function select(id) {
        var n = find(id); if (!n) return;
        current = n;
        Array.prototype.forEach.call(document.querySelectorAll('.fa-node.is-current,.fa-kid.is-current'), function (x) { x.className = x.className.replace(' is-current', ''); });
        var li = document.querySelector('[data-id="' + id + '"]'); if (li) li.className += ' is-current';
        n.parent = n.parentId ? parentOf(n.id) : null;
        editor(n);
    }

    function glOptions(val, inheritText) {
        return '<option value="">' + esc(inheritText) + '</option>' + gl.map(function (a) { return '<option value="' + esc(a.code) + '"' + (a.code === val ? ' selected' : '') + '>' + esc(a.code + '  ' + a.name) + '</option>'; }).join('');
    }

    function editor(n) {
        var isSub = n.parentId > 0, p = isSub ? (n.parent || find(n.parentId)) : null, isNew = !n.id;
        var pe = p ? p.effective : null;
        var box = qs('cEditor');
        if (!canManage) {
            var e = n.effective;
            box.innerHTML = '<div class="fa-card__head"><div class="fa-card__title">' + esc(n.code + ' ' + n.name) + '</div>' + (n.active ? '' : '<span class="fa-badge fa-badge--neutral">Inactive</span>') + '</div><div class="fa-card__body">' +
                (n.description ? '<p style="margin-top:0">' + esc(n.description) + '</p>' : '') +
                '<dl class="fa-dl fa-dl--2"><div><dt>Type</dt><dd>' + (e.assetType === 'INTANGIBLE' ? 'Intangible' : 'Tangible') + '</dd></div><div><dt>Method</dt><dd>' + esc(e.methodLabel) + '</dd></div>' +
                '<div><dt>Useful life</dt><dd>' + (e.lifeYears ? e.lifeYears + ' years' : 'Not set') + '</dd></div><div><dt>Rate</dt><dd>' + (e.ratePct ? Number(e.ratePct).toFixed(2) + '%' : 'Not set') + '</dd></div>' +
                '<div><dt>Residual value</dt><dd>' + (e.residualPct || 0) + '% of cost</dd></div><div><dt>Revaluation</dt><dd>' + (e.revalueMonths ? 'Every ' + e.revalueMonths + ' months' : 'Cost model, not revalued') + '</dd></div>' +
                '<div><dt>Cost account</dt><dd>' + esc(e.glCost || 'Not set') + '</dd></div><div><dt>Accumulated depreciation account</dt><dd>' + esc(e.glAccum || 'Not set') + '</dd></div>' +
                '<div><dt>Expense account</dt><dd>' + esc(e.glExpense || 'Not set') + '</dd></div><div><dt>Assets</dt><dd>' + money(n.assetCount) + '</dd></div></dl></div>';
            return;
        }
        var inh = function (v, unit) { return isSub ? 'Inherit (' + (v === null || v === undefined || v === '' ? 'not set' : v + (unit || '')) + ')' : ''; };
        var h = '<div class="fa-card__head"><div class="fa-card__title">' + (isNew ? (isSub ? 'New sub-category in ' + esc(p.name) : 'New category') : esc((isSub ? p.code + '-' : '') + n.code + ' ' + n.name)) + '</div>' +
            (!isNew && !n.active ? '<span class="fa-badge fa-badge--neutral">Inactive</span>' : '') + '</div><div class="fa-card__body">';
        if (isSub) h += '<div class="fa-notice">Leave a field blank to use the category\'s value. New assets copy these values when they are created; changing them later does not alter existing assets.</div>';
        h += '<div class="fa-form">' +
            '<div class="fa-field"><label class="fa-label" for="eCode">Code <span class="fa-req">*</span></label><input id="eCode" class="fa-input" maxlength="' + (isSub ? 3 : 3) + '" value="' + esc(n.code || '') + '"' + (n.assetCount ? ' readonly' : '') + ' style="text-transform:uppercase"/>' +
            '<span class="fa-hint">' + (n.assetCount ? 'Fixed: it is part of the numbers of ' + FA.plural(n.assetCount, 'asset', 'assets') + '.' : (isSub ? '3 letters, for example LAP.' : '2 or 3 letters, for example CE.')) + '</span></div>' +
            '<div class="fa-field"><label class="fa-label" for="eName">Name <span class="fa-req">*</span></label><input id="eName" class="fa-input" maxlength="120" value="' + esc(n.name || '') + '"/></div>' +
            '<div class="fa-field fa-full"><label class="fa-label" for="eDesc">Description</label><input id="eDesc" class="fa-input" maxlength="500" value="' + esc(n.description || '') + '"/></div>' +
            '<div class="fa-field"><label class="fa-label" for="eType">Type</label><select id="eType" class="fa-select">' + (isSub ? '<option value="">' + inh(pe.assetType === 'INTANGIBLE' ? 'Intangible' : 'Tangible') + '</option>' : '') +
            '<option value="TANGIBLE"' + (n.assetType === 'TANGIBLE' ? ' selected' : '') + '>Tangible</option><option value="INTANGIBLE"' + (n.assetType === 'INTANGIBLE' ? ' selected' : '') + '>Intangible</option></select></div>' +
            '<div class="fa-field"><label class="fa-label" for="eMethod">Depreciation method' + (isSub ? '' : ' <span class="fa-req">*</span>') + '</label><select id="eMethod" class="fa-select">' + (isSub ? '<option value="">' + inh(pe.methodLabel) + '</option>' : '') +
            '<option value="SL"' + (n.method === 'SL' ? ' selected' : '') + '>Straight line</option><option value="RB"' + (n.method === 'RB' ? ' selected' : '') + '>Reducing balance</option><option value="NONE"' + (n.method === 'NONE' ? ' selected' : '') + '>Not depreciated</option></select></div>' +
            '<div class="fa-field"><label class="fa-label" for="eLife">Useful life (years)</label><input id="eLife" class="fa-input fa-num-input" value="' + esc(n.lifeYears == null ? '' : n.lifeYears) + '" placeholder="' + esc(isSub ? inh(pe.lifeYears, ' years') : '') + '"/><span class="fa-hint">For straight line. The rate follows from it.</span></div>' +
            '<div class="fa-field"><label class="fa-label" for="eRate">Annual rate (%)</label><input id="eRate" class="fa-input fa-num-input" value="' + esc(n.method === 'RB' && n.ratePct != null ? n.ratePct : '') + '" placeholder="' + esc(isSub ? inh(pe.ratePct, '%') : '') + '"/><span class="fa-hint">For reducing balance.</span></div>' +
            '<div class="fa-field"><label class="fa-label" for="eRes">Residual value (% of cost)</label><input id="eRes" class="fa-input fa-num-input" value="' + esc(n.residualPct == null ? '' : n.residualPct) + '" placeholder="' + esc(isSub ? inh(pe.residualPct, '%') : '0') + '"/></div>' +
            '<div class="fa-field"><label class="fa-label" for="eRev">Revalue every (months)</label><input id="eRev" class="fa-input fa-num-input" value="' + esc(n.revalueMonths == null ? '' : n.revalueMonths) + '" placeholder="' + esc(isSub ? inh(pe.revalueMonths, ' months') : 'Blank: not revalued') + '"/><span class="fa-hint">Blank means carried at cost. Used for the Revaluation due list.</span></div>' +
            '<div class="fa-field"><label class="fa-label" for="eCap">Capitalisation threshold (UGX)</label><input id="eCap" class="fa-input fa-num-input" value="' + esc(n.capThreshold == null ? '' : money(n.capThreshold)) + '" placeholder="' + esc(isSub ? inh(money(pe.capThreshold)) : 'Default ' + money(defaults.capThreshold)) + '"/><span class="fa-hint">Below this the asset form warns that the item may be an expense.</span></div>' +
            '<div></div>' +
            '<div class="fa-field fa-full"><label class="fa-label" for="eGlc">Cost account</label><select id="eGlc" class="fa-select">' + glOptions(n.glCost, isSub ? inh(pe.glCost) : 'Not set') + '</select></div>' +
            '<div class="fa-field fa-full"><label class="fa-label" for="eGla">Accumulated depreciation account</label><select id="eGla" class="fa-select">' + glOptions(n.glAccum, isSub ? inh(pe.glAccum) : 'Not set') + '</select></div>' +
            '<div class="fa-field fa-full"><label class="fa-label" for="eGle">Depreciation expense account</label><select id="eGle" class="fa-select">' + glOptions(n.glExpense, isSub ? inh(pe.glExpense) : 'Not set') + '</select></div>' +
            '<div class="fa-field fa-full"><label class="fa-label" for="eReason">Reason for the change</label><input id="eReason" class="fa-input" maxlength="500" placeholder="Optional; recorded with the change"/></div></div>' +
            '<div class="fa-error" id="eErr" style="margin-top:10px"></div></div>' +
            '<div class="fa-card__foot" style="justify-content:flex-end;gap:8px">' +
            (!isNew ? '<button type="button" class="fa-btn fa-btn--' + (n.active ? 'danger' : 'secondary') + '" id="eAct">' + (n.active ? 'Deactivate' : 'Activate') + '</button><span class="fa-spacer"></span>' : '') +
            (!isNew && !isSub ? '<button type="button" class="fa-btn fa-btn--secondary" id="eAdd">' + FA.icon('plus') + ' Add sub-category</button>' : '') +
            '<button type="button" class="fa-btn fa-btn--primary" id="eSave">' + (isNew ? 'Create' : 'Save') + '</button></div>';
        box.innerHTML = h;
        qs('eSave').onclick = function () {
            if (blockIfPending()) return;
            var b = this;
            var d = { id: n.id || 0, parentId: n.parentId || 0, rowVersion: n.rowVersion || 0, code: FA.val('eCode').toUpperCase(), name: FA.val('eName'), description: FA.val('eDesc'),
                      assetType: FA.val('eType'), method: FA.val('eMethod'), lifeYears: FA.val('eLife'), ratePct: FA.val('eRate'), residualPct: FA.val('eRes'),
                      revalueMonths: FA.val('eRev'), capThreshold: FA.val('eCap').replace(/,/g, ''), glCost: FA.val('eGlc'), glAccum: FA.val('eGla'), glExpense: FA.val('eGle'), reason: FA.val('eReason') };
            FA.busy(b, true);
            FA.call(PAGE, 'SaveNode', { json: JSON.stringify(d) }, function (r) {
                FA.busy(b, false);
                if (!r.success) { qs('eErr').textContent = r.message; return; }
                FA.toast(isNew ? 'Created.' : 'Saved.');
                if (d.parentId) open[d.parentId] = true;
                load(r.id);
            });
        };
        if (qs('eAdd')) qs('eAdd').onclick = function () { editor({ id: 0, parentId: n.id, active: true, parent: n }); };
        if (qs('eAct')) qs('eAct').onclick = function () {
            FA.reason({ title: (n.active ? 'Deactivate ' : 'Activate ') + n.name, min: 5, ok: n.active ? 'Deactivate' : 'Activate', danger: n.active,
                        message: n.active ? 'It will no longer be offered for new assets' + (isSub ? '' : ', nor will its sub-categories') + '. Existing assets keep it.' : 'It will be offered again for new assets.' },
                function (reason, m, done) {
                    FA.call(PAGE, 'SetActive', { id: n.id, active: !n.active, reason: reason }, function (r) { if (!r.success) { done(r.message); return; } done(); load(n.id); });
                });
        };
        if (isNew) qs('eCode').focus();
    }

    qs('cInactive').onchange = draw;
    qs('cExpand').onclick = function () {
        var all = nodes.every(function (c) { return open[c.id]; });
        nodes.forEach(function (c) { open[c.id] = !all; });
        qs('cExpand').textContent = all ? 'Expand all' : 'Collapse all';
        draw();
    };
    window.addEventListener('beforeunload', function (e) { if (changes().length) { e.preventDefault(); e.returnValue = ''; } });
    load(0);
})();
