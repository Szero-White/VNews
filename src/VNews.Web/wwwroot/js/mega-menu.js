/* "Tất cả chuyên mục" desktop menu: open/close, overlay, Escape, outside click, focus management, scroll lock.
   Plain DOM, no dependency. Mobile (<= 768px) keeps its own drawer, so this menu closes when the viewport shrinks. */
(function () {
  'use strict';

  var toggle = document.getElementById('mega-menu-toggle');
  var menu = document.getElementById('mega-menu');
  var overlay = document.getElementById('mega-menu-overlay');
  var nav = document.getElementById('primary-nav');
  if (!toggle || !menu || !overlay || !nav) return;

  var DESKTOP = window.matchMedia('(min-width: 769px)');
  // Regions behind the menu: made inert while it is open so focus and assistive tech stay inside.
  var behind = ['.masthead', '#utility-drawer', '.mobile-weather-row', '.site-main', '.site-footer', '.back-to-top'];
  var lastFocus = null;
  var scrollbarGap = '';

  function isOpen() { return !menu.hidden; }

  function focusables() {
    return Array.prototype.slice.call(menu.querySelectorAll('a[href], button:not([disabled])'))
      .filter(function (el) { return el.offsetParent !== null; });
  }

  function fitHeight() {
    // Keep the whole menu reachable on short windows: it scrolls inside instead of running off-screen.
    var room = window.innerHeight - nav.getBoundingClientRect().bottom - 8;
    menu.style.maxHeight = Math.max(240, room) + 'px';
  }

  function setBehindInert(value) {
    behind.forEach(function (selector) {
      document.querySelectorAll(selector).forEach(function (el) {
        if (value) el.setAttribute('inert', ''); else el.removeAttribute('inert');
      });
    });
  }

  function open() {
    if (isOpen() || !DESKTOP.matches) return;
    lastFocus = document.activeElement;
    var root = document.documentElement;
    var gap = window.innerWidth - root.clientWidth;
    scrollbarGap = root.style.paddingRight;
    if (gap > 0) root.style.paddingRight = gap + 'px'; // no layout jump when the scrollbar disappears
    root.classList.add('mega-open');

    overlay.hidden = false;
    menu.hidden = false;
    toggle.setAttribute('aria-expanded', 'true');
    fitHeight();
    setBehindInert(true);
    var firstLink = menu.querySelector('.mega-menu__list a');
    (firstLink || menu.querySelector('[data-mega-close]')).focus();
  }

  function close(restoreFocus) {
    if (!isOpen()) return;
    menu.hidden = true;
    overlay.hidden = true;
    toggle.setAttribute('aria-expanded', 'false');
    setBehindInert(false);
    var root = document.documentElement;
    root.classList.remove('mega-open');
    root.style.paddingRight = scrollbarGap;
    if (restoreFocus !== false) (lastFocus && document.contains(lastFocus) ? lastFocus : toggle).focus();
    lastFocus = null;
  }

  toggle.addEventListener('click', function () { if (isOpen()) close(); else open(); });
  menu.querySelectorAll('[data-mega-close]').forEach(function (b) { b.addEventListener('click', function () { close(); }); });
  overlay.addEventListener('click', function () { close(); });

  // Click anywhere outside the menu and its toggle closes it (the overlay covers most of the page already).
  document.addEventListener('click', function (e) {
    if (!isOpen()) return;
    if (menu.contains(e.target) || toggle.contains(e.target)) return;
    close(false);
  });

  document.addEventListener('keydown', function (e) {
    if (!isOpen()) return;
    if (e.key === 'Escape') { e.preventDefault(); close(); return; }
    if (e.key !== 'Tab') return;
    // Keep Tab / Shift+Tab inside the menu.
    var items = focusables();
    if (!items.length) return;
    var first = items[0], last = items[items.length - 1];
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    else if (!menu.contains(document.activeElement)) { e.preventDefault(); first.focus(); }
  });

  // Opening the login dialog from inside the menu: close the menu first so the two layers do not fight for focus.
  menu.addEventListener('click', function (e) {
    if (e.target.closest('[data-reader-login-open]')) close(false);
  });

  window.addEventListener('resize', function () {
    if (!DESKTOP.matches) close(false);
    else if (isOpen()) fitHeight();
  });
  // Back/forward cache restores the page as it was; never come back with the menu still open.
  window.addEventListener('pageshow', function (e) { if (e.persisted) close(false); });
})();
