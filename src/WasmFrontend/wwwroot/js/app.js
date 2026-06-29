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
        document.getElementById('mobileNavOverlay').classList.toggle('show');
        document.body.classList.toggle('nav-open');
    },

    closeMobileNav() {
        document.getElementById('mobileNavOverlay').classList.remove('show');
        document.body.classList.remove('nav-open');
    },

    animateCartBadge(count) {
        const badge = document.getElementById('cartBadge');
        if (!badge) return;
        badge.textContent = count;
        badge.classList.remove('cart-bounce');
        void badge.offsetWidth;
        if (count > 0) {
            badge.classList.add('cart-bounce');
            badge.style.display = 'flex';
        } else {
            badge.style.display = 'none';
        }
    },

    showFloatingMsg(msg, type = 'success') {
        const container = document.getElementById('floatingMsgContainer');
        if (!container) return;
        const el = document.createElement('div');
        el.className = `floating-msg ${type} animate-slide-up`;
        el.textContent = msg;
        container.appendChild(el);
        setTimeout(() => { el.classList.add('fade-out'); setTimeout(() => el.remove(), 300); }, 2500);
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
