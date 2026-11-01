/* VNews public site scripts, module 4 of 6: on-device reading history and the "reader news" panel (latest / recently read tabs).
   Real data only: the history is what this browser actually opened (localStorage), nothing is invented. */
(function () {
  'use strict';

  var SLUG = window.VNews.SLUG;
  var KEY = 'vnews_recently_read_v1';
  var LIMIT = 12;

  function readHistory() {
    try {
      var rows = JSON.parse(localStorage.getItem(KEY) || '[]');
      return Array.isArray(rows) ? rows.filter(function (x) {
        return x && typeof x.slug === 'string' && SLUG.test(x.slug) && typeof x.title === 'string';
      }).slice(0, LIMIT) : [];
    } catch (_) { return []; }
  }

  // ---- remember the article being read ----
  var article = document.querySelector('article[data-news-article]');
  if (article) {
    var slug = article.dataset.newsSlug;
    var title = article.dataset.newsTitle;
    if (slug && SLUG.test(slug) && title) {
      try {
        var history = readHistory().filter(function (x) { return x.slug !== slug; });
        history.unshift({ slug: slug, title: title.slice(0, 220) });
        localStorage.setItem(KEY, JSON.stringify(history.slice(0, LIMIT)));
      } catch (_) { /* Optional local history may be blocked. */ }
    }
  }

  // ---- the panel ----
  var panel = document.getElementById('reader-news-panel');
  if (!panel) return;
  var tabs = Array.from(panel.querySelectorAll('[data-reader-news-tab]'));

  function renderRecentlyRead() {
    var list = panel.querySelector('#reader-news-recent-list');
    var empty = panel.querySelector('#reader-news-recent-empty');
    if (!list || !empty) return;
    list.replaceChildren();
    var rows = readHistory();
    empty.hidden = rows.length !== 0;
    rows.forEach(function (item) {
      var li = document.createElement('li');
      var a = document.createElement('a');
      a.href = '/tin/' + encodeURIComponent(item.slug);
      a.textContent = item.title; // text, never HTML
      li.appendChild(a);
      list.appendChild(li);
    });
  }

  function selectTab(which) {
    tabs.forEach(function (tab) {
      var selected = tab.dataset.readerNewsTab === which;
      tab.setAttribute('aria-selected', String(selected));
      tab.tabIndex = selected ? 0 : -1;
    });
    panel.querySelector('#reader-news-content-latest').hidden = which !== 'latest';
    panel.querySelector('#reader-news-content-seen').hidden = which !== 'seen';
    if (which === 'seen') renderRecentlyRead();
  }

  tabs.forEach(function (tab) {
    tab.addEventListener('click', function () { selectTab(tab.dataset.readerNewsTab); });
    tab.addEventListener('keydown', function (e) {
      if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
      e.preventDefault();
      var next = tabs[(tabs.indexOf(tab) + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
      selectTab(next.dataset.readerNewsTab);
      next.focus();
    });
  });

  document.addEventListener('click', function (event) {
    if (panel.open && !panel.contains(event.target)) panel.open = false;
  });
  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape' && panel.open) { panel.open = false; panel.querySelector('summary').focus(); }
  });
})();
