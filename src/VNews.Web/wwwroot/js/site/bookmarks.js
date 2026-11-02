/* VNews public site scripts, module 5 of 6: saving articles. Guests save on the device (localStorage); signed-in readers use the
   SQL-backed bookmarks API (antiforgery token from the page). Drives the "Lưu bài" button on an article and the saved-articles page. */
(function () {
  'use strict';

  var VNews = window.VNews;
  var SLUG = VNews.SLUG;
  var toast = VNews.toast;

  var KEY = 'vnews.saved.v1';
  var LIMIT = 100;
  var accountMode = !!document.querySelector('meta[name="vnews-bookmarks-mode"][content="account"]');
  var csrfMeta = document.querySelector('meta[name="vnews-csrf-token"]');
  var csrf = csrfMeta ? csrfMeta.content : '';
  var accountSaved = [];

  // ---- device storage (guests) ----
  function readLocal() {
    try {
      var data = JSON.parse(localStorage.getItem(KEY) || '[]');
      return Array.isArray(data) ? data.filter(function (x) { return x && typeof x.slug === 'string' && SLUG.test(x.slug); }).slice(0, LIMIT) : [];
    } catch (_) { return []; }
  }
  function writeLocal(data) {
    try { localStorage.setItem(KEY, JSON.stringify(data.slice(0, LIMIT))); return true; }
    catch (_) { toast('Trình duyệt không cho phép lưu bài.'); return false; }
  }

  // ---- account API (signed-in readers) ----
  async function refreshAccount() {
    if (!accountMode) return;
    var response = await fetch('/api/me/bookmarks', { credentials: 'same-origin', cache: 'no-store' });
    if (!response.ok) throw new Error('Không thể tải các bài đã lưu.');
    accountSaved = await response.json();
  }
  async function changeAccount(id, remove) {
    var response = await fetch('/api/me/bookmarks/' + id, {
      method: remove ? 'DELETE' : 'POST',
      credentials: 'same-origin',
      headers: { 'RequestVerificationToken': csrf }
    });
    if (!response.ok) throw new Error('Không thể cập nhật bài đã lưu.');
    await refreshAccount();
  }

  function rows() { return accountMode ? accountSaved : readLocal(); }

  // ---- the article page ----
  var article = document.querySelector('[data-news-article]');
  var button = document.querySelector('[data-action="save"]');

  function articleInfo() {
    return {
      id: Number(article.dataset.newsId) || 0,
      slug: article.dataset.newsSlug || '',
      title: article.dataset.newsTitle || '',
      image: article.dataset.newsImage || ''
    };
  }

  function updateButton() {
    if (!button || !article) return;
    var id = Number(article.dataset.newsId);
    var saved = rows().some(function (x) { return accountMode ? x.id === id : x.slug === article.dataset.newsSlug; });
    button.setAttribute('aria-pressed', String(saved));
    button.textContent = saved ? (button.getAttribute('data-label-on') || '★ Đã lưu') : (button.getAttribute('data-label-off') || '☆ Lưu bài');
  }

  function toggleAccount(info) {
    if (!info.id || button.disabled) return;
    button.disabled = true; // one request at a time: a double tap must not flip the state twice
    var wasSaved = accountSaved.some(function (x) { return x.id === info.id; });
    changeAccount(info.id, wasSaved).then(function () {
      updateButton();
      toast(wasSaved ? 'Đã bỏ lưu bài viết.' : 'Đã lưu vào tài khoản của bạn.');
    }).catch(function () { toast('Không thể lưu bài lúc này. Vui lòng thử lại.'); })
      .finally(function () { button.disabled = false; });
  }

  function toggleLocal(info) {
    var data = readLocal();
    var already = data.some(function (x) { return x.slug === info.slug; });
    data = already ? data.filter(function (x) { return x.slug !== info.slug; }) : [info].concat(data);
    if (writeLocal(data)) { updateButton(); toast(already ? 'Đã bỏ lưu bài viết.' : 'Đã lưu trên thiết bị. Đăng nhập để đồng bộ.'); }
  }

  if (button && article) {
    if (accountMode) {
      button.disabled = true;
      refreshAccount().then(updateButton)
        .catch(function () { toast('Không thể tải trạng thái lưu bài.'); })
        .finally(function () { button.disabled = false; });
    } else {
      updateButton();
    }

    document.addEventListener('click', function (e) {
      var target = e.target.closest('[data-action="save"]');
      if (!target) return;
      var info = articleInfo();
      if (!SLUG.test(info.slug)) return;
      if (accountMode) toggleAccount(info); else toggleLocal(info);
    });
  }

  // ---- the saved-articles list (cards are built with textContent, never unsanitized HTML) ----
  var list = document.getElementById('saved-articles-list');
  if (!list) return;
  var empty = document.getElementById('saved-empty');

  function card(row) {
    var item = document.createElement('article');
    item.className = 'saved-article';
    var href = '/tin/' + encodeURIComponent(row.slug);
    if (row.image && (/^\/images\/|^\/uploads\/articles\//).test(row.image)) {
      var imgLink = document.createElement('a'); imgLink.href = href;
      var img = document.createElement('img'); img.src = row.image; img.alt = ''; img.loading = 'lazy';
      imgLink.appendChild(img); item.appendChild(imgLink);
    }
    var content = document.createElement('div');
    var link = document.createElement('a'); link.href = href; link.className = 'saved-article__title'; link.textContent = row.title || row.slug;
    var actions = document.createElement('div');
    var remove = document.createElement('button'); remove.type = 'button'; remove.className = 'saved-article__delete'; remove.textContent = 'Bỏ lưu';
    remove.addEventListener('click', function () { removeRow(row, remove); });
    actions.appendChild(remove); content.append(link, actions); item.appendChild(content);
    return item;
  }

  function removeRow(row, remove) {
    if (accountMode) {
      remove.disabled = true;
      changeAccount(row.id, true).then(render)
        .catch(function () { toast('Không thể bỏ lưu lúc này.'); })
        .finally(function () { remove.disabled = false; });
    } else if (writeLocal(readLocal().filter(function (x) { return x.slug !== row.slug; }))) {
      render();
    }
  }

  function render() {
    list.replaceChildren();
    var data = rows();
    if (empty) empty.hidden = data.length > 0;
    data.forEach(function (row) { list.appendChild(card(row)); });
  }

  if (accountMode) {
    if (empty) { empty.hidden = false; empty.textContent = 'Đang tải bài đã lưu...'; }
    refreshAccount().then(function () {
      if (empty) empty.textContent = 'Bạn chưa lưu bài viết nào.';
      render();
    }).catch(function () {
      if (empty) empty.textContent = 'Không thể tải bài đã lưu. Vui lòng tải lại trang.';
    });
  } else {
    render();
  }
})();
