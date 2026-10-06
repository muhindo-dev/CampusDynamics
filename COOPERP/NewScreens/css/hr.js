/* HR module: shared confirmation dialog in the hr-modal style (replaces the browser's confirm()).
   hrConfirm({ title, message, ok, danger }, function () { ...runs when confirmed... }); */
(function () {
    function esc(s) {
        return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }
    window.hrConfirm = function (opt, onOk) {
        opt = opt || {};
        var wrap = document.createElement('div');
        wrap.className = 'hr-modal is-open';
        wrap.setAttribute('role', 'dialog');
        wrap.setAttribute('aria-modal', 'true');
        wrap.innerHTML =
            '<div class="hr-modal__box" style="width:440px;">' +
              '<div class="hr-modal__head"><span>' + esc(opt.title || 'Please confirm') + '</span>' +
                '<button type="button" class="hr-modal__close" aria-label="Close" data-x="1">' +
                  '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>' +
                '</button></div>' +
              '<div class="hr-modal__body">' + esc(opt.message || '').replace(/\n/g, '<br/>') + '</div>' +
              '<div class="hr-modal__foot">' +
                '<button type="button" class="hr-btn hr-btn--secondary" data-x="1">Cancel</button>' +
                '<button type="button" class="hr-btn ' + (opt.danger ? 'hr-btn--danger' : 'hr-btn--primary') + '" data-ok="1">' + esc(opt.ok || 'Confirm') + '</button>' +
              '</div>' +
            '</div>';
        function close() {
            document.removeEventListener('keydown', onKey);
            if (wrap.parentNode) wrap.parentNode.removeChild(wrap);
        }
        function onKey(e) { if (e.key === 'Escape') close(); }
        wrap.addEventListener('click', function (e) {
            var t = e.target.closest ? e.target.closest('[data-x],[data-ok]') : null;
            if (e.target === wrap || (t && t.hasAttribute('data-x'))) { close(); return; }
            if (t && t.hasAttribute('data-ok')) { close(); if (typeof onOk === 'function') onOk(); }
        });
        document.addEventListener('keydown', onKey);
        document.body.appendChild(wrap);
        var ok = wrap.querySelector('[data-ok]');
        if (ok && ok.focus) ok.focus();
    };
})();
