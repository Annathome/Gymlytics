// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Apply the shared Cancel-button hover treatment to role pages and to buttons
// inserted later by modal/dialog scripts.
(() => {
    const markCancelButtons = (root) => {
        const candidates = [];
        if (root instanceof Element && root.matches('button, a, input[type="button"], input[type="submit"]')) {
            candidates.push(root);
        }
        if (root.querySelectorAll) {
            candidates.push(...root.querySelectorAll('button, a, input[type="button"], input[type="submit"]'));
        }

        candidates.forEach((element) => {
            const label = element instanceof HTMLInputElement
                ? element.value
                : element.textContent;
            if ((label || '').trim().toLowerCase() === 'cancel') {
                element.classList.add('cancel-hover-lime');
            }
        });
    };

    document.addEventListener('DOMContentLoaded', () => {
        markCancelButtons(document);
        new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === Node.ELEMENT_NODE) markCancelButtons(node);
                });
            });
        }).observe(document.body, { childList: true, subtree: true });
    });
})();
