/* Row actions that work in place. data-inplace="update" (comments) changes the state of the rows instead of removing them: the server returns the
   new state of each comment; a row that no longer belongs to the list's status filter leaves, the others switch badge and button. A bulk
   form with data-bulk-status posts the ids of the ticked rows that are in that state. A form marked data-inplace (delete / restore in the article list, and "restore selected") is posted with fetch
   once the shared confirmation dialog says yes. The server answers JSON; the page keeps its scroll position and everything else on screen,
   the finished rows fade out, the figures on the page are brought up to date, and a toast tells what happened.
   Anything that is not a JSON answer (for example the sign-in page after the session ended) falls back to a normal submit. */
(function () {
  'use strict';

  var forms = document.querySelectorAll('form[data-inplace]');
  if (!forms.length) return;

  function toast(message, kind) {
    if (window.VNewsToast) window.VNewsToast.show(message, kind);
    else window.alert(message);
  }

  /** The rows an answer is about: the row that holds the form, or the rows of the ticked boxes for "restore selected". */
  function rowsFor(form, ids) {
    var own = form.closest('tr');
    if (own) return [own];
    return Array.prototype.slice.call(document.querySelectorAll('[data-bulk-item]')).filter(function (box) {
      return ids.indexOf(parseInt(box.value, 10)) !== -1;
    }).map(function (box) { return box.closest('tr'); }).filter(Boolean);
  }

  function setText(selector, text) {
    document.querySelectorAll(selector).forEach(function (node) { node.textContent = text; });
  }

  /** Figures that depend on the row count: the totals in the list header and the pager. */
  function lowerTotals(removed) {
    document.querySelectorAll('[data-list-total]').forEach(function (node) {
      var n = parseInt(node.textContent, 10);
      if (!isNaN(n)) node.textContent = String(Math.max(0, n - removed));
    });
    document.querySelectorAll('[data-list-to]').forEach(function (node) {
      var n = parseInt(node.textContent, 10);
      if (!isNaN(n)) node.textContent = String(Math.max(0, n - removed));
    });
  }

  /** The counters of the page (draft / waiting / returned / published cards and the trash button) from the server's fresh figures. */
  function updateStats(stats) {
    if (!stats) return;
    var map = { Draft: stats.draft, PendingReview: stats.pendingReview, Rejected: stats.rejected, Published: stats.published };
    Object.keys(map).forEach(function (key) { setText('[data-stat="' + key + '"]', String(map[key])); });
    setText('[data-stat-trash]', stats.trashed > 0 ? ' (' + stats.trashed + ')' : '');
  }

  /** The numbers on the status tiles, from the server's fresh counts. */
  function updateCounts(counts) {
    if (!counts) return;
    var all = 0;
    Object.keys(counts).forEach(function (key) { all += counts[key]; setText('[data-count="' + key + '"]', String(counts[key])); });
    setText('[data-count="All"]', String(all));
  }

  /** Comments: apply the new states. Returns the rows that left the list because they no longer match the page's status filter. */
  function applyStates(states) {
    var filter = (document.querySelector('[data-filter-status]') || {}).getAttribute ? document.querySelector('[data-filter-status]').getAttribute('data-filter-status') : '';
    var leaving = [];
    Object.keys(states || {}).forEach(function (id) {
      var row = document.querySelector('[data-comment-row][data-id="' + id + '"]');
      if (!row) return;
      var box = row.querySelector('[data-bulk-item]');
      if (box) box.checked = false;
      if (filter && states[id] !== filter) leaving.push(row); else row.setAttribute('data-status', states[id]);
    });
    return leaving;
  }

  function removeRows(rows, done) {
    if (!rows.length) { done(); return; }
    rows.forEach(function (row) { row.classList.add('is-removing'); });
    setTimeout(function () {
      var body = rows[0].parentNode;
      rows.forEach(function (row) { row.remove(); });
      if (body && !body.querySelector('tr')) { window.location.reload(); return; } // the page ran out of rows: show the list again
      done();
    }, 280);
  }

  forms.forEach(function (form) {
    var busy = false;

    form.addEventListener('vnews:confirmed', async function () {
      if (busy) return;
      busy = true;
      var buttons = form.querySelectorAll('button');
      buttons.forEach(function (b) { b.disabled = true; });
      try {
        var data = new FormData(form);
        var wantedStatus = form.getAttribute('data-bulk-status');
        if (wantedStatus) { // a bulk form for one state: the ticked rows that are in it
          document.querySelectorAll('[data-bulk-item]').forEach(function (box) {
            var row = box.closest('tr');
            if (box.checked && row && row.getAttribute('data-status') === wantedStatus) data.append('ids', box.value);
          });
        } else if (form.hasAttribute('data-bulk-collect')) { // a second bulk form beside #bulk-form (the boxes belong to that one): take every ticked row
          document.querySelectorAll('[data-bulk-item]').forEach(function (box) { if (box.checked) data.append('ids', box.value); });
        }
        var response = await fetch(form.action, {
          method: 'POST',
          body: data,
          credentials: 'same-origin',
          headers: { 'X-Requested-With': 'fetch', 'Accept': 'application/json' }
        });
        if ((response.headers.get('content-type') || '').indexOf('application/json') === -1) {
          form.submit(); // not a JSON answer: let the browser handle it the ordinary way
          return;
        }
        var result = await response.json();
        if (!result.ok) { toast(result.message || 'Không thực hiện được. Vui lòng thử lại.', 'error'); return; }

        var rows = result.states ? applyStates(result.states) : rowsFor(form, result.ids || []);
        updateStats(result.stats);
        updateCounts(result.counts);
        removeRows(rows, function () {
          if (rows.length) lowerTotals(rows.length);
          document.dispatchEvent(new CustomEvent('vnews:rows-removed'));
        });
        toast(result.message || 'Đã hoàn tất.', 'success');
      } catch (err) {
        toast('Không kết nối được máy chủ. Vui lòng thử lại.', 'error');
      } finally {
        busy = false;
        buttons.forEach(function (b) { b.disabled = false; });
      }
    });
  });
})();
