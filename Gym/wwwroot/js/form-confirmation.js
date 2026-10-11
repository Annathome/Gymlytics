(() => {
    function showConfirmation() {
        const notice = document.querySelector('[data-confirmation-action]');
        if (!notice) return;
        const action = notice.dataset.confirmationAction;
        const form = [...document.forms].find(form => form.method.toLowerCase() === 'post' &&
            new URL(form.action, location.href).pathname.split('/').filter(Boolean).pop() === action);
        if (form) {
            form.prepend(notice);
            const dialog = form.closest('dialog');
            if (dialog && !dialog.open) dialog.showModal();
            const modal = form.closest('.ae-modal, .ap-modal, .eq-overlay');
            if (modal && !modal.classList.contains('eq-overlay')) modal.classList.add('open');
            if (modal?.classList.contains('eq-overlay')) modal.style.display = 'flex';
        }
        notice.scrollIntoView({ block: 'nearest' });
        notice.focus({ preventScroll: true });
    }
    if (document.readyState === 'complete') showConfirmation();
    else window.addEventListener('load', showConfirmation, { once: true });
})();
