/* Article editor module: pictures at the cursor, each with a caption line. A new picture (chosen, or pasted with Ctrl+V while the cursor is in
   the text) is uploaded and inserted as <figure><img><figcaption>, with the cursor in the caption so the source or description is typed at
   once. A picture that has no caption yet: click it, then the caption button. Empty captions are not saved.
   Plugs into the core (body-editor.js) through window.VNewsRte. */
(function () {
  'use strict';

  var ctx = window.VNewsRte;
  if (!ctx) return;

  var editor = ctx.editor;
  var area = ctx.area;
  var form = ctx.form;
  var IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
  var MAX_BYTES = parseInt(area.getAttribute('data-max-bytes'), 10) || 3 * 1024 * 1024;
  var LIMIT_LABEL = area.getAttribute('data-max-label') || '3 MB';
  var UPLOAD_URL = area.getAttribute('data-upload-url');
  var selectedImage = null;
  var pending = 0;

  // ---- keeping the figures tidy (also in the read-only view) ----
  /** Hint on empty captions; a figure whose picture was deleted goes away with it. */
  function refreshFigures() {
    Array.prototype.forEach.call(editor.querySelectorAll('figure'), function (fig) {
      if (!fig.querySelector('img')) { if (fig.parentNode) fig.parentNode.removeChild(fig); return; }
      var cap = fig.querySelector('figcaption');
      if (cap) { if (cap.textContent.trim()) cap.removeAttribute('data-empty'); else cap.setAttribute('data-empty', ''); }
    });
  }
  ctx.beforeSync.push(refreshFigures);
  ctx.onReady.push(refreshFigures);

  // the saved HTML carries no selection outline, no hint attribute and no empty caption lines
  ctx.cleaners.push(function (clone) {
    Array.prototype.forEach.call(clone.querySelectorAll('figcaption'), function (cap) {
      cap.removeAttribute('data-empty');
      if (!cap.textContent.trim()) cap.parentNode.removeChild(cap);
    });
    Array.prototype.forEach.call(clone.querySelectorAll('img.is-selected'), function (img) {
      img.classList.remove('is-selected');
      if (!img.getAttribute('class')) img.removeAttribute('class');
    });
  });

  if (ctx.readOnly) return;

  var picker = ctx.el('input');
  picker.type = 'file'; picker.accept = IMAGE_TYPES.join(','); picker.hidden = true;
  ctx.wrap.appendChild(picker);

  // ---- selecting a picture ----
  function selectImage(img) {
    if (selectedImage) selectedImage.classList.remove('is-selected');
    selectedImage = img;
    if (img) img.classList.add('is-selected');
  }
  editor.addEventListener('click', function (e) { selectImage(e.target && e.target.nodeName === 'IMG' ? e.target : null); });

  // ---- captions ----
  function focusCaption(fig) {
    var cap = fig.querySelector('figcaption');
    if (!cap) { cap = document.createElement('figcaption'); cap.innerHTML = '<br>'; fig.appendChild(cap); }
    refreshFigures();
    var range = document.createRange();
    range.selectNodeContents(cap); range.collapse(false);
    var sel = window.getSelection();
    sel.removeAllRanges(); sel.addRange(range);
    ctx.saveRange();
    cap.scrollIntoView({ block: 'nearest' });
  }

  /** A new picture arrives as <figure><img><figcaption>, with the cursor in the caption so the writer can type it straight away. */
  function insertFigure(url) {
    var safe = url.replace(/"/g, '&quot;');
    document.execCommand('insertHTML', false, '<figure><img src="' + safe + '" alt=""><figcaption><br></figcaption></figure>');
    var fig = null;
    Array.prototype.forEach.call(editor.querySelectorAll('figure img'), function (img) { if (img.getAttribute('src') === url) fig = img.closest('figure'); });
    if (!fig) return;
    if (!fig.nextElementSibling) { // somewhere to keep typing after the picture
      var next = document.createElement('p'); next.innerHTML = '<br>';
      fig.parentNode.insertBefore(next, fig.nextSibling);
    }
    focusCaption(fig);
  }

  /** The caption button: puts the picture you clicked (or are in) into a figure and moves the cursor to its caption. */
  function captionAction() {
    if (ctx.isSource()) return;
    editor.focus();
    var fig = null;
    if (selectedImage && editor.contains(selectedImage)) {
      fig = selectedImage.closest('figure');
      if (!fig) {
        var parent = selectedImage.parentNode;
        fig = document.createElement('figure');
        if (parent.nodeName === 'P') {
          var onlyPicture = !parent.textContent.trim() && parent.querySelectorAll('img').length === 1;
          fig.appendChild(selectedImage);
          if (onlyPicture) parent.parentNode.replaceChild(fig, parent); else parent.parentNode.insertBefore(fig, parent.nextSibling);
        } else {
          parent.insertBefore(fig, selectedImage);
          fig.appendChild(selectedImage);
        }
      }
    } else {
      var sel = window.getSelection();
      var node = sel && sel.anchorNode;
      while (node && node !== editor) { if (node.nodeName === 'FIGURE') { fig = node; break; } node = node.parentNode; }
    }
    if (!fig) { ctx.note('Bấm vào ảnh cần chú thích, rồi bấm nút này.'); return; }
    focusCaption(fig);
    ctx.sync();
  }

  // Enter in a caption leaves the figure instead of starting a paragraph inside it.
  editor.addEventListener('keydown', function (e) {
    if (e.key !== 'Enter') return;
    var sel = window.getSelection();
    var node = sel && sel.anchorNode;
    while (node && node !== editor && node.nodeName !== 'FIGCAPTION') node = node.parentNode;
    if (!node || node === editor) return;
    e.preventDefault();
    var fig = node.closest('figure');
    var next = fig.nextElementSibling;
    if (!next || next.nodeName !== 'P') { next = document.createElement('p'); next.innerHTML = '<br>'; fig.parentNode.insertBefore(next, fig.nextSibling); }
    var range = document.createRange();
    range.selectNodeContents(next); range.collapse(true);
    sel.removeAllRanges(); sel.addRange(range);
  });

  // ---- uploading ----
  function lockSave(locked) {
    if (!form) return;
    form.querySelectorAll('button[type="submit"]').forEach(function (b) { b.disabled = locked; });
  }

  async function insertImage(file) {
    if (IMAGE_TYPES.indexOf(file.type) === -1) { ctx.note('Lỗi: chỉ dùng ảnh JPG, PNG hoặc WebP.'); return; }
    if (file.size > MAX_BYTES) { ctx.note('Lỗi: ảnh lớn hơn ' + LIMIT_LABEL + '.'); return; }
    var token = form && form.querySelector('input[name="__RequestVerificationToken"]');
    var data = new FormData(); data.append('file', file);
    ctx.note('Đang tải ảnh lên…');
    pending++; lockSave(true);
    try {
      var response = await fetch(UPLOAD_URL, { method: 'POST', headers: { 'RequestVerificationToken': token ? token.value : '' }, body: data });
      var body = await response.json();
      if (!response.ok) { ctx.note('Lỗi: ' + body.error); return; }
      editor.focus(); ctx.restoreRange();
      insertFigure(body.url);
      ctx.sync();
      ctx.note('Đã chèn ảnh. Nhập chú thích ngay bên dưới ảnh (để trống thì không hiện).');
    } catch (err) {
      ctx.note('Lỗi tải ảnh lên.');
    } finally {
      pending = Math.max(0, pending - 1);
      if (pending === 0) lockSave(false);
    }
  }

  picker.addEventListener('change', function () {
    if (picker.files && picker.files[0]) insertImage(picker.files[0]);
    picker.value = '';
  });

  if (UPLOAD_URL) {
    ctx.addButton('image', '', 'Chèn ảnh vào vị trí con trỏ', function () { picker.click(); }, ctx.slots.figures);

    // Ctrl+V with a picture on the clipboard and the cursor in the text: the picture belongs here, not to the cover.
    ctx.pasteImage = function (e) {
      var image = Array.prototype.filter.call(e.clipboardData.items || [], function (item) { return item.kind === 'file' && item.type.indexOf('image/') === 0; })[0];
      if (!image) return false;
      e.preventDefault(); e.stopPropagation();
      var file = image.getAsFile();
      if (file) insertImage(new File([file], 'anh-dan.' + (file.type === 'image/jpeg' ? 'jpg' : file.type.replace('image/', '')), { type: file.type }));
      return true;
    };
  }
  ctx.addButton('caption', '', 'Chú thích cho ảnh (bấm vào ảnh trước)', captionAction, ctx.slots.figures);
})();
