(() => {
    // Registration dialogs embed this same confirmation page in a same-origin frame.
    let frame;
    try { frame = window.frameElement; } catch { return; }
    if (!frame) return;
    const card = document.querySelector('.completion-card');
    if (!card) return;
    frame.parentElement.classList.add('confirmation-frame-shell');
    const resize = () => { frame.style.height = `${Math.ceil(card.getBoundingClientRect().height)}px`; };
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(card);
    window.addEventListener('pagehide', () => {
        observer.disconnect();
        frame.style.removeProperty('height');
        frame.parentElement.classList.remove('confirmation-frame-shell');
    }, { once: true });
})();
