window.BurgerIAM = {
    observer: null,

    initScrollAnimations() {
        var self = window.BurgerIAM;
        document.body.classList.add('js-ready');
        self.observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('animate-visible');
                    self.observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.1, rootMargin: '0px 0px -50px 0px' });

        document.querySelectorAll('.animate-on-scroll').forEach(function (el) {
            self.observer.observe(el);
        });
    },

    observeNewElements() {
        var self = window.BurgerIAM;
        if (self.observer) {
            document.querySelectorAll('.animate-on-scroll:not(.animate-visible)').forEach(function (el) {
                self.observer.observe(el);
            });
        } else {
            self.initScrollAnimations();
        }
    },

    toggleMobileNav() {
        document.body.classList.toggle('drawer-open');
        var overlay = document.getElementById('mobileNavOverlay');
        if (overlay) overlay.classList.toggle('open');
        var hamburger = document.getElementById('hamburgerBtn');
        if (hamburger) hamburger.classList.toggle('active');
    },

    closeMobileNav() {
        document.body.classList.remove('drawer-open');
        var overlay = document.getElementById('mobileNavOverlay');
        if (overlay) overlay.classList.remove('open');
        var hamburger = document.getElementById('hamburgerBtn');
        if (hamburger) hamburger.classList.remove('active');
    },

    toggleCartDrawer() {
        var drawer = document.getElementById('cartDrawer');
        var overlay = document.getElementById('cartDrawerOverlay');
        if (!drawer) return;
        drawer.classList.toggle('open');
        if (overlay) overlay.classList.toggle('open');
        document.body.classList.toggle('nav-open');
    },

    openCartDrawer() {
        var drawer = document.getElementById('cartDrawer');
        var overlay = document.getElementById('cartDrawerOverlay');
        if (!drawer) return;
        drawer.classList.add('open');
        if (overlay) overlay.classList.add('open');
        document.body.classList.add('nav-open');
    },

    closeCartDrawer() {
        var drawer = document.getElementById('cartDrawer');
        var overlay = document.getElementById('cartDrawerOverlay');
        if (!drawer) return;
        drawer.classList.remove('open');
        if (overlay) overlay.classList.remove('open');
        document.body.classList.remove('nav-open');
    },

    animateCartBadge(count) {
        var badge = document.getElementById('cartBadge');
        if (!badge) return;
        badge.textContent = count;
        badge.classList.remove('bump');
        void badge.offsetWidth;
        if (count > 0) {
            badge.classList.add('bump');
            badge.style.display = 'flex';
        } else {
            badge.style.display = 'none';
        }
    },

    showToast: function (msg, type) {
        if (type === undefined) type = 'success';
        var container = document.getElementById('toastContainer');
        if (!container) return;
        var el = document.createElement('div');
        el.className = 'toast ' + type;
        var icon = type === 'success' ? '\u2705' : type === 'error' ? '\u274C' : '\u2139\uFE0F';
        var text = document.createElement('span');
        text.textContent = msg;
        el.innerHTML = '<span>' + icon + '</span>';
        el.appendChild(text);
        container.appendChild(el);
        setTimeout(function () {
            el.classList.add('hide');
            setTimeout(function () { el.remove(); }, 300);
        }, 2500);
    },

    initSmoothScrolling() {
        document.querySelectorAll('a[href^="#"]').forEach(function (a) {
            a.addEventListener('click', function (e) {
                e.preventDefault();
                var target = document.querySelector(a.getAttribute('href'));
                if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
        });
    },

    initReviewsCarousel: function (totalCards) {
        var self = window.BurgerIAM;
        if (self._reviewTimer) clearInterval(self._reviewTimer);
        var track = document.getElementById('reviewsTrack');
        if (!track) return;
        var cardWidth = track.querySelector('.review-card')?.offsetWidth || 270;
        var gap = 16;
        var step = cardWidth + gap;
        self._reviewTimer = setInterval(function () {
            var offset = parseInt(track.getAttribute('data-offset') || '0', 10);
            var visible = window.innerWidth <= 480 ? 1 : window.innerWidth <= 768 ? 2 : 3;
            var maxOffset = Math.max(0, totalCards - visible);
            offset = offset + 1 > maxOffset ? 0 : offset + 1;
            track.setAttribute('data-offset', offset);
            track.style.transform = 'translateX(-' + (offset * step) + 'px)';
        }, 4000);
    },

    destroyReviewsCarousel: function () {
        var self = window.BurgerIAM;
        if (self._reviewTimer) {
            clearInterval(self._reviewTimer);
            self._reviewTimer = null;
        }
        var track = document.getElementById('reviewsTrack');
        if (track) {
            track.removeAttribute('data-offset');
            track.style.transform = '';
        }
    }
};

document.addEventListener('DOMContentLoaded', function () {
    window.BurgerIAM.initScrollAnimations();
    window.BurgerIAM.initSmoothScrolling();
});