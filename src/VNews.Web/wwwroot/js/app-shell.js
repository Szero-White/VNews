/* Account / newsroom shell: mobile menu, dismissible alerts, auto-submitting filters, and one confirmation dialog for
   important actions. Plain DOM, no dependency. Forms opt in with data attributes:
     data-confirm="Question?"  (optional data-confirm-title, data-confirm-label, data-confirm-danger)
     data-reason               asks for a required reason and puts it into the form's hidden input[name="reason"]. */
(function () {
  'use strict';

  // ----- mobile menu -----
  var sidebar = document.getElementById('app-sidebar');
  var backdrop = document.getElementById('app-backdrop');
  var menuButton = document.getElementById('app-menu-btn');
  function setMenu(open) {
    if (!sidebar || !menuButton) return;
    sidebar.classList.toggle('is-open', open);
    if (backdrop) backdrop.hidden = !open;
    menuButton.setAttribute('aria-expanded', String(open));
    menuButton.setAttribute('aria-label', open ? 'Đóng menu' : 'Mở menu');
    if (open) { var first = sidebar.querySelector('a, button'); if (first) first.focus(); } else { menuButton.focus(); }
  }
  if (menuButton) menuButton.addEventListener('click', function () { setMenu(!sidebar.classList.contains('is-open')); });
  if (backdrop) backdrop.addEventListener('click', function () { setMenu(false); });
  document.querySelectorAll('[data-app-menu-close]').forEach(function (b) { b.addEventListener('click', function () { setMenu(false); }); });
  document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && sidebar && sidebar.classList.contains('is-open')) setMenu(false); });
  window.addEventListener('resize', function () { if (window.innerWidth > 900 && sidebar && sidebar.classList.contains('is-open')) { sidebar.classList.remove('is-open'); if (backdrop) backdrop.hidden = true; menuButton.setAttribute('aria-expanded', 'false'); } });

  // ----- alerts and toasts -----
  document.addEventListener('click', function (e) {
    var close = e.target.closest('[data-dismiss]');
    if (!close) return;
    var alert = close.closest('.app-alert');
    if (alert) { alert.remove(); return; }
    var toast = close.closest('[data-toast]');
    if (toast) dismissToast(toast);
  });

  function dismissToast(toast) {
    if (toast.__leaving) return;
    toast.__leaving = true;
    toast.classList.add('is-leaving');
    setTimeout(function () { toast.remove(); }, 260);
  }

  /** A toast leaves when its time bar has run out (the bar pauses while the pointer or focus is on the toast, so the toast waits too). */
  function watchToast(toast) {
    var bar = toast.querySelector('.app-toast__bar');
    if (bar) bar.addEventListener('animationend', function () { dismissToast(toast); });
  }
  document.querySelectorAll('[data-toast]').forEach(watchToast);

  var SVG = 'http://www.w3.org/2000/svg';
  function svgIcon(id) {
    var svg = document.createElementNS(SVG, 'svg');
    svg.setAttribute('class', 'icon');
    svg.setAttribute('aria-hidden', 'true');
    var use = document.createElementNS(SVG, 'use');
    use.setAttribute('href', '#' + id);
    svg.appendChild(use);
    return svg;
  }
  function part(tag, cls, text) {
    var node = document.createElement(tag);
    node.className = cls;
    if (text) node.textContent = text; // text only: a message can never become markup
    return node;
  }

  /** Shows a floating message from script: VNewsToast.show('Đã lưu thay đổi.', 'success' | 'error'). Same look as the ones the server renders. */
  window.VNewsToast = {
    show: function (message, kind) {
      var error = kind === 'error';
      var host = document.querySelector('.app-toasts');
      if (!host) { host = part('div', 'app-toasts'); host.setAttribute('aria-live', 'polite'); document.body.appendChild(host); }

      var toast = part('div', 'app-toast ' + (error ? 'app-toast--error' : 'app-toast--success'));
      toast.setAttribute('role', error ? 'alert' : 'status');
      toast.setAttribute('data-toast', '');
      toast.style.setProperty('--toast-ms', error ? '10000ms' : '5000ms');

      var badge = part('span', 'app-toast__icon');
      badge.appendChild(svgIcon(error ? 'i-alert' : 'i-check'));
      var body = part('div', 'app-toast__body');
      body.appendChild(part('strong', 'app-toast__title', error ? 'Có lỗi xảy ra' : 'Thành công'));
      body.appendChild(part('span', 'app-toast__text', message));
      var close = part('button', 'app-toast__close');
      close.type = 'button';
      close.setAttribute('data-dismiss', '');
      close.setAttribute('aria-label', 'Đóng thông báo');
      close.appendChild(svgIcon('i-x'));
      var bar = part('span', 'app-toast__bar');
      bar.setAttribute('aria-hidden', 'true');

      toast.appendChild(badge); toast.appendChild(body); toast.appendChild(close); toast.appendChild(bar);
      host.appendChild(toast);
      watchToast(toast);
      var all = host.querySelectorAll('[data-toast]'); // never a tall pile of them
      for (var i = 0; i < all.length - 4; i++) dismissToast(all[i]);
    }
  };

  // ----- filters that apply when changed -----
  document.querySelectorAll('select[data-autosubmit]').forEach(function (select) {
    select.addEventListener('change', function () { select.form.submit(); });
  });

  // ----- confirmation dialog -----
  var dialog = document.getElementById('app-confirm');
  if (!dialog || typeof dialog.showModal !== 'function') return;
  var title = document.getElementById('app-confirm-title');
  var text = document.getElementById('app-confirm-text');
  var reasonBox = dialog.querySelector('.app-dialog__reason');
  var reason = document.getElementById('app-confirm-reason');
  var reasonError = dialog.querySelector('.app-field-error');
  var accept = dialog.querySelector('[data-confirm-accept]');
  var cancel = dialog.querySelector('[data-confirm-cancel]');
  var pendingForm = null;
  var opener = null;

  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form.matches || !form.matches('form[data-confirm]') || form.__confirmed) return;
    e.preventDefault();
    pendingForm = form;
    opener = document.activeElement;
    title.textContent = form.getAttribute('data-confirm-title') || 'Xác nhận';
    text.textContent = form.getAttribute('data-confirm');
    accept.textContent = form.getAttribute('data-confirm-label') || 'Xác nhận';
    accept.className = 'btn ' + (form.hasAttribute('data-confirm-danger') ? 'btn--danger-solid' : 'btn--primary');
    var needsReason = form.hasAttribute('data-reason');
    reasonBox.hidden = !needsReason;
    reasonError.hidden = true;
    reason.value = '';
    dialog.showModal();
    (needsReason ? reason : accept).focus();
  });

  accept.addEventListener('click', function () {
    if (!pendingForm) return;
    var needsReason = pendingForm.hasAttribute('data-reason');
    if (needsReason) {
      var value = reason.value.trim();
      if (!value) { reasonError.hidden = false; reason.focus(); return; }
      var hidden = pendingForm.querySelector('input[name="reason"]');
      if (hidden) hidden.value = value;
    }
    var form = pendingForm;
    pendingForm = null;
    form.__confirmed = true;
    dialog.close();
    if (form.hasAttribute('data-inplace')) form.dispatchEvent(new CustomEvent('vnews:confirmed')); // list-actions.js does the work without reloading the page
    else form.submit();
  });
  cancel.addEventListener('click', function () { pendingForm = null; dialog.close(); });
  dialog.addEventListener('close', function () { pendingForm = null; if (opener && opener.isConnected) opener.focus(); opener = null; });
  dialog.addEventListener('click', function (e) { if (e.target === dialog) dialog.close(); });
})();

