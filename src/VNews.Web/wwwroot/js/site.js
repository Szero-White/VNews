/* VNews public experience. No third-party dependency. */
(function () {
  'use strict';
  var dateEl = document.getElementById('today-date');
  if (dateEl) {
    var days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];
    var now = new Date();
    dateEl.textContent = days[now.getDay()] + ', ' + now.toLocaleDateString('vi-VN');
  }

  // Category nav scroll-with-arrow: matches the reference's scrollable strip + round
  // "next" button that appears once the list overflows and advances it on click.
  var categoryList = document.querySelector('.category-nav__list');
  var navArrow = document.getElementById('primary-nav-next');
  if (categoryList && navArrow) {
    var updateArrow = function () {
      var overflowing = categoryList.scrollWidth > categoryList.clientWidth + 1;
      var atEnd = categoryList.scrollLeft + categoryList.clientWidth >= categoryList.scrollWidth - 1;
      navArrow.hidden = !overflowing || atEnd;
    };
    navArrow.addEventListener('click', function () {
      categoryList.scrollBy({ left: 220, behavior: 'smooth' });
    });
    categoryList.addEventListener('scroll', updateArrow);
    window.addEventListener('resize', updateArrow);
    window.addEventListener('load', updateArrow);

    // Bring the category being read into view (matters when the strip scrolls, e.g. on phones).
    var current = categoryList.querySelector('a[aria-current="page"]');
    var centerCurrent = function () {
      if (!current) return;
      var listBox = categoryList.getBoundingClientRect();
      var linkBox = current.getBoundingClientRect();
      var left = linkBox.left - listBox.left + categoryList.scrollLeft;
      categoryList.scrollLeft = Math.max(0, left - (categoryList.clientWidth - linkBox.width) / 2);
    };
    centerCurrent();
    window.addEventListener('load', centerCurrent); // web fonts change link widths
    updateArrow();
  }

  var toggle = document.querySelector('.masthead__menu-toggle');
  var nav = document.getElementById('utility-drawer');
  if (toggle && nav) {
    toggle.addEventListener('click', function () {
      var open = nav.classList.toggle('is-open');
      toggle.setAttribute('aria-expanded', String(open));
      toggle.setAttribute('aria-label', open ? 'Đóng menu tiện ích' : 'Mở menu tiện ích');
    });
    nav.addEventListener('click', function (e) {
      if (e.target.closest('a') && nav.classList.contains('is-open')) {
        nav.classList.remove('is-open');
        toggle.setAttribute('aria-expanded', 'false');
      }
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && nav.classList.contains('is-open')) {
        nav.classList.remove('is-open'); toggle.setAttribute('aria-expanded', 'false'); toggle.focus();
      }
    });
  }

  // Login dialog: header and mobile drawer links remain ordinary /Account/Login
  // links when JavaScript or <dialog> is unsupported. Invalid credentials are
  // handled by the existing Identity view with full server-side errors.
  var readerDialog = document.getElementById('reader-login-dialog');
  if (readerDialog && typeof readerDialog.showModal === 'function') {
    var loginOpener = null;
    document.addEventListener('click', function (event) {
      var trigger = event.target.closest('[data-reader-login-open]');
      if (!trigger) return;
      event.preventDefault();
      loginOpener = trigger;
      if (nav && nav.classList.contains('is-open')) {
        nav.classList.remove('is-open');
        if (toggle) toggle.setAttribute('aria-expanded', 'false');
      }
      readerDialog.showModal();
      var emailField = readerDialog.querySelector('input[name="Email"]');
      if (emailField) emailField.focus();
    });
    var closeReaderDialog = readerDialog.querySelector('[data-reader-login-close]');
    if (closeReaderDialog) closeReaderDialog.addEventListener('click', function () { readerDialog.close(); });
    readerDialog.addEventListener('click', function (event) {
      if (event.target === readerDialog) readerDialog.close();
    });
    readerDialog.addEventListener('close', function () {
      if (loginOpener && loginOpener.isConnected) loginOpener.focus();
      loginOpener = null;
    });

    // Sign in from the dialog without leaving the page: wrong credentials show their message right here.
    // If fetch is unavailable or the request itself fails, the form simply submits normally (full-page fallback).
    var loginForm = readerDialog.querySelector('.reader-login-dialog__form');
    var loginError = readerDialog.querySelector('[data-reader-login-error]');
    var loginSubmit = loginForm ? loginForm.querySelector('button[type="submit"]') : null;
    if (loginForm && loginError && loginSubmit && window.fetch && window.FormData) {
      var showLoginError = function (message) {
        loginError.textContent = message;
        loginError.hidden = false;
        var passwordField = loginForm.querySelector('input[name="Password"]');
        if (passwordField) { passwordField.value = ''; passwordField.focus(); }
      };
      loginForm.addEventListener('submit', function (event) {
        event.preventDefault();
        loginError.hidden = true;
        loginSubmit.disabled = true;
        loginSubmit.setAttribute('aria-busy', 'true');
        fetch(loginForm.action, { method: 'POST', body: new FormData(loginForm), headers: { 'X-Requested-With': 'fetch', 'Accept': 'application/json' }, credentials: 'same-origin' })
          .then(function (response) {
            if (response.status === 429) { showLoginError('Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.'); return null; }
            return response.json().then(function (data) { return { ok: response.ok, data: data }; });
          })
          .then(function (result) {
            if (!result) return;
            var target = result.data && result.data.redirect;
            if (result.ok && typeof target === 'string' && target.charAt(0) === '/' && target.charAt(1) !== '/' && target.charAt(1) !== '\\') {
              window.location.assign(target);
              return;
            }
            showLoginError((result.data && result.data.error) || 'Không thể đăng nhập lúc này. Vui lòng thử lại.');
          })
          .catch(function () { loginForm.submit(); })
          .then(function () { loginSubmit.disabled = false; loginSubmit.removeAttribute('aria-busy'); });
      });
    }
  }

  // An actual, on-device read history. No invented notifications or comments.
  var recentKey = 'vnews_recently_read_v1';
  function getRecent() {
    try {
      var rows = JSON.parse(localStorage.getItem(recentKey) || '[]');
      return Array.isArray(rows) ? rows.filter(function (x) {
        return x && typeof x.slug === 'string' && /^[a-z0-9-]{1,180}$/.test(x.slug)
          && typeof x.title === 'string';
      }).slice(0, 12) : [];
    } catch (_) { return []; }
  }
  var currentArticle = document.querySelector('article[data-news-article]');
  if (currentArticle) {
    var currentSlug = currentArticle.dataset.newsSlug;
    var currentTitle = currentArticle.dataset.newsTitle;
    if (currentSlug && /^[a-z0-9-]{1,180}$/.test(currentSlug) && currentTitle) {
      try {
        var history = getRecent().filter(function (x) { return x.slug !== currentSlug; });
        history.unshift({ slug: currentSlug, title: currentTitle.slice(0, 220) });
        localStorage.setItem(recentKey, JSON.stringify(history.slice(0, 12)));
      } catch (_) { /* Optional local history may be blocked. */ }
    }
  }
  var newsPanel = document.getElementById('reader-news-panel');
  if (newsPanel) {
    var newsTabs = Array.from(newsPanel.querySelectorAll('[data-reader-news-tab]'));
    function renderRecentlyRead() {
      var list = newsPanel.querySelector('#reader-news-recent-list');
      var empty = newsPanel.querySelector('#reader-news-recent-empty');
      if (!list || !empty) return;
      list.replaceChildren();
      var rows = getRecent();
      empty.hidden = rows.length !== 0;
      rows.forEach(function (item) {
        var li = document.createElement('li');
        var a = document.createElement('a');
        a.href = '/tin/' + encodeURIComponent(item.slug);
        a.textContent = item.title;
        li.appendChild(a);
        list.appendChild(li);
      });
    }
    function changeNewsTab(which) {
      newsTabs.forEach(function (tab) {
        var selected = tab.dataset.readerNewsTab === which;
        tab.setAttribute('aria-selected', String(selected));
        tab.tabIndex = selected ? 0 : -1;
      });
      newsPanel.querySelector('#reader-news-content-latest').hidden = which !== 'latest';
      newsPanel.querySelector('#reader-news-content-seen').hidden = which !== 'seen';
      if (which === 'seen') renderRecentlyRead();
    }
    newsTabs.forEach(function (tab) {
      tab.addEventListener('click', function () { changeNewsTab(tab.dataset.readerNewsTab); });
      tab.addEventListener('keydown', function (e) {
        if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
        e.preventDefault();
        var index = newsTabs.indexOf(tab);
        var next = newsTabs[(index + (e.key === 'ArrowRight' ? 1 : newsTabs.length - 1)) % newsTabs.length];
        changeNewsTab(next.dataset.readerNewsTab); next.focus();
      });
    });
    document.addEventListener('click', function (event) {
      if (newsPanel.open && !newsPanel.contains(event.target)) newsPanel.open = false;
    });
    document.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && newsPanel.open) { newsPanel.open = false; newsPanel.querySelector('summary').focus(); }
    });
  }

  var backTop = document.getElementById('back-to-top');
  var progress = document.getElementById('reading-progress');
  var article = document.querySelector('[data-news-article]');
  var toastEl = document.getElementById('site-toast');
  var toastHandle;
  function toast(message) {
    if (!toastEl) return;
    toastEl.textContent = message;
    toastEl.hidden = false;
    clearTimeout(toastHandle);
    toastHandle = setTimeout(function () { toastEl.hidden = true; }, 2600);
  }
  var ticking = false;
  function onScroll() {
    if (ticking) return;
    ticking = true;
    requestAnimationFrame(function () {
      var top = window.scrollY || document.documentElement.scrollTop;
      var length = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);
      if (backTop) backTop.hidden = top < 500;
      if (progress) progress.style.width = article ? (Math.min(100, Math.round(100 * top / length)) + '%') : '0';
      ticking = false;
    });
  }
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();
  if (backTop) backTop.addEventListener('click', function () { window.scrollTo({ top: 0, behavior: 'smooth' }); });

  // Guests save locally; signed-in readers use the SQL-backed bookmarks API.
  var key = 'vnews.saved.v1';
  var accountBookmarks = !!document.querySelector('meta[name="vnews-bookmarks-mode"][content="account"]');
  var csrfMeta = document.querySelector('meta[name="vnews-csrf-token"]');
  var csrf = csrfMeta ? csrfMeta.content : '';
  var accountSaved = [];
  var savedLoaded = false;

  function readSaved() {
    try {
      var data = JSON.parse(localStorage.getItem(key) || '[]');
      return Array.isArray(data) ? data.filter(function (x) { return x && typeof x.slug === 'string' && /^[a-z0-9-]{1,180}$/.test(x.slug); }).slice(0, 100) : [];
    } catch (_) { return []; }
  }
  function writeSaved(data) {
    try { localStorage.setItem(key, JSON.stringify(data.slice(0, 100))); return true; }
    catch (_) { toast('Trình duyệt không cho phép lưu bài.'); return false; }
  }
  function articleInfo() {
    return article ? {
      id: Number(article.dataset.newsId) || 0,
      slug: article.dataset.newsSlug || '',
      title: article.dataset.newsTitle || '',
      image: article.dataset.newsImage || ''
    } : null;
  }
  function savedRows() { return accountBookmarks ? accountSaved : readSaved(); }
  async function refreshAccountSaved() {
    if (!accountBookmarks) return;
    var response = await fetch('/api/me/bookmarks', { credentials: 'same-origin', cache: 'no-store' });
    if (!response.ok) throw new Error('Không thể tải các bài đã lưu.');
    accountSaved = await response.json();
    savedLoaded = true;
  }
  async function changeAccountBookmark(id, remove) {
    var response = await fetch('/api/me/bookmarks/' + id, {
      method: remove ? 'DELETE' : 'POST',
      credentials: 'same-origin',
      headers: { 'RequestVerificationToken': csrf }
    });
    if (!response.ok) throw new Error('Không thể cập nhật bài đã lưu.');
    await refreshAccountSaved();
  }
  var savedButton = document.querySelector('[data-action="save"]');
  function updateSaveButton() {
    if (!savedButton || !article) return;
    var id = Number(article.dataset.newsId);
    var saved = savedRows().some(function (x) { return accountBookmarks ? x.id === id : x.slug === article.dataset.newsSlug; });
    savedButton.setAttribute('aria-pressed', String(saved));
    savedButton.textContent = saved ? '★ Đã lưu' : '☆ Lưu bài';
  }
  if (accountBookmarks && savedButton) {
    savedButton.disabled = true;
    refreshAccountSaved().then(function () { updateSaveButton(); })
      .catch(function () { toast('Không thể tải trạng thái lưu bài.'); })
      .finally(function () { savedButton.disabled = false; });
  } else {
    updateSaveButton();
  }

  function shareArticle() {
    var payload = { title: document.title, url: location.href };
    if (navigator.share) {
      navigator.share(payload).catch(function () { /* user may cancel */ });
    } else if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(location.href).then(function () { toast('Đã sao chép liên kết bài viết.'); })
        .catch(function () { toast('Không thể sao chép tự động. Hãy sao chép từ thanh địa chỉ.'); });
    } else {
      toast('Bạn có thể sao chép liên kết trên thanh địa chỉ.');
    }
  }
  var speaking = false;
  function toggleSpeech(button) {
    if (!('speechSynthesis' in window)) { toast('Trình duyệt này không hỗ trợ đọc bằng giọng máy.'); return; }
    if (speaking) { speechSynthesis.cancel(); speaking = false; button.setAttribute('aria-pressed', 'false'); button.textContent = '▶ Nghe tóm tắt'; return; }
    var summary = document.getElementById('article-summary');
    if (!summary) return;
    speechSynthesis.cancel();
    var utterance = new SpeechSynthesisUtterance((article.dataset.newsTitle || '') + '. ' + summary.textContent);
    utterance.lang = 'vi-VN'; utterance.rate = 0.95;
    speaking = true; button.setAttribute('aria-pressed', 'true'); button.textContent = '■ Dừng đọc';
    function reset() { speaking = false; button.setAttribute('aria-pressed', 'false'); button.textContent = '▶ Nghe tóm tắt'; }
    utterance.onend = reset; utterance.onerror = reset;
    speechSynthesis.speak(utterance);
  }
  document.addEventListener('click', function (e) {
    var button = e.target.closest('[data-action]');
    if (!button) return;
    var action = button.dataset.action;
    if (action === 'share') shareArticle();
    if (action === 'save') {
      var info = articleInfo();
      if (!info || !/^[a-z0-9-]{1,180}$/.test(info.slug)) return;
      if (accountBookmarks) {
        if (!info.id || button.disabled) return;
        button.disabled = true;
        var wasSaved = accountSaved.some(function (x) { return x.id === info.id; });
        changeAccountBookmark(info.id, wasSaved).then(function () {
          updateSaveButton();
          toast(wasSaved ? 'Đã bỏ lưu bài viết.' : 'Đã lưu vào tài khoản của bạn.');
        }).catch(function () { toast('Không thể lưu bài lúc này. Vui lòng thử lại.'); })
          .finally(function () { button.disabled = false; });
      } else {
        var data = readSaved(); var current = data.some(function (x) { return x.slug === info.slug; });
        data = current ? data.filter(function (x) { return x.slug !== info.slug; }) : [info].concat(data);
        if (writeSaved(data)) { updateSaveButton(); toast(current ? 'Đã bỏ lưu bài viết.' : 'Đã lưu trên thiết bị. Đăng nhập để đồng bộ.'); }
      }
    }
    if (action === 'speak') toggleSpeech(button);
    if (action === 'font-up' || action === 'font-down') {
      var body = document.getElementById('article-body');
      if (!body) return;
      var currentSize = parseFloat(getComputedStyle(body).fontSize) || 19;
      body.style.fontSize = Math.min(25, Math.max(15, currentSize + (action === 'font-up' ? 1 : -1))) + 'px';
    }
  });
  window.addEventListener('pagehide', function () { if (speaking && 'speechSynthesis' in window) speechSynthesis.cancel(); });

  // Saved-page cards are built with textContent, never unsanitized HTML.
  var savedList = document.getElementById('saved-articles-list');
  if (savedList) {
    var empty = document.getElementById('saved-empty');
    function renderSaved() {
      savedList.replaceChildren();
      var rows = savedRows();
      if (empty) empty.hidden = rows.length > 0;
      rows.forEach(function (row) {
        var item = document.createElement('article'); item.className = 'saved-article';
        var href = '/tin/' + encodeURIComponent(row.slug);
        if (row.image && (/^\/images\/|^\/uploads\/articles\//).test(row.image)) {
          var imgLink = document.createElement('a'); imgLink.href = href;
          var img = document.createElement('img'); img.src = row.image; img.alt = ''; img.loading = 'lazy'; imgLink.appendChild(img); item.appendChild(imgLink);
        }
        var content = document.createElement('div');
        var link = document.createElement('a'); link.href = href; link.className = 'saved-article__title'; link.textContent = row.title || row.slug;
        var br = document.createElement('div');
        var remove = document.createElement('button'); remove.type = 'button'; remove.className = 'saved-article__delete'; remove.textContent = 'Bỏ lưu';
        remove.addEventListener('click', function () {
          if (accountBookmarks) {
            remove.disabled = true;
            changeAccountBookmark(row.id, true).then(renderSaved)
              .catch(function () { toast('Không thể bỏ lưu lúc này.'); })
              .finally(function () { remove.disabled = false; });
          } else {
            if (writeSaved(readSaved().filter(function (x) { return x.slug !== row.slug; }))) renderSaved();
          }
        });
        br.appendChild(remove); content.append(link, br); item.appendChild(content); savedList.appendChild(item);
      });
    }
    if (accountBookmarks) {
      if (empty) { empty.hidden = false; empty.textContent = 'Đang tải bài đã lưu...'; }
      refreshAccountSaved().then(function () {
        if (empty) empty.textContent = 'Bạn chưa lưu bài viết nào.';
        renderSaved();
      }).catch(function () {
        if (empty) empty.textContent = 'Không thể tải bài đã lưu. Vui lòng tải lại trang.';
      });
    } else renderSaved();
  }
})();
