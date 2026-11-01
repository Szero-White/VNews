/* VNews public site scripts, module 1 of 6: shared helpers. Every other module reads this one namespace instead of its own globals.
   Load order (see _Layout): core, chrome, login-dialog, reading-history, bookmarks, article-tools; then the standalone mega-menu and
   category-submenu scripts. */
(function () {
  'use strict';

  var VNews = window.VNews = window.VNews || {};

  /** Slugs are only ever [a-z0-9-]; anything else coming from storage or the DOM is ignored. */
  VNews.SLUG = /^[a-z0-9-]{1,180}$/;

  var toastEl = document.getElementById('site-toast');
  var toastHandle;
  /** Shows a short status message (aria-live region), hidden again after a moment. */
  VNews.toast = function (message) {
    if (!toastEl) return;
    toastEl.textContent = message;
    toastEl.hidden = false;
    clearTimeout(toastHandle);
    toastHandle = setTimeout(function () { toastEl.hidden = true; }, 2600);
  };
})();
