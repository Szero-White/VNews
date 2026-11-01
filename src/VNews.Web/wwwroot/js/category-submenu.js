/* Sub-category lists in the navigation.
   - Strip (desktop): hover opens the list under the item; the button (keyboard / touch) toggles it. The panel is position:fixed and placed
     from the item's rectangle, so the strip's own scrolling and overflow can never clip it.
   - Drawer (mobile menu): the arrow button expands the list in place; the category name still navigates.
   Standard disclosure pattern: a <button aria-expanded aria-controls>, Escape closes and returns focus, focus leaving the item closes it. */
(function () {
  'use strict';

  var root = document.documentElement;
  var canHover = window.matchMedia('(hover: hover) and (pointer: fine)');
  var CLOSE_DELAY = 160; // lets the pointer travel from the item to its list without the list closing
  var openStrip = null;
  var closeTimer = null;

  function panelOf(button) { return document.getElementById(button.getAttribute('aria-controls')); }

  // ---- strip ----
  function place(item, panel) {
    var box = item.getBoundingClientRect();
    panel.style.top = box.bottom + 'px';
    var width = panel.offsetWidth;
    panel.style.left = Math.max(8, Math.min(box.left, window.innerWidth - width - 8)) + 'px';
  }

  function openItem(item) {
    if (root.classList.contains('mega-open')) return; // the full menu owns the screen while it is open
    var button = item.querySelector('[data-subnav-toggle]');
    var panel = panelOf(button);
    if (openStrip && openStrip !== item) closeItem(openStrip);
    clearTimeout(closeTimer);
    panel.hidden = false;
    place(item, panel);
    button.setAttribute('aria-expanded', 'true');
    item.classList.add('is-open');
    openStrip = item;
  }

  function closeItem(item, returnFocus) {
    var button = item.querySelector('[data-subnav-toggle]');
    panelOf(button).hidden = true;
    button.setAttribute('aria-expanded', 'false');
    item.classList.remove('is-open');
    if (openStrip === item) openStrip = null;
    if (returnFocus) button.focus();
  }

  function scheduleClose(item) {
    clearTimeout(closeTimer);
    closeTimer = setTimeout(function () { if (!item.matches(':focus-within')) closeItem(item); }, CLOSE_DELAY);
  }

  document.querySelectorAll('.category-nav__item.has-sub').forEach(function (item) {
    var button = item.querySelector('[data-subnav-toggle]');

    item.addEventListener('mouseenter', function () { if (canHover.matches) openItem(item); });
    item.addEventListener('mouseleave', function () { if (canHover.matches) scheduleClose(item); });
    button.addEventListener('click', function () { if (item.classList.contains('is-open')) closeItem(item); else openItem(item); });
    item.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && item.classList.contains('is-open')) { e.preventDefault(); closeItem(item, true); }
      else if (e.key === 'ArrowDown' && (e.target === button || e.target.matches('a')) && !e.target.closest('.category-subnav')) {
        e.preventDefault(); openItem(item); panelOf(button).querySelector('a').focus();
      } else if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && e.target.closest('.category-subnav')) {
        var links = Array.prototype.slice.call(panelOf(button).querySelectorAll('a'));
        var next = links.indexOf(e.target) + (e.key === 'ArrowDown' ? 1 : -1);
        e.preventDefault();
        if (next >= 0 && next < links.length) links[next].focus(); else if (next < 0) button.focus();
      }
    });
    item.addEventListener('focusout', function (e) {
      if (!item.contains(e.relatedTarget) && item.classList.contains('is-open')) closeItem(item);
    });
  });

  // Anything that moves the strip or opens another layer closes the list (it is positioned from a snapshot of the item).
  function closeStrip() { if (openStrip) closeItem(openStrip); }
  document.addEventListener('click', function (e) { if (openStrip && !openStrip.contains(e.target)) closeStrip(); });
  window.addEventListener('resize', closeStrip);
  window.addEventListener('scroll', closeStrip, { passive: true });
  var list = document.querySelector('.category-nav__list');
  if (list) list.addEventListener('scroll', closeStrip, { passive: true });
  var megaToggle = document.getElementById('mega-menu-toggle');
  if (megaToggle) megaToggle.addEventListener('click', closeStrip);
  document.addEventListener('click', function (e) { if (e.target.closest('[data-reader-login-open]')) closeStrip(); });

  // ---- drawer ----
  document.querySelectorAll('.drawer-cat [data-subnav-toggle]').forEach(function (button) {
    button.addEventListener('click', function () {
      var open = button.getAttribute('aria-expanded') === 'true';
      button.setAttribute('aria-expanded', String(!open));
      panelOf(button).hidden = open;
    });
  });
})();
