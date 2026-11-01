/* Cover image field of the article form. Keeps three clear states: the image stored now ("Ảnh hiện tại"), a newly chosen
   image ("Ảnh mới", previewed at once from the local file, then uploaded), and "Sẽ bỏ ảnh bìa khi lưu". The server value
   lives in the hidden CoverImageUrl input, so the normal form post saves exactly what the preview shows. */
(function () {
  'use strict';
  var root = document.querySelector('[data-cover]');
  if (!root) return;

  var img = root.querySelector('[data-cover-img]');
  var placeholder = root.querySelector('[data-cover-placeholder]');
  var placeholderText = root.querySelector('[data-cover-placeholder-text]');
  var state = root.querySelector('[data-cover-state]');
  var pickLabel = root.querySelector('[data-cover-pick-label]');
  var fileInput = document.getElementById('coverFile');
  var removeBtn = root.querySelector('[data-cover-remove]');
  var undoBtn = root.querySelector('[data-cover-undo]');
  var fileName = root.querySelector('[data-cover-file-name]');
  var status = document.getElementById('uploadStatus');
  var value = root.querySelector('input[name="CoverImageUrl"]');
  var form = root.closest('form');
  var HINT = 'JPG, PNG hoặc WebP, tối đa 5 MB.';
  var MAX = 5 * 1024 * 1024;

  var saved = value.value;      // what the server stores now (or the last successful upload)
  var token = 0;                // guards against a slow preview of an older choice overwriting a newer one

  function render(current, label, text) {
    if (current) { img.src = current; img.hidden = false; placeholder.hidden = true; }
    else { img.removeAttribute('src'); img.hidden = true; placeholder.hidden = false; placeholderText.textContent = text || 'Chưa có ảnh bìa'; }
    state.textContent = label || '';
    state.hidden = !label;
    if (pickLabel) pickLabel.textContent = current ? 'Thay ảnh' : 'Chọn ảnh';
    if (removeBtn) removeBtn.hidden = !current;
  }
  // A data: URL (not blob:) keeps the page's Content-Security-Policy (img-src 'self' data: https:) untouched.
  function readPreview(file) {
    return new Promise(function (resolve) {
      var reader = new FileReader();
      reader.onload = function () { resolve(String(reader.result)); };
      reader.onerror = function () { resolve(''); };
      reader.readAsDataURL(file);
    });
  }
  function setBusy(busy) {
    if (!form) return;
    form.querySelectorAll('button[type="submit"]').forEach(function (b) { b.disabled = busy; });
  }

  if (!fileInput) return; // read-only view: the preview is all there is

  fileInput.addEventListener('change', async function (e) {
    var file = e.target.files[0];
    if (!file) return;
    status.textContent = '';
    if (file.size > MAX) {
      status.textContent = 'Lỗi: ảnh lớn hơn 5 MB.';
      fileInput.value = '';
      return;
    }
    var mine = ++token;
    var preview = await readPreview(file);
    if (mine !== token) return;
    render(preview, 'Ảnh mới · đang tải lên…');
    fileName.textContent = file.name;
    undoBtn.hidden = false;
    setBusy(true);

    var data = new FormData();
    data.append('file', file);
    var csrf = form.querySelector('input[name="__RequestVerificationToken"]').value;
    try {
      var res = await fetch(root.getAttribute('data-upload-url'), { method: 'POST', headers: { 'RequestVerificationToken': csrf }, body: data });
      var body = await res.json();
      if (res.ok) {
        value.value = body.url;
        if (mine !== token) return;
        render(preview, 'Ảnh mới · lưu bài để áp dụng');
        status.textContent = 'Đã tải lên: ' + body.url;
      } else {
        status.textContent = 'Lỗi: ' + body.error;
        revert();
      }
    } catch (err) {
      status.textContent = 'Lỗi tải lên.';
      revert();
    } finally {
      setBusy(false);
    }
  });

  // Back to the stored image: drops a failed or unwanted new choice.
  function revert() {
    token++;
    fileInput.value = '';
    value.value = saved;
    fileName.textContent = HINT;
    undoBtn.hidden = true;
    render(saved, saved ? 'Ảnh hiện tại' : '');
  }

  removeBtn.addEventListener('click', function () {
    token++;
    fileInput.value = '';
    value.value = '';
    fileName.textContent = HINT;
    status.textContent = '';
    undoBtn.hidden = false;
    render('', 'Sẽ bỏ ảnh bìa khi lưu', 'Ảnh bìa sẽ bị bỏ khi bạn lưu bài');
  });

  undoBtn.addEventListener('click', function () { status.textContent = ''; revert(); });
})();
