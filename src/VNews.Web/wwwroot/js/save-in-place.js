/* Save without leaving the page. A form marked data-save-in-place is posted with fetch: the page keeps its scroll position and whatever is on
   screen, and a toast says whether it worked. The server answers JSON when the request carries X-Requested-With: fetch.
   After a good save the form's new version number replaces the old one (so the next save is not a conflict) and the form fires "vnews:saved",
   which the cover, the gallery and the draft modules use to treat what is on screen as stored. A bad save fires "vnews:save-failed".
   Anything that is not a JSON answer (for example the sign-in page after the session ended) falls back to a normal submit. */
(function () {
  'use strict';

  var form = document.querySelector('form[data-save-in-place]');
  if (!form) return;

  var busy = false;

  function toast(message, kind) {
    if (window.VNewsToast) window.VNewsToast.show(message, kind);
    else window.alert(message);
  }

  function setBusy(on, button) {
    busy = on;
    form.querySelectorAll('button[type="submit"]').forEach(function (b) { b.disabled = on; });
    if (button) {
      if (on) { button.__label = button.textContent; button.textContent = 'Đang lưu…'; }
      else if (button.__label) { button.textContent = button.__label; }
    }
  }

  form.addEventListener('submit', async function (e) {
    if (busy) { e.preventDefault(); return; }
    if (e.defaultPrevented) return;
    if (window.jQuery && window.jQuery.fn && window.jQuery.fn.valid && !window.jQuery(form).valid()) { e.preventDefault(); return; } // the page's own field checks

    e.preventDefault();
    var button = e.submitter || form.querySelector('button[type="submit"]');
    setBusy(true, button);
    try {
      var response = await fetch(form.action, {
        method: 'POST',
        body: new FormData(form),
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'fetch', 'Accept': 'application/json' }
      });
      if ((response.headers.get('content-type') || '').indexOf('application/json') === -1) {
        setBusy(false, button);
        form.submit(); // not a JSON answer: let the browser handle it the ordinary way
        return;
      }
      var result = await response.json();
      if (result.ok) {
        var version = form.querySelector('input[name="rowVersionBase64"]');
        if (version && result.rowVersion) version.value = result.rowVersion;
        form.dispatchEvent(new CustomEvent('vnews:saved', { detail: result }));
        toast(result.message || 'Đã lưu thay đổi.', 'success');
      } else {
        form.dispatchEvent(new CustomEvent('vnews:save-failed', { detail: result }));
        toast(result.message || 'Không lưu được. Vui lòng thử lại.', 'error');
      }
    } catch (err) {
      form.dispatchEvent(new CustomEvent('vnews:save-failed'));
      toast('Không kết nối được máy chủ. Nội dung của bạn vẫn còn trên trang, hãy thử lưu lại.', 'error');
    } finally {
      if (busy) setBusy(false, button);
    }
  });
})();
