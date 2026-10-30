/* VNews public experience. No third-party dependency. */
(function () {
  'use strict';
  var dateEl = document.getElementById('today-date');
  if (dateEl) {
    var days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];
    var now = new Date();
    dateEl.textContent = days[now.getDay()] + ', ' + now.toLocaleDateString('vi-VN');
  }

  var toggle = document.querySelector('.masthead__menu-toggle');
  var nav = document.getElementById('primary-nav');
  if (toggle && nav) {
    toggle.addEventListener('click', function () {
      var open = nav.classList.toggle('is-open');
      toggle.setAttribute('aria-expanded', String(open));
      toggle.setAttribute('aria-label', open ? 'Đóng danh mục' : 'Mở danh mục');
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

  // Bookmarks are explicitly local to the browser; no authentication or server persistence.
  var key = 'vnews.saved.v1';
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
    return article ? { slug: article.dataset.newsSlug || '', title: article.dataset.newsTitle || '', image: article.dataset.newsImage || '' } : null;
  }
  var savedButton = document.querySelector('[data-action="save"]');
  function updateSaveButton() {
    if (!savedButton || !article) return;
    var saved = readSaved().some(function (x) { return x.slug === article.dataset.newsSlug; });
    savedButton.setAttribute('aria-pressed', String(saved));
    savedButton.textContent = saved ? '★ Đã lưu' : '☆ Lưu bài';
  }
  updateSaveButton();

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
      var data = readSaved(); var current = data.some(function (x) { return x.slug === info.slug; });
      data = current ? data.filter(function (x) { return x.slug !== info.slug; }) : [info].concat(data);
      if (writeSaved(data)) { updateSaveButton(); toast(current ? 'Đã bỏ lưu bài viết.' : 'Đã lưu bài viết trên thiết bị.'); }
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

  // Saved-page rendering uses DOM text nodes, not innerHTML, to avoid XSS through storage.
  var savedList = document.getElementById('saved-articles-list');
  if (savedList) {
    var empty = document.getElementById('saved-empty');
    function renderSaved() {
      savedList.replaceChildren();
      var rows = readSaved();
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
        remove.addEventListener('click', function () { writeSaved(readSaved().filter(function (x) { return x.slug !== row.slug; })); renderSaved(); });
        br.appendChild(remove); content.append(link, br); item.appendChild(content); savedList.appendChild(item);
      });
    }
    renderSaved();
  }
})();
