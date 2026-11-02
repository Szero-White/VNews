/* Article editor module: the text is kept as a draft in this browser while you write, and offered back if the page was lost before saving.
   Plugs into the core (body-editor.js) through window.VNewsRte. Nothing leaves the browser. */
(function () {
  'use strict';

  var ctx = window.VNewsRte;
  if (!ctx || ctx.readOnly) return;

  var area = ctx.area;
  var el = ctx.el;
  var DRAFT_KEY = 'vnews.body-draft.' + location.pathname;
  var serverValue = area.value; // what the server gave us: a draft identical to it is not worth offering
  var timer = null;

  function store(value) {
    try {
      if (value === null) localStorage.removeItem(DRAFT_KEY);
      else localStorage.setItem(DRAFT_KEY, JSON.stringify({ html: value, at: Date.now() }));
    } catch (e) { /* storage blocked: no draft */ }
  }

  function scheduleDraft() {
    clearTimeout(timer);
    timer = setTimeout(function () {
      if (area.value === serverValue) { store(null); return; }
      store(area.value);
      var d = new Date();
      ctx.note('Đã lưu nháp trong trình duyệt lúc ' + ('0' + d.getHours()).slice(-2) + ':' + ('0' + d.getMinutes()).slice(-2));
    }, 1500);
  }

  function offerSavedDraft() {
    var saved;
    try { saved = JSON.parse(localStorage.getItem(DRAFT_KEY) || 'null'); } catch (e) { return; } // unreadable draft: ignore
    if (!saved || typeof saved.html !== 'string' || saved.html === serverValue || !saved.html.trim()) return;

    var banner = el('div', 'rte__draft');
    var when = new Date(saved.at || Date.now()).toLocaleString('vi-VN');
    banner.innerHTML = '<span>Có bản nháp chưa lưu của nội dung này (' + when + ').</span>';
    var restore = el('button', 'btn btn--secondary btn--sm'); restore.type = 'button'; restore.textContent = 'Khôi phục';
    var drop = el('button', 'btn btn--ghost btn--sm'); drop.type = 'button'; drop.textContent = 'Bỏ bản nháp';
    restore.addEventListener('click', function () { ctx.setContent(saved.html); banner.hidden = true; });
    drop.addEventListener('click', function () { store(null); banner.hidden = true; });
    banner.appendChild(restore); banner.appendChild(drop);
    ctx.wrap.insertBefore(banner, ctx.bar);
  }

  ctx.afterSync.push(scheduleDraft);
  offerSavedDraft();
  if (ctx.form) {
    ctx.form.addEventListener('submit', function () { clearTimeout(timer); store(null); }, true); // saved for real: no draft needed
    // saved in place (the page stays): what is on screen is now the stored text, so it is the new baseline
    ctx.form.addEventListener('vnews:saved', function () { serverValue = area.value; clearTimeout(timer); store(null); });
    // the save failed after the draft was cleared: keep the text safe again
    ctx.form.addEventListener('vnews:save-failed', scheduleDraft);
  }
})();
