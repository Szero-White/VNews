/* Bulk selection in the CMS lists: "Xóa (n)" on the article lists, "Khôi phục (n)" in the trash, "Gỡ (n)" / "Khôi phục (n)" for comments.
   Row checkboxes ([data-bulk-item]) are looked up each time, because rows leave or change state when an action works in place
   (list-actions.js). A bulk form ([data-bulk-form]) with data-bulk-status only counts the ticked rows that are in that state, so the
   right button appears for the right rows. This script keeps the UI in step: the "select all" box, the counts on the buttons, the
   confirmation texts, and showing each button (top right) only while it has something to act on. The server still checks every item. */
(function () {
  'use strict';

  var forms = Array.prototype.slice.call(document.querySelectorAll('[data-bulk-form]'));
  if (!forms.length) return;

  var all = document.querySelector('[data-bulk-all]');

  function items() { return Array.prototype.slice.call(document.querySelectorAll('[data-bulk-item]')); }
  function stateOf(box) { var row = box.closest('tr'); return row ? row.getAttribute('data-status') : null; }

  function update() {
    var boxes = items();
    var ticked = boxes.filter(function (box) { return box.checked; });
    forms.forEach(function (form) {
      var wanted = form.getAttribute('data-bulk-status');
      var n = ticked.filter(function (box) { return !wanted || stateOf(box) === wanted; }).length;
      form.hidden = n === 0;
      var count = form.querySelector('[data-bulk-count]');
      if (count) count.textContent = String(n);
      form.setAttribute('data-confirm', (form.getAttribute('data-bulk-template') || '{n} mục đã chọn?').replace('{n}', String(n)));
    });
    if (all) {
      all.disabled = boxes.length === 0;
      all.checked = boxes.length > 0 && ticked.length === boxes.length;
      all.indeterminate = ticked.length > 0 && ticked.length < boxes.length;
    }
    boxes.forEach(function (box) {
      var row = box.closest('tr');
      if (row) row.classList.toggle('is-selected', box.checked);
    });
  }

  document.addEventListener('change', function (e) { if (e.target.matches && e.target.matches('[data-bulk-item]')) update(); });
  if (all) {
    all.addEventListener('change', function () {
      items().forEach(function (box) { box.checked = all.checked; });
      update();
    });
  }
  document.addEventListener('vnews:rows-removed', update);
  update(); // browsers may restore ticked boxes when the page is revisited
})();
