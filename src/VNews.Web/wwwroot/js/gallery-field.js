/* "Ảnh trong bài": any number of picture boxes (none, one, five...). Each box has the same three actions as the cover: change (file picker),
   paste (clipboard button, or Ctrl+V while the box is selected) and remove. A box posts its address in a hidden GalleryImages input, so the
   normal form save stores exactly what the page shows, in the order shown.

   Same concurrency rule as the cover: every action on a box starts a new generation for THAT box; an upload that finishes late is dropped and
   its request aborted, so it can never overwrite what the user chose afterwards. The Save buttons stay locked while any upload is running. */
(function () {
  'use strict';

  var root = document.querySelector('[data-gallery]');
  if (!root) return;

  var list = root.querySelector('[data-gallery-list]');
  var template = root.querySelector('[data-gallery-template]');
  var addButton = root.querySelector('[data-gallery-add]');
  var emptyNote = root.querySelector('[data-gallery-empty]');
  var counter = root.querySelector('[data-gallery-count]');
  var status = root.querySelector('[data-gallery-status]');
  var form = root.closest('form');

  var MAX_BYTES = parseInt(root.getAttribute('data-max-bytes'), 10) || 3 * 1024 * 1024;
  var LIMIT_LABEL = root.getAttribute('data-max-label') || '3 MB';
  var MAX_IMAGES = parseInt(root.getAttribute('data-max-images'), 10) || 20;
  var IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
  var pending = 0;

  function slots() { return Array.prototype.slice.call(list.querySelectorAll('[data-gallery-slot]')); }
  function say(text) { if (status) status.textContent = text || ''; }

  function setSaveLocked(locked) {
    if (!form) return;
    form.querySelectorAll('button[type="submit"]').forEach(function (button) { button.disabled = locked; });
  }
  function beginUpload() { pending++; setSaveLocked(true); }
  function endUpload() { pending = Math.max(0, pending - 1); if (pending === 0) setSaveLocked(false); }

  // ---- list state: numbering, counter, the empty note, the add button ----
  function refresh() {
    var all = slots();
    all.forEach(function (slot, index) {
      var label = slot.querySelector('[data-slot-label]');
      if (label) label.textContent = 'Ảnh ' + (index + 1);
    });
    if (emptyNote) emptyNote.hidden = all.length > 0;
    if (counter) counter.textContent = all.length > 0 ? all.length + '/' + MAX_IMAGES + ' ảnh' : '';
    if (addButton) addButton.disabled = all.length >= MAX_IMAGES;
  }

  // ---- one box ----
  function parts(slot) {
    return {
      value: slot.querySelector('[data-slot-value]'),
      img: slot.querySelector('[data-slot-img]'),
      placeholder: slot.querySelector('[data-slot-placeholder]'),
      placeholderText: slot.querySelector('[data-slot-placeholder-text]'),
      state: slot.querySelector('[data-slot-state]'),
      pick: slot.querySelector('[data-slot-pick-label]'),
      file: slot.querySelector('[data-slot-file]')
    };
  }

  function render(slot, imageUrl, stateText) {
    var p = parts(slot);
    if (imageUrl) {
      p.img.src = imageUrl;
      p.img.hidden = false;
      p.placeholder.hidden = true;
    } else {
      p.img.removeAttribute('src');
      p.img.hidden = true;
      p.placeholder.hidden = false;
    }
    p.state.textContent = stateText || '';
    p.state.hidden = !stateText;
    if (p.pick) p.pick.textContent = imageUrl ? 'Thay ảnh' : 'Chọn ảnh';
    slot.classList.toggle('is-empty', !imageUrl);
  }

  function readPreview(file) {
    // A data: URL keeps the page's Content-Security-Policy (img-src 'self' data: https:) untouched.
    return new Promise(function (resolve) {
      var reader = new FileReader();
      reader.onload = function () { resolve(String(reader.result)); };
      reader.onerror = function () { resolve(''); };
      reader.readAsDataURL(file);
    });
  }

  async function upload(slot, file) {
    var token = form.querySelector('input[name="__RequestVerificationToken"]');
    var data = new FormData();
    data.append('file', file);
    slot.__controller = new AbortController();
    var response = await fetch(root.getAttribute('data-upload-url'), {
      method: 'POST',
      headers: { 'RequestVerificationToken': token ? token.value : '' },
      body: data,
      signal: slot.__controller.signal
    });
    return { ok: response.ok, body: await response.json() };
  }

  /** A new generation for this box: whatever was still running for it (preview or upload) is now stale. */
  function begin(slot) {
    slot.__generation = (slot.__generation || 0) + 1;
    if (slot.__controller) { slot.__controller.abort(); slot.__controller = null; }
    return slot.__generation;
  }

  async function setImage(slot, file) {
    say('');
    if (IMAGE_TYPES.indexOf(file.type) === -1) { say('Lỗi: chỉ dùng ảnh JPG, PNG hoặc WebP.'); return; }
    if (file.size > MAX_BYTES) { say('Lỗi: ảnh lớn hơn ' + LIMIT_LABEL + '.'); return; }

    var p = parts(slot);
    var before = p.value.value; // what this box held before: kept if the upload fails
    var id = begin(slot);
    var preview = await readPreview(file);
    if (slot.__generation !== id) return;

    render(slot, preview, 'Đang tải lên…');
    beginUpload();
    var counted = true;
    try {
      var result = await upload(slot, file);
      if (slot.__generation !== id) return; // a newer action on this box: this answer is stale
      if (result.ok) {
        p.value.value = result.body.url;
        render(slot, preview, 'Ảnh mới · lưu bài để áp dụng');
      } else {
        say('Lỗi: ' + result.body.error);
        render(slot, before, '');
      }
    } catch (err) {
      if (slot.__generation !== id) return; // aborted by a newer action, not a failure
      say('Lỗi tải lên.');
      render(slot, before, '');
    } finally {
      if (counted) { counted = false; endUpload(); }
      if (slot.__generation === id) slot.__controller = null;
    }
    if (p.file) p.file.value = '';
  }

  function acceptBlob(slot, blob) {
    if (IMAGE_TYPES.indexOf(blob.type) === -1) { say('Lỗi: chỉ dán được ảnh JPG, PNG hoặc WebP.'); return; }
    var extension = blob.type === 'image/jpeg' ? 'jpg' : blob.type.replace('image/', '');
    setImage(slot, new File([blob], 'anh-dan.' + extension, { type: blob.type }));
  }

  async function pasteFromClipboard(slot) {
    say('');
    if (!navigator.clipboard || !navigator.clipboard.read) {
      say('Trình duyệt không cho đọc clipboard. Hãy chọn ô này rồi nhấn Ctrl+V.');
      return;
    }
    try {
      var items = await navigator.clipboard.read();
      for (var i = 0; i < items.length; i++) {
        var type = items[i].types.filter(function (t) { return t.indexOf('image/') === 0; })[0];
        if (type) { acceptBlob(slot, await items[i].getType(type)); return; }
      }
      say('Clipboard chưa có ảnh. Hãy sao chép một ảnh rồi thử lại.');
    } catch (err) {
      say('Không đọc được clipboard (có thể bạn đã từ chối quyền). Hãy chọn ô này rồi nhấn Ctrl+V.');
    }
  }

  function removeSlot(slot) {
    begin(slot); // cancels its upload, if any (the upload's finally block releases the Save lock)
    slot.remove();
    refresh();
  }

  // ---- adding a box ----
  function addSlot() {
    if (!template || slots().length >= MAX_IMAGES) return null;
    var fragment = template.content.cloneNode(true);
    var slot = fragment.querySelector('[data-gallery-slot]');
    list.appendChild(fragment);
    setActive(slot);
    refresh();
    slot.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    return slot;
  }

  // ---- which box Ctrl+V goes to ----
  function setActive(slot) {
    slots().forEach(function (s) { s.classList.toggle('is-active', s === slot); });
  }
  function activeSlot() { return list.querySelector('[data-gallery-slot].is-active'); }

  // Clicking or tabbing into a box selects it; anywhere else clears the selection, so Ctrl+V falls back to the cover picture.
  document.addEventListener('focusin', function (e) {
    var slot = e.target.closest ? e.target.closest('[data-gallery-slot]') : null;
    setActive(slot && list.contains(slot) ? slot : null);
  });
  document.addEventListener('mousedown', function (e) {
    var slot = e.target.closest ? e.target.closest('[data-gallery-slot]') : null;
    setActive(slot && list.contains(slot) ? slot : null);
    if (slot) slot.focus({ preventScroll: true });
  });

  document.addEventListener('paste', function (e) {
    var slot = activeSlot();
    if (!slot) return; // the cover handles it
    var items = e.clipboardData ? Array.prototype.slice.call(e.clipboardData.items || []) : [];
    var imageItem = items.filter(function (item) { return item.kind === 'file' && item.type.indexOf('image/') === 0; })[0];
    if (!imageItem) return; // text pasted into the form keeps working as usual
    var blob = imageItem.getAsFile();
    if (!blob) return;
    e.preventDefault();
    acceptBlob(slot, blob);
  });

  // ---- events from the boxes ----
  list.addEventListener('click', function (e) {
    var slot = e.target.closest('[data-gallery-slot]');
    if (!slot) return;
    if (e.target.closest('[data-slot-paste]')) pasteFromClipboard(slot);
    else if (e.target.closest('[data-slot-remove]')) removeSlot(slot);
  });
  list.addEventListener('change', function (e) {
    if (!e.target.matches('[data-slot-file]')) return;
    var slot = e.target.closest('[data-gallery-slot]');
    var file = e.target.files && e.target.files[0];
    if (slot && file) setImage(slot, file);
  });

  if (form) {
    // after a save made in place the boxes show stored pictures: drop the "Ảnh mới · lưu bài" notes
    form.addEventListener('vnews:saved', function () {
      slots().forEach(function (slot) { render(slot, parts(slot).value.value, ''); });
    });
  }

  if (addButton) addButton.addEventListener('click', addSlot);
  refresh();
})();
