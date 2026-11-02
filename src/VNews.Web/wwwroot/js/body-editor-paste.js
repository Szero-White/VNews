/* Article editor module: pasting formatted content (from a web page, Word, Google Docs or this editor). The clipboard's HTML is rebuilt from
   an allow-list: paragraphs, headings, bold / italic / underline, lists, quotes, links, and pictures with their captions. Styles, classes,
   scripts, frames and everything else are dropped, so what lands in the editor is the same clean markup the editor itself makes.
   Plain-text clipboards keep the core's behaviour. Plugs into the core (body-editor.js) through window.VNewsRte.pasteHtml. */
(function () {
  'use strict';

  var ctx = window.VNewsRte;
  if (!ctx) return;

  var DROP = /^(SCRIPT|STYLE|NOSCRIPT|IFRAME|OBJECT|EMBED|FORM|BUTTON|INPUT|SELECT|TEXTAREA|SVG|CANVAS|VIDEO|AUDIO|NAV|HEAD|META|LINK)$/;
  var RENAME = { B: 'STRONG', I: 'EM', H1: 'H2', H5: 'H4', H6: 'H4' };
  var KEEP = /^(P|BR|STRONG|EM|U|UL|OL|LI|H2|H3|H4|BLOCKQUOTE|A|IMG|FIGURE|FIGCAPTION)$/;
  var BLOCKISH = /^(DIV|SECTION|ARTICLE|ASIDE|HEADER|FOOTER|MAIN|TABLE|THEAD|TBODY|TR|TD|TH|PRE|CENTER)$/;
  var INLINE = /^(STRONG|EM|U|A|BR)$/;

  function safeUrl(value, allowMail) {
    var v = (value || '').trim();
    return /^https?:\/\//i.test(v) || (allowMail && /^mailto:/i.test(v)) ? v : '';
  }

  /** The picture address: lazy-loaded pages keep the real one in a data attribute. */
  function imageSource(img) {
    return safeUrl(img.getAttribute('src')) || safeUrl(img.getAttribute('data-src')) || safeUrl(img.getAttribute('data-original'));
  }

  /** Copies `node`'s children into `out`, keeping only what the allow-list names. `inCaption` flattens blocks, a caption holds text only. */
  function copyChildren(node, out, inCaption) {
    Array.prototype.forEach.call(node.childNodes, function (child) { copyNode(child, out, inCaption); });
  }

  function copyNode(node, out, inCaption) {
    if (node.nodeType === 3) { out.appendChild(document.createTextNode(node.nodeValue.replace(/\s+/g, ' '))); return; } // the source's line breaks are layout, not content
    if (node.nodeType !== 1) return;

    var tag = node.nodeName.toUpperCase();
    if (DROP.test(tag)) return;
    tag = RENAME[tag] || tag;

    if (tag === 'IMG') {
      var src = imageSource(node);
      if (!src) return;
      var img = document.createElement('img');
      img.setAttribute('src', src);
      var alt = (node.getAttribute('alt') || '').trim();
      if (alt) img.setAttribute('alt', alt);
      out.appendChild(img);
      return;
    }

    if (!KEEP.test(tag) || (inCaption && !INLINE.test(tag))) {
      // unknown wrappers (span, div, font, ...) disappear but their content stays; block wrappers keep their content apart from the rest
      var inner = document.createDocumentFragment();
      copyChildren(node, inner, inCaption);
      if (BLOCKISH.test(tag) && !inCaption) { var wrap = document.createElement('div'); wrap.appendChild(inner); wrap.setAttribute('data-block', ''); out.appendChild(wrap); }
      else out.appendChild(inner);
      return;
    }

    var el = document.createElement(tag.toLowerCase());
    if (tag === 'A') {
      var href = safeUrl(node.getAttribute('href'), true);
      if (!href) { copyChildren(node, out, inCaption); return; }
      el.setAttribute('href', href);
    }
    copyChildren(node, el, inCaption || tag === 'FIGCAPTION');
    out.appendChild(el);
  }

  /** Loose text and inline elements between blocks become paragraphs; block wrappers are opened up; pictures outside a figure get one. */
  function tidy(root) {
    var result = document.createElement('div');
    var run = null;

    function flush() {
      if (run && run.textContent.replace(/\s+/g, '') !== '') {
        if (run.firstChild.nodeType === 3) run.firstChild.nodeValue = run.firstChild.nodeValue.replace(/^\s+/, '');
        if (run.lastChild.nodeType === 3) run.lastChild.nodeValue = run.lastChild.nodeValue.replace(/\s+$/, '');
        result.appendChild(run);
      }
      run = null;
    }
    function add(node) {
      var tag = node.nodeType === 1 ? node.nodeName.toUpperCase() : '';
      if (node.nodeType === 3 || INLINE.test(tag)) {
        if (!run) run = document.createElement('p');
        run.appendChild(node.cloneNode(true));
      } else if (tag === 'IMG') {
        flush();
        var fig = document.createElement('figure'); fig.appendChild(node.cloneNode(true)); result.appendChild(fig);
      } else if (node.hasAttribute && node.hasAttribute('data-block')) {
        flush();
        Array.prototype.forEach.call(node.childNodes, add);
        flush();
      } else {
        flush();
        if (tag === 'FIGURE') fixFigure(node);
        if (!(tag === 'P' && node.textContent.replace(/\s+/g, '') === '' && !node.querySelector('img'))) result.appendChild(node.cloneNode(true));
      }
    }
    Array.prototype.forEach.call(root.childNodes, add);
    flush();
    return result.innerHTML;
  }

  /** A figure holds the picture and, after it, one caption line. */
  function fixFigure(fig) {
    var img = fig.querySelector('img');
    var cap = fig.querySelector('figcaption');
    while (fig.firstChild) fig.removeChild(fig.firstChild);
    if (img) fig.appendChild(img);
    if (img && cap && cap.textContent.trim()) fig.appendChild(cap);
  }

  // ---- keeping the pictures ----
  var IMPORT_URL = ctx.area.getAttribute('data-import-url');

  function isRemote(img) {
    var src = img.getAttribute('src') || '';
    try { return /^https?:\/\//i.test(src) && new URL(src).origin !== window.location.origin; } catch (err) { return false; }
  }

  /** Pictures that came with the paste still point at the other site; the server fetches each one and stores a copy, so the article keeps them. */
  async function keepPictures() {
    if (!IMPORT_URL) return;
    var pictures = Array.prototype.filter.call(ctx.editor.querySelectorAll('img'), isRemote);
    if (!pictures.length) return;

    var token = ctx.area.form && ctx.area.form.querySelector('input[name="__RequestVerificationToken"]');
    var kept = 0;
    var firstProblem = '';
    ctx.work(1);
    try {
      for (var i = 0; i < pictures.length; i++) {
        var img = pictures[i];
        if (!ctx.editor.contains(img)) continue; // removed while the others were being saved
        ctx.note('Đang lưu ảnh vào bài viết… (' + (i + 1) + '/' + pictures.length + ')');
        try {
          var data = new FormData(); data.append('url', img.getAttribute('src'));
          var response = await fetch(IMPORT_URL, { method: 'POST', headers: { 'RequestVerificationToken': token ? token.value : '' }, body: data });
          var body = await response.json();
          if (response.ok && body.url) { img.setAttribute('src', body.url); kept++; ctx.sync(); }
          else if (!firstProblem) firstProblem = body.error || 'Không lưu được ảnh.';
        } catch (err) {
          if (!firstProblem) firstProblem = 'Không kết nối được máy chủ.';
        }
      }
    } finally {
      ctx.work(-1);
    }
    ctx.note(kept === pictures.length
      ? 'Đã lưu ' + kept + ' ảnh vào bài viết.'
      : 'Đã lưu ' + kept + '/' + pictures.length + ' ảnh. Ảnh chưa lưu được vẫn dùng liên kết gốc: ' + firstProblem);
  }

  ctx.pasteHtml = function (e) {
    var html = e.clipboardData.getData('text/html');
    if (!html) return false;
    var clean;
    try {
      var doc = new DOMParser().parseFromString(html, 'text/html');
      var body = document.createDocumentFragment();
      copyChildren(doc.body, body, false);
      var holder = document.createElement('div');
      holder.appendChild(body);
      clean = tidy(holder);
    } catch (err) {
      return false; // anything unexpected: the plain-text paste takes over
    }
    if (!clean || (!/<img/.test(clean) && clean.replace(/<[^>]*>/g, '').trim() === '')) return false;

    e.preventDefault();
    document.execCommand('insertHTML', false, clean);
    ctx.sync();
    keepPictures();
    return true;
  };
})();
