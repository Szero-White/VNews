/* VNews public site scripts, module 6 of 6: the reading tools row of an article: share, listen to the article, font size.
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

  // ---- listen to the article ----
  // The whole text is read: title, summary, every paragraph / heading / list item / quote and the picture captions. Browsers cut a single long
  // utterance after ~15 seconds, so the text is queued as short pieces (a sentence or two each) and read one after another.
  var MAX_PIECE = 180;
  var session = 0; // each start gets a number; events of a cancelled run must not touch the next one
  var speaking = false;

  function setSpeaking(button, on) {
    speaking = on;
    button.setAttribute('aria-pressed', String(on));
    button.textContent = on ? '■ Dừng đọc' : '▶ Nghe bài viết';
  }

  function clean(text) { return (text || '').replace(/\s+/g, ' ').trim(); }

  /** Splits one block of text into pieces of at most MAX_PIECE characters, at sentence ends where possible. */
  function pieces(text) {
    var out = [];
    var sentences = text.match(/[^.!?…]+[.!?…]*\s*/g) || [text];
    var current = '';
    sentences.forEach(function (sentence) {
      if (current && (current + sentence).length > MAX_PIECE) { out.push(current.trim()); current = ''; }
      current += sentence;
      while (current.length > MAX_PIECE * 1.5) { // a very long sentence: cut at a space
        var cut = current.lastIndexOf(' ', MAX_PIECE);
        if (cut < 40) cut = MAX_PIECE;
        out.push(current.slice(0, cut).trim());
        current = current.slice(cut);
      }
    });
    if (current.trim()) out.push(current.trim());
    return out;
  }

  /** What is read, in page order: title, summary, then the body's blocks and captions. */
  function textToRead() {
    var blocks = [clean(article.dataset.newsTitle)];
    var summary = document.getElementById('article-summary');
    if (summary) blocks.push(clean(summary.textContent));
    var body = document.getElementById('article-body');
    if (body) {
      body.querySelectorAll('p, h2, h3, h4, li, blockquote, figcaption').forEach(function (node) {
        if (node.closest('blockquote') && node.tagName !== 'BLOCKQUOTE') return; // the quote itself is read once
        blocks.push(clean(node.textContent));
      });
    }
    var queue = [];
    blocks.filter(Boolean).forEach(function (block) { pieces(block).forEach(function (piece) { queue.push(piece); }); });
    return queue;
  }

  function stopSpeech(button) {
    session++;
    speechSynthesis.cancel();
    setSpeaking(button, false);
  }

  function toggleSpeech(button) {
    if (!('speechSynthesis' in window)) { toast('Trình duyệt này không hỗ trợ đọc bằng giọng máy.'); return; }
    if (speaking) { stopSpeech(button); return; }

    var queue = textToRead();
    if (!queue.length) return;
    speechSynthesis.cancel();
    var mine = ++session;
    setSpeaking(button, true);
    queue.forEach(function (piece, index) {
      var utterance = new SpeechSynthesisUtterance(piece);
      utterance.lang = 'vi-VN';
      utterance.rate = 0.95;
      var finished = function () { if (mine === session && index === queue.length - 1) setSpeaking(button, false); };
      utterance.onend = finished;
      utterance.onerror = function () { if (mine === session) { session++; setSpeaking(button, false); } }; // a real failure stops the run
      speechSynthesis.speak(utterance);
    });
  }
  window.addEventListener('pagehide', function () { if (speaking && 'speechSynthesis' in window) { session++; speechSynthesis.cancel(); } });

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
