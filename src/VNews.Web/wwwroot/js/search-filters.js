/* Search page: choosing a time, category or kind applies it at once (the form is a normal GET, so it also works without this script). */
(function () {
  'use strict';
  document.querySelectorAll('select[data-auto-submit]').forEach(function (select) {
    select.addEventListener('change', function () { select.form.submit(); });
  });
})();
