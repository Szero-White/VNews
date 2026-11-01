/* Cover image field of the article form. Three clear states: the image stored now ("Ảnh hiện tại"), a newly chosen image ("Ảnh mới",
   previewed at once from the local file, then uploaded), and "Sẽ bỏ ảnh bìa khi lưu". The server value lives in the hidden CoverImageUrl
   input, so the normal form post saves exactly what the preview shows.

   Concurrency rule: every user action (choose, remove, undo) starts a new "generation". Work that belongs to an older generation (a slow
   preview, an upload that finishes late) is dropped, and its request is aborted, so a late response can never overwrite what the user
   chose afterwards. The Save buttons stay locked while an upload of the CURRENT generation is running. */
(function () {
  'use strict';

  var root = document.querySelector('[data-cover]');
  if (!root) return;

  var els = {
    img: root.querySelector('[data-cover-img]'),
    placeholder: root.querySelector('[data-cover-placeholder]'),
    placeholderText: root.querySelector('[data-cover-placeholder-text]'),
    state: root.querySelector('[data-cover-state]'),
    pickLabel: root.querySelector('[data-cover-pick-label]'),
    fileInput: document.getElementById('coverFile'),
    remove: root.querySelector('[data-cover-remove]'),
    undo: root.querySelector('[data-cover-undo]'),
    fileName: root.querySelector('[data-cover-file-name]'),
    status: document.getElementById('uploadStatus'),
    value: root.querySelector('input[name="CoverImageUrl"]'),
    form: root.closest('form')
  };

  // The limit comes from the server (ImageUploadLimits) through data attributes, so the form and the check can never disagree.
  var MAX_BYTES = parseInt(root.getAttribute('data-max-bytes'), 10) || 3 * 1024 * 1024;
  var LIMIT_LABEL = root.getAttribute('data-max-label') || '3 MB';
  var HINT = 'JPG, PNG hoặc WebP, tối đa ' + LIMIT_LABEL + '.';

  var storedUrl = els.value.value; // what the server stores now
  var generation = 0;
  var uploadController = null;

  // ---- display ----
  function render(imageUrl, label, placeholderText) {
    if (imageUrl) {
      els.img.src = imageUrl;
      els.img.hidden = false;
      els.placeholder.hidden = true;
    } else {
      els.img.removeAttribute('src');
      els.img.hidden = true;
      els.placeholder.hidden = false;
      els.placeholderText.textContent = placeholderText || 'Chưa có ảnh bìa';
    }
    els.state.textContent = label || '';
    els.state.hidden = !label;
    if (els.pickLabel) els.pickLabel.textContent = imageUrl ? 'Thay ảnh' : 'Chọn ảnh';
    if (els.remove) els.remove.hidden = !imageUrl;
  }

  function setSaveLocked(locked) {
    if (!els.form) return;
    els.form.querySelectorAll('button[type="submit"]').forEach(function (button) { button.disabled = locked; });
  }

  /** A data: URL (not blob:) keeps the page's Content-Security-Policy (img-src 'self' data: https:) untouched. */
  function readPreview(file) {
    return new Promise(function (resolve) {
      var reader = new FileReader();
      reader.onload = function () { resolve(String(reader.result)); };
      reader.onerror = function () { resolve(''); };
      reader.readAsDataURL(file);
    });
  }

  // ---- generations ----
  /** Invalidates everything in flight (preview or upload) and releases the Save buttons. Returns the new generation id. */
  function beginGeneration() {
    generation++;
    if (uploadController) { uploadController.abort(); uploadController = null; }
    setSaveLocked(false);
    return generation;
  }
  function isCurrent(id) { return id === generation; }

  function showStored() {
    els.fileInput.value = '';
    els.value.value = storedUrl;
    els.fileName.textContent = HINT;
    els.undo.hidden = true;
    render(storedUrl, storedUrl ? 'Ảnh hiện tại' : '');
  }

  // ---- upload ----
  async function upload(file) {
    var csrf = els.form.querySelector('input[name="__RequestVerificationToken"]').value;
    var data = new FormData();
    data.append('file', file);
    uploadController = new AbortController();
    var response = await fetch(root.getAttribute('data-upload-url'), {
      method: 'POST',
      headers: { 'RequestVerificationToken': csrf },
      body: data,
      signal: uploadController.signal
    });
    return { ok: response.ok, body: await response.json() };
  }

  async function onFileChosen(file) {
    els.status.textContent = '';
    if (file.size > MAX_BYTES) {
      els.status.textContent = 'Lỗi: ảnh lớn hơn ' + LIMIT_LABEL + '.';
      els.fileInput.value = '';
      return;
    }

    var id = beginGeneration();
    var preview = await readPreview(file);
    if (!isCurrent(id)) return;

    render(preview, 'Ảnh mới · đang tải lên…');
    els.fileName.textContent = file.name;
    els.undo.hidden = false;
    setSaveLocked(true);

    try {
      var result = await upload(file);
      if (!isCurrent(id)) return; // the user chose something else meanwhile: this answer is stale
      if (result.ok) {
        els.value.value = result.body.url;
        render(preview, 'Ảnh mới · lưu bài để áp dụng');
        els.status.textContent = 'Đã tải lên: ' + result.body.url;
      } else {
        els.status.textContent = 'Lỗi: ' + result.body.error;
        showStored();
      }
    } catch (err) {
      if (!isCurrent(id)) return; // aborted by a newer action, not a failure
      els.status.textContent = 'Lỗi tải lên.';
      showStored();
    } finally {
      if (isCurrent(id)) { uploadController = null; setSaveLocked(false); }
    }
  }

  if (!els.fileInput) return; // read-only view: the preview is all there is

  els.fileInput.addEventListener('change', function (e) {
    var file = e.target.files[0];
    if (file) onFileChosen(file);
  });

  els.remove.addEventListener('click', function () {
    beginGeneration();
    els.fileInput.value = '';
    els.value.value = '';
    els.fileName.textContent = HINT;
    els.status.textContent = '';
    els.undo.hidden = false;
    render('', 'Sẽ bỏ ảnh bìa khi lưu', 'Ảnh bìa sẽ bị bỏ khi bạn lưu bài');
  });

  els.undo.addEventListener('click', function () {
    beginGeneration();
    els.status.textContent = '';
    showStored();
  });
})();
