// One accessible summary per form, shared by standalone pages and role layouts.
(() => {
    if (window.formErrorSummaryLoaded) return;
    window.formErrorSummaryLoaded = true;
    const states = new WeakMap();
    const labels = field => {
        if (field.labels?.length) return [...field.labels];
        // Legacy modal forms place an unassociated label immediately before a control.
        return field.previousElementSibling?.matches('label') ? [field.previousElementSibling] : [];
    };
    const label = field => (labels(field)[0]?.textContent || field.name || 'Field')
        .replace(/\*/g, '').trim().replace(/([a-z])([A-Z])/g, '$1 $2');
    const validName = field => {
        const name = field.value.trim().replace(/\s+/g, ' ');
        const validPart = part => part.length >= 2 && /\p{L}/u.test(part) &&
            /^[\p{L}\p{M} '\-’]+$/u.test(part) && !['na', 'none', 'unknown', 'null'].includes(part.toLowerCase());
        return field.dataset.registrationName === 'full'
            ? name.split(' ').length >= 2 && name.split(' ').every(validPart) : validPart(name);
    };
    function collect(form) {
        form.dispatchEvent(new Event('form-validation-requested', { bubbles: true }));
        const errors = [];
        for (const field of form.elements) {
            if (!field.willValidate) continue;
            let message = '';
            const v = field.validity;
            if (v.valueMissing || (field.required && typeof field.value === 'string' && !field.value.trim())) message = 'This field is required.';
            else if (field.dataset.registrationName && !validName(field)) message = field.dataset.registrationNameError;
            else if (field.dataset.phoneDigits && field.value && !/^[0-9]{11}$/.test(field.value)) message = 'Enter exactly 11 digits (0–9), without letters, spaces, or symbols.';
            else if (field.dataset.registrationBirthDate && field.validity.customError) message = field.validationMessage;
            else if (!v.valid) message = field.validationMessage;
            if (field.type === 'file' && field.name === 'EvidencePhoto' && field.files.length) {
                const file = field.files[0];
                if (file.size > 5 * 1024 * 1024) message = 'Use a photo no larger than 5 MB.';
                else if (!/\.(jpe?g|png|webp)$/i.test(file.name)) message = 'Use a JPG, PNG, or WebP photo.';
            }
            if (message) errors.push({ field, message: `${label(field)}: ${message}` });
        }
        const validator = window.jQuery?.(form).data('validator');
        if (validator) {
            validator.form();
            for (const error of validator.errorList) {
                if (!errors.some(item => item.field === error.element)) errors.push({ field: error.element, message: `${label(error.element)}: ${error.message}` });
            }
        }
        for (const error of states.get(form)?.serverErrors || []) {
            if (error.field && error.field.value === error.value && !errors.some(item => item.field === error.field))
                errors.push(error);
        }
        return errors;
    }
    function render(form, errors, focus = false) {
        const state = states.get(form);
        if (!state) return;
        state.box.replaceChildren();
        state.box.hidden = errors.length === 0;
        for (const field of state.marked) {
            field.removeAttribute('aria-invalid');
            for (const fieldLabel of labels(field)) fieldLabel.classList.remove('form-invalid-label');
        }
        state.marked = [];
        if (!errors.length) return;
        const title = document.createElement('strong');
        title.textContent = 'Please correct the following before submitting:';
        const list = document.createElement('ul');
        for (const { field, message } of errors) {
            const item = document.createElement('li');
            if (field && field.type !== 'hidden') {
                const link = document.createElement('a');
                link.href = '#';
                link.textContent = message;
                link.addEventListener('click', event => { event.preventDefault(); field.focus(); });
                item.append(link);
                field.setAttribute('aria-invalid', 'true');
                for (const fieldLabel of labels(field)) fieldLabel.classList.add('form-invalid-label');
                state.marked.push(field);
            } else item.textContent = message;
            list.append(item);
        }
        state.box.append(title, list);
        if (focus) { state.box.scrollIntoView({ block: 'nearest' }); state.box.focus({ preventScroll: true }); }
    }
    function initialize(root) {
        const forms = [...(root.querySelectorAll?.('form') || [])];
        if (root.matches?.('form')) forms.unshift(root);
        for (const form of forms) {
            if (states.has(form)) continue;
            const serverErrors = [...form.querySelectorAll('[data-valmsg-summary] li, .field-validation-error, [data-form-server-error]')]
                .map(node => {
                    const name = node.getAttribute('data-valmsg-for');
                    const field = name ? [...form.elements].find(input => input.name === name) : null;
                    return { message: node.textContent.trim(), field, value: field?.value };
                }).filter(error => error.message);
            // Prefer field-linked errors over duplicate MVC summary entries.
            const uniqueErrors = [...new Map(serverErrors.sort((a, b) => Number(!!a.field) - Number(!!b.field))
                .map(error => [error.message, error])).values()];
            const box = form.querySelector('.form-error-summary') || document.createElement('div');
            box.className = 'form-error-summary';
            box.tabIndex = -1;
            box.setAttribute('role', 'alert');
            box.hidden = true;
            form.prepend(box);
            form.classList.add('unified-form-errors');
            form.noValidate = true;
            states.set(form, { box, marked: [], attempted: serverErrors.length > 0, serverErrors: uniqueErrors });
            // A rejected POST must show current browser errors alongside server
            // errors immediately, without requiring another submit or edit.
            const combined = serverErrors.length
                ? [...collect(form), ...uniqueErrors.filter(error => !error.field)] : [];
            render(form, combined, serverErrors.length > 0);
        }
    }
    document.addEventListener('submit', event => {
        const form = event.target;
        initialize(form);
        if (event.submitter?.formNoValidate) return;
        states.get(form).attempted = true;
        const errors = collect(form);
        render(form, errors, errors.length > 0);
        if (errors.length) { event.preventDefault(); event.stopImmediatePropagation(); }
    }, true);
    const refreshSummary = event => {
        const form = event.target.form;
        if (form && states.get(form)?.attempted) render(form, collect(form));
    };
    document.addEventListener('input', refreshSummary);
    document.addEventListener('change', refreshSummary);
    const start = () => {
        initialize(document);
        new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(initialize)))
            .observe(document.body, { subtree: true, childList: true });
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})();
