window.BurgerIAM = {
    observer: null,

    initScrollAnimations() {
        this.observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('animate-visible');
                    this.observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.1, rootMargin: '0px 0px -50px 0px' });

        document.querySelectorAll('.animate-on-scroll').forEach(el => {
            this.observer.observe(el);
        });
    },

    observeNewElements() {
        if (this.observer) {
            document.querySelectorAll('.animate-on-scroll:not(.animate-visible)').forEach(el => {
                this.observer.observe(el);
            });
        } else {
            this.initScrollAnimations();
        }
    },

    toggleMobileNav() {
        document.body.classList.toggle('drawer-open');
        const overlay = document.getElementById('mobileNavOverlay');
        if (overlay) overlay.classList.toggle('open');
        const hamburger = document.getElementById('hamburgerBtn');
        if (hamburger) hamburger.classList.toggle('active');
    },

    closeMobileNav() {
        document.body.classList.remove('drawer-open');
        const overlay = document.getElementById('mobileNavOverlay');
        if (overlay) overlay.classList.remove('open');
        const hamburger = document.getElementById('hamburgerBtn');
        if (hamburger) hamburger.classList.remove('active');
    },

    toggleCartDrawer() {
        const drawer = document.getElementById('cartDrawer');
        const overlay = document.getElementById('cartDrawerOverlay');
        if (!drawer) return;
        drawer.classList.toggle('open');
        if (overlay) overlay.classList.toggle('open');
        document.body.classList.toggle('nav-open');
    },

    openCartDrawer() {
        const drawer = document.getElementById('cartDrawer');
        const overlay = document.getElementById('cartDrawerOverlay');
        if (!drawer) return;
        drawer.classList.add('open');
        if (overlay) overlay.classList.add('open');
        document.body.classList.add('nav-open');
    },

    closeCartDrawer() {
        const drawer = document.getElementById('cartDrawer');
        const overlay = document.getElementById('cartDrawerOverlay');
        if (!drawer) return;
        drawer.classList.remove('open');
        if (overlay) overlay.classList.remove('open');
        document.body.classList.remove('nav-open');
    },

    animateCartBadge(count) {
        const badge = document.getElementById('cartBadge');
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

    showToast(msg, type = 'success') {
        const container = document.getElementById('toastContainer');
        if (!container) return;
        const el = document.createElement('div');
        el.className = `toast ${type}`;
        const icon = type === 'success' ? '\u2705' : type === 'error' ? '\u274C' : '\u2139\uFE0F';
        el.innerHTML = `<span>${icon}</span><span>${this.escapeHtml(msg)}</span>`;
        container.appendChild(el);
        setTimeout(() => {
            el.classList.add('hide');
            setTimeout(() => el.remove(), 300);
        }, 2500);
    },

    escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    },

    initSmoothScrolling() {
        document.querySelectorAll('a[href^="#"]').forEach(a => {
            a.addEventListener('click', e => {
                e.preventDefault();
                const target = document.querySelector(a.getAttribute('href'));
                if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
        });
    }
};

document.addEventListener('DOMContentLoaded', () => {
    window.BurgerIAM.initScrollAnimations();
    window.BurgerIAM.initSmoothScrolling();
});
