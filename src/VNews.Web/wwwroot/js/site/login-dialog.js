/* VNews public site scripts, module 3 of 6: the sign-in dialog.
   Header and drawer links stay ordinary /tai-khoan/dang-nhap links when JavaScript or <dialog> is unsupported. Wrong credentials are
   answered by the server (JSON) and shown inside the dialog; if fetch fails the form simply submits normally (full-page fallback). */
(function () {
  'use strict';

  var VNews = window.VNews;
  var dialog = document.getElementById('reader-login-dialog');
  if (!dialog || typeof dialog.showModal !== 'function') return;

  var opener = null;

  // ---- open / close ----
  function open(trigger) {
    opener = trigger;
    if (VNews.closeDrawer) VNews.closeDrawer();
    dialog.showModal();
    var email = dialog.querySelector('input[name="Email"]');
    if (email) email.focus({ preventScroll: true });
    // A new opening always starts at the top (logo, title and the close button in view), never mid-scroll.
    var scroller = dialog.querySelector('[data-reader-login-scroll]');
    if (scroller) scroller.scrollTop = 0;
  }

  document.addEventListener('click', function (event) {
    var trigger = event.target.closest('[data-reader-login-open]');
    if (!trigger) return;
    event.preventDefault();
    open(trigger);
  });

  var closeButton = dialog.querySelector('[data-reader-login-close]');
  if (closeButton) closeButton.addEventListener('click', function () { dialog.close(); });
  dialog.addEventListener('click', function (event) { if (event.target === dialog) dialog.close(); });
  dialog.addEventListener('close', function () {
    if (opener && opener.isConnected) opener.focus();
    opener = null;
  });

  // ---- focus trap: Tab / Shift+Tab wrap inside the dialog instead of leaving it for the browser UI ----
  dialog.addEventListener('keydown', function (event) {
    if (event.key !== 'Tab') return;
    var focusables = Array.prototype.slice.call(dialog.querySelectorAll('a[href], button:not([disabled]), input:not([type="hidden"]):not([disabled])'))
      .filter(function (el) { return el.offsetParent !== null; });
    if (!focusables.length) return;
    var first = focusables[0];
    var last = focusables[focusables.length - 1];
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  });

  // ---- sign in without leaving the page ----
  var form = dialog.querySelector('.reader-login-dialog__form');
  var errorBox = dialog.querySelector('[data-reader-login-error]');
  var submit = form ? form.querySelector('button[type="submit"]') : null;
  if (!form || !errorBox || !submit || !window.fetch || !window.FormData) return;

  function showError(message) {
    errorBox.textContent = message;
    errorBox.hidden = false;
    var password = form.querySelector('input[name="Password"]');
    if (password) { password.value = ''; password.focus(); }
  }

  /** Only same-site absolute paths are followed ("/x", never "//host" or "/\host"). */
  function isLocalPath(target) {
    return typeof target === 'string' && target.charAt(0) === '/' && target.charAt(1) !== '/' && target.charAt(1) !== '\\';
  }

  function setBusy(busy) {
    submit.disabled = busy;
    if (busy) submit.setAttribute('aria-busy', 'true'); else submit.removeAttribute('aria-busy');
  }

  form.addEventListener('submit', function (event) {
    event.preventDefault();
    errorBox.hidden = true;
    setBusy(true);
    fetch(form.action, { method: 'POST', body: new FormData(form), headers: { 'X-Requested-With': 'fetch', 'Accept': 'application/json' }, credentials: 'same-origin' })
      .then(function (response) {
        if (response.status === 429) { showError('Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.'); return null; }
        return response.json().then(function (data) { return { ok: response.ok, data: data }; });
      })
      .then(function (result) {
        if (!result) return;
        var target = result.data && result.data.redirect;
        if (result.ok && isLocalPath(target)) { window.location.assign(target); return; }
        showError((result.data && result.data.error) || 'Không thể đăng nhập lúc này. Vui lòng thử lại.');
      })
      .catch(function () { form.submit(); })
      .then(function () { setBusy(false); });
  });
})();
