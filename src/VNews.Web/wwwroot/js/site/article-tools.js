/* VNews public site scripts, module 6 of 6: the reading tools row of an article: share, listen to the summary, font size.
   (Saving is bookmarks.js.) */
(function () {
  'use strict';

  var toast = window.VNews.toast;
  var article = document.querySelector('[data-news-article]');
  if (!article) return;

  // ---- share ----
  function share() {
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

  // ---- listen to the summary ----
  var speaking = false;
  function setSpeaking(button, on) {
    speaking = on;
    button.setAttribute('aria-pressed', String(on));
    button.textContent = on ? '■ Dừng đọc' : '▶ Nghe tóm tắt';
  }
  function toggleSpeech(button) {
    if (!('speechSynthesis' in window)) { toast('Trình duyệt này không hỗ trợ đọc bằng giọng máy.'); return; }
    if (speaking) { speechSynthesis.cancel(); setSpeaking(button, false); return; }
    var summary = document.getElementById('article-summary');
    if (!summary) return;
    speechSynthesis.cancel();
    var utterance = new SpeechSynthesisUtterance((article.dataset.newsTitle || '') + '. ' + summary.textContent);
    utterance.lang = 'vi-VN';
    utterance.rate = 0.95;
    utterance.onend = utterance.onerror = function () { setSpeaking(button, false); };
    setSpeaking(button, true);
    speechSynthesis.speak(utterance);
  }
  window.addEventListener('pagehide', function () { if (speaking && 'speechSynthesis' in window) speechSynthesis.cancel(); });

  // ---- font size ----
  function resizeText(delta) {
    var body = document.getElementById('article-body');
    if (!body) return;
    var size = parseFloat(getComputedStyle(body).fontSize) || 19;
    body.style.fontSize = Math.min(25, Math.max(15, size + delta)) + 'px';
  }

  // One delegated listener for the row; each action maps to one small function.
  document.addEventListener('click', function (e) {
    var button = e.target.closest('[data-action]');
    if (!button) return;
    switch (button.dataset.action) {
      case 'share': share(); break;
      case 'speak': toggleSpeech(button); break;
      case 'font-up': resizeText(1); break;
      case 'font-down': resizeText(-1); break;
    }
  });
})();
