/* Visual editor for the article text: the core. It builds the editor, the toolbar and the status bar, keeps the posted <textarea data-rte>
   in step with what is typed, and offers the shared context `window.VNewsRte` that the feature modules load after this file plug into:
     body-editor-fonts.js    font family and size
     body-editor-link.js     link dialog
     body-editor-figures.js  pictures at the cursor and their captions
     body-editor-draft.js    unsaved draft kept in this browser
   The <textarea> stays in the page: it is what the form posts and the fallback without script. The server sanitizes whatever is saved, so
   this is a convenience, not a security boundary. */
(function () {
  'use strict';

  var area = document.querySelector('textarea[data-rte]');
  if (!area) return;

  var readOnly = area.hasAttribute('readonly');
  var form = area.form;
  var BLOCK = /<\s*(p|h[1-6]|ul|ol|li|blockquote|div|figure|table|img|br)\b/i;

  function escapeHtml(text) { return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }

  /** Mirror of ArticleBodyFormatter on the server: plain lines become paragraphs, HTML stays as it is. */
  function toHtml(text) {
    if (!text || !text.trim()) return '';
    if (BLOCK.test(text)) return text;
    return text.replace(/\r\n?/g, '\n').trim().split(/\n\s*\n/)
      .map(function (p) { return p.trim(); }).filter(Boolean)
      .map(function (p) { return '<p>' + escapeHtml(p).replace(/\n/g, '<br>') + '</p>'; }).join('');
  }

  function el(tag, cls) { var n = document.createElement(tag); if (cls) n.className = cls; return n; }

  // ---- icons (24px, drawn with strokes) ----
  function icon(body) { return '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + body + '</svg>'; }
  var ICONS = {
    undo: icon('<path d="M9 14 4 9l5-5"/><path d="M4 9h10a6 6 0 0 1 0 12h-3"/>'),
    redo: icon('<path d="m15 14 5-5-5-5"/><path d="M20 9H10a6 6 0 0 0 0 12h3"/>'),
    bold: icon('<path d="M7 5h6a3.5 3.5 0 0 1 0 7H7zM7 12h7a3.5 3.5 0 0 1 0 7H7z"/>'),
    italic: icon('<path d="M10 5h8M6 19h8M14 5 10 19"/>'),
    underline: icon('<path d="M7 4v7a5 5 0 0 0 10 0V4M5 20h14"/>'),
    ul: icon('<path d="M9 6h11M9 12h11M9 18h11M4.5 6h.01M4.5 12h.01M4.5 18h.01"/>'),
    ol: icon('<path d="M10 6h10M10 12h10M10 18h10"/><text x="2" y="8" font-size="7" font-weight="700" stroke="none" fill="currentColor">1</text><text x="2" y="14" font-size="7" font-weight="700" stroke="none" fill="currentColor">2</text><text x="2" y="20" font-size="7" font-weight="700" stroke="none" fill="currentColor">3</text>'),
    quote: icon('<path d="M7 7h4v5a4 4 0 0 1-4 4M15 7h4v5a4 4 0 0 1-4 4"/>'),
    link: icon('<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>'),
    unlink: icon('<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/><path d="M4 4l16 16"/>'),
    caption: icon('<rect x="3" y="3" width="18" height="11" rx="2"/><path d="M4 18h16M4 21.5h9"/>'),
    image: icon('<rect x="3" y="4" width="18" height="16" rx="2"/><path d="m3 16 5-5 4 4 3-3 6 6"/><path d="M9 9h.01"/>'),
    clear: icon('<path d="M6 5h12M12 5v6M9 19h6M4 4l16 16"/>'),
    code: icon('<path d="m8 8-4 4 4 4M16 8l4 4-4 4"/>'),
    full: icon('<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>'),
    exit: icon('<path d="M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5"/>')
  };

  // ---- build ----
  var wrap = el('div', 'rte' + (readOnly ? ' rte--readonly' : ''));
  var bar = el('div', 'rte__bar'); bar.setAttribute('role', 'toolbar'); bar.setAttribute('aria-label', 'Định dạng nội dung');
  var pop = el('div', 'rte__pop'); pop.hidden = true;
  var editor = el('div', 'rte__area article-detail__body');
  var source = el('textarea', 'rte__source'); source.hidden = true; source.spellcheck = false; source.readOnly = readOnly; source.setAttribute('aria-label', 'Mã HTML của nội dung');
  var status = el('div', 'rte__status');
  var statusCount = el('span', 'rte__count');
  var statusNote = el('span', 'rte__note'); statusNote.setAttribute('role', 'status');
  status.appendChild(statusCount); status.appendChild(statusNote);

  editor.setAttribute('role', 'textbox');
  editor.setAttribute('aria-multiline', 'true');
  editor.setAttribute('aria-label', 'Nội dung bài viết');
  editor.innerHTML = toHtml(area.value) || '<p><br></p>';
  if (!readOnly) editor.contentEditable = 'true';

  // Where the feature modules put their buttons, so the toolbar keeps one fixed order whatever the loading order.
  var slots = { fonts: el('span', 'rte__slot'), link: el('span', 'rte__slot'), figures: el('span', 'rte__slot') };

  var controls = {}; // name -> button, for the "is on" state and for disabling in HTML view
  function addButton(name, label, title, handler, slot) {
    var b = el('button', 'rte__btn');
    b.type = 'button'; b.innerHTML = ICONS[name] || label; b.title = title; b.setAttribute('aria-label', title);
    b.addEventListener('mousedown', function (e) { e.preventDefault(); }); // keep the selection in the text
    b.addEventListener('click', handler);
    controls[name] = b; (slot || bar).appendChild(b); return b;
  }
  function group() { var g = el('span', 'rte__sep'); g.setAttribute('aria-hidden', 'true'); bar.appendChild(g); }

  var blockSelect;
  if (!readOnly) {
    addButton('undo', '', 'Hoàn tác (Ctrl+Z)', function () { exec('undo'); });
    addButton('redo', '', 'Làm lại (Ctrl+Y)', function () { exec('redo'); });
    group();
    blockSelect = el('select', 'rte__select');
    blockSelect.setAttribute('aria-label', 'Kiểu đoạn');
    [['p', 'Đoạn văn'], ['h2', 'Tiêu đề mục'], ['h3', 'Tiêu đề nhỏ']].forEach(function (o) {
      var opt = document.createElement('option'); opt.value = o[0]; opt.textContent = o[1]; blockSelect.appendChild(opt);
    });
    blockSelect.addEventListener('change', function () { exec('formatBlock', blockSelect.value); });
    bar.appendChild(blockSelect);
    bar.appendChild(slots.fonts);
    group();
    addButton('bold', '', 'Chữ đậm (Ctrl+B)', function () { exec('bold'); });
    addButton('italic', '', 'Chữ nghiêng (Ctrl+I)', function () { exec('italic'); });
    addButton('underline', '', 'Gạch chân (Ctrl+U)', function () { exec('underline'); });
    group();
    addButton('ul', '', 'Danh sách gạch đầu dòng', function () { exec('insertUnorderedList'); });
    addButton('ol', '', 'Danh sách đánh số', function () { exec('insertOrderedList'); });
    addButton('quote', '', 'Trích dẫn', function () { exec('formatBlock', isIn('BLOCKQUOTE') ? 'p' : 'blockquote'); });
    group();
    bar.appendChild(slots.link);
    bar.appendChild(slots.figures);
    group();
    addButton('clear', '', 'Xóa định dạng', function () { exec('removeFormat'); exec('formatBlock', 'p'); });
    bar.appendChild(el('span', 'rte__spacer'));
  }
  var htmlButton = addButton('code', '', 'Xem / sửa mã HTML', toggleSource);
  var fullButton = addButton('full', '', 'Toàn màn hình', toggleFull);

  wrap.appendChild(bar);
  if (!readOnly) wrap.appendChild(pop);
  wrap.appendChild(editor);
  wrap.appendChild(source);
  wrap.appendChild(status);
  area.parentNode.insertBefore(wrap, area);
  area.hidden = true;
  area.setAttribute('aria-hidden', 'true');

  // Enter makes <p> (not <div>), so the saved HTML is made of real paragraphs.
  try { document.execCommand('defaultParagraphSeparator', false, 'p'); } catch (e) { /* older browsers: the server tidies it */ }

  // ---- state ----
  var lastRange = null;
  var busy = 0;
  var inSource = false;

  function note(text) { statusNote.textContent = text || ''; }
  function isSource() { return inSource; }

  /** The editor's HTML for saving: the modules strip editor-only state (selection outline, hints, empty captions) from the copy. */
  function cleanHtml() {
    var clone = editor.cloneNode(true);
    ctx.cleaners.forEach(function (clean) { clean(clone); });
    return clone.innerHTML;
  }

  function sync() {
    ctx.beforeSync.forEach(function (fn) { fn(); });
    if (inSource) { area.value = source.value; }
    else {
      var text = (editor.textContent || '').trim();
      area.value = (text || editor.querySelector('img')) ? cleanHtml() : '';
    }
    updateCount();
    ctx.afterSync.forEach(function (fn) { fn(); });
  }

  /** Replaces the whole text (used when a saved draft is brought back). */
  function setContent(html) {
    editor.innerHTML = toHtml(html) || '<p><br></p>';
    sync();
  }

  function exec(cmd, value) {
    if (inSource) return;
    editor.focus();
    restoreRange();
    document.execCommand(cmd, false, value === undefined ? null : value);
    sync();
    updateState();
  }

  function isIn(tag) {
    var sel = window.getSelection();
    var node = sel && sel.anchorNode;
    while (node && node !== editor) { if (node.nodeName === tag) return true; node = node.parentNode; }
    return false;
  }

  function saveRange() {
    var sel = window.getSelection();
    if (sel && sel.rangeCount && editor.contains(sel.anchorNode)) lastRange = sel.getRangeAt(0).cloneRange();
  }
  function restoreRange() {
    if (!lastRange) return;
    var sel = window.getSelection();
    sel.removeAllRanges(); sel.addRange(lastRange);
  }

  // ---- which tools are "on" at the cursor ----
  function updateState() {
    if (readOnly || inSource) return;
    [['bold', 'bold'], ['italic', 'italic'], ['underline', 'underline'], ['ul', 'insertUnorderedList'], ['ol', 'insertOrderedList']].forEach(function (pair) {
      var on = false;
      try { on = document.queryCommandState(pair[1]); } catch (e) { /* not supported */ }
      controls[pair[0]].classList.toggle('is-on', on);
      controls[pair[0]].setAttribute('aria-pressed', String(on));
    });
    controls.quote.classList.toggle('is-on', isIn('BLOCKQUOTE'));
    var block = 'p';
    if (isIn('H2')) block = 'h2'; else if (isIn('H3')) block = 'h3';
    blockSelect.value = block;
    ctx.onState.forEach(function (fn) { fn(); });
  }
  document.addEventListener('selectionchange', function () {
    var sel = window.getSelection();
    if (sel && sel.anchorNode && editor.contains(sel.anchorNode)) { saveRange(); updateState(); }
  });

  // ---- counts ----
  function updateCount() {
    var text = inSource ? (source.value.replace(/<[^>]*>/g, ' ')) : (editor.innerText || '');
    var words = (text.trim().match(/\S+/g) || []).length;
    var minutes = Math.max(1, Math.round(words / 220));
    statusCount.textContent = words + ' từ · ' + text.replace(/\s/g, '').length + ' ký tự · khoảng ' + minutes + ' phút đọc';
  }

  // ---- paste: a picture is offered to the figures module first, then formatted content to the paste module; plain text keeps its paragraphs ----
  editor.addEventListener('paste', function (e) {
    var data = e.clipboardData;
    if (!data) return;
    if (ctx.pasteImage && ctx.pasteImage(e)) return;
    if (ctx.pasteHtml && ctx.pasteHtml(e)) return;
    var text = data.getData('text/plain');
    if (!text) return;
    e.preventDefault();
    document.execCommand('insertHTML', false, toHtml(text) || escapeHtml(text));
    sync();
  });

  editor.addEventListener('input', sync);
  editor.addEventListener('blur', sync);

  // ---- HTML view ----
  function toggleSource() {
    inSource = !inSource;
    if (inSource) {
      source.value = (editor.textContent || '').trim() || editor.querySelector('img') ? cleanHtml() : '';
      source.hidden = false; editor.hidden = true; pop.hidden = true;
    } else {
      editor.innerHTML = toHtml(source.value) || '<p><br></p>';
      source.hidden = true; editor.hidden = false;
    }
    wrap.classList.toggle('rte--source', inSource);
    htmlButton.classList.toggle('is-on', inSource);
    Object.keys(controls).forEach(function (name) { if (name !== 'code' && name !== 'full') controls[name].disabled = inSource; });
    if (blockSelect) blockSelect.disabled = inSource;
    ctx.onSource.forEach(function (fn) { fn(inSource); });
    sync();
  }
  source.addEventListener('input', sync);

  // ---- full screen ----
  function toggleFull() {
    var on = wrap.classList.toggle('rte--full');
    document.documentElement.classList.toggle('rte-lock', on);
    fullButton.innerHTML = on ? ICONS.exit : ICONS.full;
    fullButton.title = on ? 'Thu nhỏ (Esc)' : 'Toàn màn hình';
    fullButton.setAttribute('aria-label', fullButton.title);
  }
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && wrap.classList.contains('rte--full') && pop.hidden) toggleFull();
  });

  if (form) form.addEventListener('submit', sync, true); // before the browser validates and posts

  // ---- what the feature modules (loaded after this file) can use ----
  var ctx = window.VNewsRte = {
    area: area, form: form, readOnly: readOnly, wrap: wrap, bar: bar, pop: pop, editor: editor, slots: slots, icons: ICONS,
    el: el, escapeHtml: escapeHtml, toHtml: toHtml, addButton: addButton, group: group,
    exec: exec, sync: sync, note: note, isSource: isSource, saveRange: saveRange, restoreRange: restoreRange, updateState: updateState, setContent: setContent,
    // hooks the modules fill in
    beforeSync: [], afterSync: [], cleaners: [], onState: [], onSource: [], onReady: [], pasteImage: null, pasteHtml: null,
    /** +1 when an upload or import starts, -1 when it ends: the form's save buttons stay off while any is running. */
    work: function (delta) {
      busy = Math.max(0, busy + delta);
      if (form) form.querySelectorAll('button[type="submit"]').forEach(function (b) { b.disabled = busy > 0; });
    }
  };

  // Once every module has been loaded (scripts at the end of the page run before DOMContentLoaded), do the first update.
  function finish() {
    ctx.onReady.forEach(function (fn) { fn(); });
    updateCount();
    updateState();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', finish); else setTimeout(finish, 0);
})();
