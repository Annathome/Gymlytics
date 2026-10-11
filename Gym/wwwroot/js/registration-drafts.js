// Registration drafts stay in this browser tab. Never store credentials or files.
window.addEventListener('load', () => {
    const user = document.getElementById('registration-draft-context')?.dataset.user || 'guest';
    const prefix = `registration-draft:${user}:`;
    const pending = prefix + 'submitted';
    const read = key => { try { return JSON.parse(sessionStorage.getItem(key)); } catch { return null; } };
    const write = (key, value) => { try { sessionStorage.setItem(key, JSON.stringify(value)); } catch { /* Storage may be disabled. */ } };
    const remove = key => { try { sessionStorage.removeItem(key); } catch { } };
    if (document.querySelector('[data-registration-succeeded]')) {
        const submitted = read(pending);
        if (typeof submitted === 'string' && submitted.startsWith(prefix)) remove(submitted);
        remove(pending);
    }
    document.querySelectorAll('form').forEach(form => {
        const action = new URL(form.action, location.href).pathname;
        if (!/\/(AddMember|WalkInRegister|RegisterStaff|RegisterAdmin)\/?$/i.test(action) || form.method.toLowerCase() !== 'post') return;
        const key = prefix + action.toLowerCase();
        const fields = () => [...form.elements].filter(field => field.name &&
            /^(INPUT|SELECT|TEXTAREA)$/.test(field.tagName) &&
            !['password', 'file', 'hidden', 'submit', 'button'].includes(field.type) &&
            !/password|token/i.test(field.name));
        let restoring = true;
        const save = () => {
            if (restoring) return;
            const values = {};
            fields().forEach(field => {
                if (['checkbox', 'radio'].includes(field.type)) {
                    if (!values[field.name]) values[field.name] = [];
                    if (field.checked) values[field.name].push(field.value);
                } else if (field.multiple) {
                    values[field.name] = [...field.selectedOptions].map(option => option.value);
                } else values[field.name] = field.value;
            });
            write(key, { values, days: [...form.querySelectorAll('.day-button.selected')].map(button => button.dataset.day) });
        };
        const draft = read(key);
        // A validation response already contains the server's submitted values.
        const hasErrors = form.querySelector('.field-validation-error, .validation-summary-errors');
        if (draft?.values && !hasErrors) {
            fields().forEach(field => {
                if (!Object.hasOwn(draft.values, field.name)) return;
                const value = draft.values[field.name];
                if (['checkbox', 'radio'].includes(field.type)) field.checked = Array.isArray(value) && value.includes(field.value);
                else if (field.multiple) [...field.options].forEach(option => { option.selected = Array.isArray(value) && value.includes(option.value); });
                else if (!field.readOnly) field.value = field.dataset.phoneDigits
                    ? String(value).replace(/[^0-9]/g, '').slice(0, 11) : value;
                // Rebuild dependent trainer/time options and current plan prices.
                field.dispatchEvent(new Event('change', { bubbles: true }));
            });
            form.querySelectorAll('.day-button').forEach(button => {
                if (!button.disabled && button.classList.contains('selected') !== (draft.days || []).includes(button.dataset.day)) button.click();
            });
            const notice = document.createElement('p');
            notice.setAttribute('role', 'status');
            notice.textContent = 'Your unfinished registration has been restored. Review the details before registering.';
            notice.style.color = '#b7ff1b';
            form.prepend(notice);
        }
        restoring = false;
        form.addEventListener('input', save);
        form.addEventListener('change', save);
        form.addEventListener('click', () => queueMicrotask(save));
        form.addEventListener('submit', () => { save(); write(pending, key); });
        window.addEventListener('pagehide', save);
    });
});
