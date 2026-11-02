/* Article editor module: font family and font size for the selected text. Plugs into the core (body-editor.js) through window.VNewsRte.
   The server keeps only fixed class names (fs-8..fs-96, ff-...), see ArticleHtmlSanitizer. */
(function () {
  'use strict';

  var ctx = window.VNewsRte;
  if (!ctx || ctx.readOnly) return;

  var editor = ctx.editor;
  var el = ctx.el;
  var slot = ctx.slots.fonts;

  var FONTS = [['ff-arial', 'Arial (mặc định)'], ['ff-times', 'Times New Roman'], ['ff-georgia', 'Georgia'], ['ff-tahoma', 'Tahoma'], ['ff-verdana', 'Verdana'], ['ff-courier', 'Courier New']];
  var SIZE_HINTS = [12, 14, 16, 18, 20, 24, 28, 32, 40, 48]; // a few suggestions; any whole number in MIN_SIZE..MAX_SIZE can be typed
  var MIN_SIZE = 8, MAX_SIZE = 96, DEFAULT_SIZE = 18;

  /**
   * Font size / family for the selected text. The browser marks the selection with a temporary <font> tag (size 7 / face "vnmark"); each mark
   * becomes a <span class="fs-.."> or <span class="ff-..">, and any older span of the same kind inside it is cleaned so the newest choice wins.
   */
  function applyClass(prefix, cls) {
    if (ctx.isSource()) return;
    editor.focus(); ctx.restoreRange();
    var sel = window.getSelection();
    if (!sel.rangeCount || sel.isCollapsed) { ctx.note('Hãy bôi đen đoạn chữ cần đổi, rồi chọn lại.'); ctx.updateState(); return; }
    try { document.execCommand('styleWithCSS', false, false); } catch (e) { /* ignore */ }
    if (prefix === 'fs-') document.execCommand('fontSize', false, '7'); else document.execCommand('fontName', false, 'vnmark');
    var marks = editor.querySelectorAll(prefix === 'fs-' ? 'font[size="7"]' : 'font[face="vnmark"]');
    Array.prototype.forEach.call(marks, function (mark) {
      var span = document.createElement('span');
      span.className = cls;
      while (mark.firstChild) span.appendChild(mark.firstChild);
      mark.parentNode.replaceChild(span, mark);
      Array.prototype.forEach.call(span.querySelectorAll('span'), function (inner) { dropClass(inner, prefix); });
    });
    ctx.note('');
    ctx.sync(); ctx.updateState();
  }

  function dropClass(node, prefix) {
    var kept = (node.getAttribute('class') || '').split(/\s+/).filter(function (c) { return c && c.indexOf(prefix) !== 0; });
    if (kept.length) { node.setAttribute('class', kept.join(' ')); return; }
    while (node.firstChild) node.parentNode.insertBefore(node.firstChild, node); // nothing left on it: unwrap
    node.parentNode.removeChild(node);
  }

  function classAtCursor(prefix, fallback) {
    var sel = window.getSelection();
    var node = sel && sel.anchorNode;
    if (node && node.nodeType === 3) node = node.parentNode;
    while (node && node !== editor) {
      var found = (node.getAttribute && (node.getAttribute('class') || '').split(/\s+/).filter(function (c) { return c.indexOf(prefix) === 0; })[0]);
      if (found) return found;
      node = node.parentNode;
    }
    return fallback;
  }

  function makeSelect(label, options, initial, onPick) {
    var select = el('select', 'rte__select');
    select.setAttribute('aria-label', label);
    select.title = label + ' (bôi đen chữ rồi chọn)';
    options.forEach(function (o) { var opt = document.createElement('option'); opt.value = o[0]; opt.textContent = o[1]; select.appendChild(opt); });
    select.value = initial;
    select.addEventListener('change', function () { onPick(select.value); });
    slot.appendChild(select);
    return select;
  }

  /** Size box: type any size (8-96 px) and press Enter, use the - / + buttons or the arrow keys, or pick one of the suggestions. */
  function makeSizeBox() {
    var box = el('span', 'rte__size');
    function stepButton(text, title, delta) {
      var b = el('button', 'rte__step');
      b.type = 'button'; b.textContent = text; b.title = title; b.setAttribute('aria-label', title);
      b.addEventListener('mousedown', function (e) { e.preventDefault(); }); // keep the selection in the text
      b.addEventListener('click', function () { commit(current() + delta); });
      return b;
    }
    var input = el('input', 'rte__sizeinput');
    input.type = 'text'; input.inputMode = 'numeric'; input.autocomplete = 'off'; input.value = String(DEFAULT_SIZE);
    input.setAttribute('list', 'rte-size-hints');
    input.title = 'Cỡ chữ (px): nhập số từ ' + MIN_SIZE + ' đến ' + MAX_SIZE + ' rồi nhấn Enter, hoặc chọn gợi ý';
    input.setAttribute('aria-label', 'Cỡ chữ (px)');
    var hints = document.createElement('datalist');
    hints.id = 'rte-size-hints';
    SIZE_HINTS.forEach(function (n) { var o = document.createElement('option'); o.value = String(n); hints.appendChild(o); });

    function current() { var n = parseInt(input.value, 10); return isNaN(n) ? DEFAULT_SIZE : n; }
    function commit(n) {
      n = Math.min(MAX_SIZE, Math.max(MIN_SIZE, Math.round(n)));
      input.value = String(n);
      applyClass('fs-', 'fs-' + n);
    }
    input.addEventListener('change', function () {
      if (/^\s*\d+\s*$/.test(input.value)) commit(parseInt(input.value, 10)); else ctx.updateState(); // not a number: back to the size at the cursor
    });
    input.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') { e.preventDefault(); if (/^\s*\d+\s*$/.test(input.value)) commit(parseInt(input.value, 10)); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); commit(current() + 1); }
      else if (e.key === 'ArrowDown') { e.preventDefault(); commit(current() - 1); }
      else if (e.key === 'Escape') { editor.focus(); ctx.restoreRange(); }
    });
    box.appendChild(stepButton('−', 'Giảm cỡ chữ', -1));
    box.appendChild(input);
    box.appendChild(stepButton('+', 'Tăng cỡ chữ', 1));
    box.appendChild(hints);
    slot.appendChild(box);
    return { input: input, setDisabled: function (d) { input.disabled = d; box.querySelectorAll('button').forEach(function (b) { b.disabled = d; }); } };
  }

  var fontSelect = makeSelect('Kiểu chữ', FONTS, 'ff-arial', function (cls) { applyClass('ff-', cls); });
  var sizeBox = makeSizeBox();

  // the boxes show the font and size of the text at the cursor (not while a number is being typed)
  ctx.onState.push(function () {
    fontSelect.value = classAtCursor('ff-', 'ff-arial');
    if (document.activeElement !== sizeBox.input) {
      var size = parseInt(classAtCursor('fs-', 'fs-' + DEFAULT_SIZE).replace('fs-', ''), 10);
      sizeBox.input.value = String(isNaN(size) ? DEFAULT_SIZE : size);
    }
  });
  ctx.onSource.push(function (inSource) { fontSelect.disabled = inSource; sizeBox.setDisabled(inSource); });
})();
