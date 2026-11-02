/* Article editor module: links. A small dialog under the toolbar (not a browser prompt), Ctrl+K, and "remove link".
   Plugs into the core (body-editor.js) through window.VNewsRte. */
(function () {
  'use strict';

  var ctx = window.VNewsRte;
  if (!ctx || ctx.readOnly) return;

  var editor = ctx.editor;
  var pop = ctx.pop;
  var el = ctx.el;
  var slot = ctx.slots.link;

  function openLinkDialog() {
    if (ctx.isSource()) return;
    ctx.saveRange();
    var existing = null;
    var sel = window.getSelection();
    var node = sel && sel.anchorNode;
    while (node && node !== editor) { if (node.nodeName === 'A') { existing = node; break; } node = node.parentNode; }
    pop.innerHTML = '';
    var label = el('label', 'rte__pop-label'); label.textContent = 'Địa chỉ liên kết';
    var input = el('input', 'app-input'); input.type = 'url'; input.placeholder = 'https://...'; input.value = existing ? existing.getAttribute('href') : '';
    label.appendChild(input);
    var ok = el('button', 'btn btn--primary btn--sm'); ok.type = 'button'; ok.textContent = 'Áp dụng';
    var cancel = el('button', 'btn btn--ghost btn--sm'); cancel.type = 'button'; cancel.textContent = 'Hủy';
    pop.appendChild(label); pop.appendChild(ok); pop.appendChild(cancel);
    pop.hidden = false; input.focus(); input.select();

    function close() { pop.hidden = true; editor.focus(); ctx.restoreRange(); }
    function apply() {
      var url = input.value.trim();
      if (!url) { close(); return; }
      if (!/^https?:\/\//i.test(url)) url = 'https://' + url;
      close();
      if (window.getSelection().isCollapsed && !existing) ctx.exec('insertHTML', '<a href="' + ctx.escapeHtml(url).replace(/"/g, '&quot;') + '">' + ctx.escapeHtml(url) + '</a>');
      else ctx.exec('createLink', url);
    }
    ok.addEventListener('click', apply);
    cancel.addEventListener('click', close);
    input.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') { e.preventDefault(); apply(); }
      if (e.key === 'Escape') { e.preventDefault(); close(); }
    });
  }

  ctx.addButton('link', '', 'Chèn liên kết (Ctrl+K)', openLinkDialog, slot);
  ctx.addButton('unlink', '', 'Bỏ liên kết', function () { ctx.exec('unlink'); }, slot);
  editor.addEventListener('keydown', function (e) {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); openLinkDialog(); }
  });
})();
