(function () {
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
            var isOpen = nav.classList.toggle('is-open');
            toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });
    }
})();
