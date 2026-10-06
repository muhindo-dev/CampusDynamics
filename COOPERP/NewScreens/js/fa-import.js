/* Fixed Assets: import screen. ES5. */
(function () {
    'use strict';
    var B = window.FA_BOOT || {}, PAGE = 'AssetImport.aspx', esc = FA.esc, money = FA.money, qs = FA.qs;
    qs('iTpl').innerHTML = FA.icon('download') + ' Download the template';
    qs('iMax').textContent = money(B.maxRows);

    function step(n) {
        Array.prototype.forEach.call(document.querySelectorAll('#iSteps li'), function (li) {
            var s = parseInt(li.getAttribute('data-s'), 10);
            li.className = s < n ? 'is-done' : s === n ? 'is-now' : '';
        });
    }
    qs('iTpl').addEventListener('click', function () { step(2); });

    function recent(rows) {
        qs('iRecent').innerHTML = rows.length ? rows.map(function (r) {
            return '<tr><td>' + esc(r.file) + '<span class="fa-sub">' + esc(r.by) + ', ' + esc(r.at) + '</span></td><td class="fa-num">' + money(r.rows) + '</td>' +
                '<td><span class="fa-badge fa-badge--' + (r.status === 'COMMITTED' ? 'ok' : r.status === 'VALIDATED' ? (r.errors ? 'bad' : 'info') : 'neutral') + '">' + esc(r.statusLabel) + '</span>' +
                (r.status === 'VALIDATED' && r.errors ? '<span class="fa-sub">' + FA.plural(r.errors, 'error', 'errors') + '</span>' : '') + '</td></tr>';
        }).join('') : '<tr><td colspan="3" class="fa-empty">None yet.</td></tr>';
    }
    recent(B.recent || []);
    function reloadRecent() { FA.call(PAGE, 'Recent', {}, function (r) { if (r.success) recent(r.rows); }); }

    var drop = qs('iDrop'), file = qs('iFile');
    drop.onclick = function () { file.click(); };
    drop.addEventListener('dragover', function (e) { e.preventDefault(); drop.className = 'fa-drop is-over'; });
    drop.addEventListener('dragleave', function () { drop.className = 'fa-drop'; });
    drop.addEventListener('drop', function (e) { e.preventDefault(); drop.className = 'fa-drop'; if (e.dataTransfer.files[0]) send(e.dataTransfer.files[0]); });
    file.onchange = function () { if (file.files[0]) send(file.files[0]); file.value = ''; };

    function send(f) {
        step(3);
        qs('iReport').innerHTML = '<div class="fa-card"><div class="fa-loading">Checking ' + esc(f.name) + '. Every row is tried exactly as the import would save it, so a large file takes a minute.</div></div>';
        var fd = new FormData(); fd.append('file', f);
        var x = new XMLHttpRequest();
        x.open('POST', PAGE + '?ajax=validate', true);
        x.setRequestHeader('X-CSRF-Token', FA.token());
        x.timeout = 600000;
        x.onreadystatechange = function () {
            if (x.readyState !== 4) return;
            var r; try { r = JSON.parse(x.responseText); } catch (e) { r = { success: false, message: 'The server could not check the file.' }; }
            report(r, f.name);
            reloadRecent();
        };
        x.send(fd);
    }

    function report(r, name) {
        if (!r.success) { qs('iReport').innerHTML = '<div class="fa-notice fa-notice--bad">' + esc(r.message) + '</div>'; step(2); return; }
        var h = '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">3. Report for ' + esc(name) + '</div></div><div class="fa-card__body">';
        h += '<div class="fa-kpis"><div class="fa-kpi"><div class="fa-kpi__label">Rows</div><div class="fa-kpi__value">' + money(r.total) + '</div></div>' +
             '<div class="fa-kpi"><div class="fa-kpi__label">Ready</div><div class="fa-kpi__value">' + money(r.ok) + '</div></div>' +
             '<div class="fa-kpi' + (r.errorCount ? ' fa-kpi--warn' : '') + '"><div class="fa-kpi__label">Errors</div><div class="fa-kpi__value">' + money(r.errorCount) + '</div></div>' +
             '<div class="fa-kpi"><div class="fa-kpi__label">Warnings</div><div class="fa-kpi__value">' + money(r.warnings.length) + '</div></div></div>';
        if (r.errorCount) {
            h += '<div class="fa-notice fa-notice--bad">Fix these in the file and upload it again. Nothing has been saved.</div>' +
                 '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Row</th><th>Column</th><th>Value</th><th>Problem</th></tr></thead><tbody>' +
                 r.errors.map(function (e) { return '<tr><td>' + (e.row || '') + '</td><td class="fa-code">' + esc(e.column) + '</td><td>' + esc(e.value) + '</td><td>' + esc(e.message) + '</td></tr>'; }).join('') +
                 '</tbody></table></div>' + (r.errorCount > r.errors.length ? '<p class="fa-hint">First ' + r.errors.length + ' errors shown.</p>' : '');
        }
        if (r.warnings.length) {
            h += '<details style="margin-top:12px"' + (r.errorCount ? '' : ' open') + '><summary style="cursor:pointer;font-weight:600">' + FA.plural(r.warnings.length, 'warning', 'warnings') + ' (these do not stop the import)</summary>' +
                 '<table class="fa-table" style="margin-top:8px"><tbody>' + r.warnings.slice(0, 300).map(function (w) { return '<tr><td style="width:60px">' + (w.row ? 'Row ' + w.row : '') + '</td><td>' + esc(w.message) + '</td></tr>'; }).join('') + '</tbody></table></details>';
        }
        h += '</div>';
        if (r.canCommit) h += '<div class="fa-card__foot" style="justify-content:flex-end;gap:8px"><button type="button" class="fa-btn fa-btn--secondary" id="iAbandon">Discard</button>' +
                              '<button type="button" class="fa-btn fa-btn--primary" id="iCommit">Import ' + FA.plural(r.ok, 'asset', 'assets') + '</button></div>';
        h += '</div>';
        qs('iReport').innerHTML = h;
        if (r.canCommit) {
            step(4);
            qs('iCommit').onclick = function () {
                FA.confirm({ title: 'Import assets', message: 'Create ' + FA.plural(r.ok, 'asset', 'assets') + ' on the register? Each gets its acquisition or opening record and an asset number.', ok: 'Import' }, function () {
                    var b = qs('iCommit'); FA.busy(b, true, 'Importing');
                    FA.call(PAGE, 'Commit', { batchId: r.batchId }, function (x) {
                        FA.busy(b, false);
                        if (!x.success) { FA.toast(x.message, true); return; }
                        qs('iReport').innerHTML = '<div class="fa-notice fa-notice--ok">' + FA.plural(x.created, 'asset was', 'assets were') + ' added to the register. <a href="Assets.aspx" class="fa-btn--link">Open the register</a></div>';
                        step(5); reloadRecent();
                    });
                });
            };
            qs('iAbandon').onclick = function () {
                FA.call(PAGE, 'Abandon', { batchId: r.batchId }, function (x) { if (x.success) { qs('iReport').innerHTML = ''; step(2); reloadRecent(); } else FA.toast(x.message, true); });
            };
        }
    }
})();
