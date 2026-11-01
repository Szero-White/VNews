/* VNews public site scripts, module 2 of 6: page chrome. Today's date, the scrolling category strip with its "next" arrow, the mobile
   utility drawer, back-to-top and the reading-progress bar. Exposes VNews.closeDrawer() for the login dialog. */
(function () {
  'use strict';

  var VNews = window.VNews;

  // ---- date label ----
  var dateEl = document.getElementById('today-date');
  if (dateEl) {
    var days = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];
    var now = new Date();
    dateEl.textContent = days[now.getDay()] + ', ' + now.toLocaleDateString('vi-VN');
  }

  // ---- category strip: scrollable list with a round "next" button that appears once the list overflows ----
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

  // ---- mobile utility drawer ----
  var toggle = document.querySelector('.masthead__menu-toggle');
  var drawer = document.getElementById('utility-drawer');
  if (toggle && drawer) {
    var closeDrawer = function () {
      drawer.classList.remove('is-open');
      toggle.setAttribute('aria-expanded', 'false');
    };
    VNews.closeDrawer = closeDrawer;

    toggle.addEventListener('click', function () {
      var open = drawer.classList.toggle('is-open');
      toggle.setAttribute('aria-expanded', String(open));
      toggle.setAttribute('aria-label', open ? 'Đóng menu tiện ích' : 'Mở menu tiện ích');
    });
    drawer.addEventListener('click', function (e) {
      if (e.target.closest('a') && drawer.classList.contains('is-open')) closeDrawer();
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && drawer.classList.contains('is-open')) { closeDrawer(); toggle.focus(); }
    });
  }

  // ---- back-to-top and reading progress (one throttled scroll handler) ----
  var backTop = document.getElementById('back-to-top');
  var progress = document.getElementById('reading-progress');
  var article = document.querySelector('[data-news-article]');
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
})();
