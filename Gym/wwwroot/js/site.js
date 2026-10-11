// Keep report fields and selected evidence in the live form on server restrictions.
document.addEventListener('submit', async event => {
    const form = event.target;
    if (event.defaultPrevented || form.method.toLowerCase() !== 'post' ||
        !new URL(form.action, location.href).pathname.endsWith('/AddEquipment')) return;
    event.preventDefault();
    if (form.dataset.reportSubmitting) return;
    form.dataset.reportSubmitting = 'true';
    const button = event.submitter;
    const wasDisabled = button?.disabled;
    if (button) button.disabled = true;
    const showErrors = messages => {
        let box = form.querySelector('.form-error-summary');
        if (!box) {
            box = document.createElement('div');
            box.className = 'form-error-summary';
            box.setAttribute('role', 'alert');
            box.tabIndex = -1;
            form.prepend(box);
        }
        box.replaceChildren();
        for (const message of messages) {
            const line = document.createElement('p');
            line.textContent = message;
            box.append(line);
        }
        box.hidden = false;
        box.focus();
    };
    try {
        const response = await fetch(form.action, {
            method: 'POST', body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!response.headers.get('content-type')?.includes('application/json')) {
            showErrors(['Your entries are still here. The response could not be confirmed; check the report list before submitting again.']);
            return;
        }
        const result = await response.json();
        if (response.status === 422) showErrors(result.errors);
        else if (response.ok && result.redirectUrl) location.assign(result.redirectUrl);
        else showErrors(['Unable to confirm submission. Your entries have been kept. Check the report list before trying again.']);
    } catch {
        showErrors(['Connection interrupted. Your entries and photo have been kept. Check the report list before trying again.']);
    } finally {
        delete form.dataset.reportSubmitting;
        if (button) button.disabled = wasDisabled;
    }
});

// Shared registration feedback for standalone pages and embedded role dialogs.
(() => {
    if (window.registrationProgressLoaded) return;
    window.registrationProgressLoaded = true;
    const pending = new Map();
    document.addEventListener('submit', event => {
        const form = event.target;
        if (event.defaultPrevented || form.method.toLowerCase() !== 'post') return;
        const action = new URL(form.action, location.href).pathname.split('/').filter(Boolean).pop();
        if (!['AddMember', 'WalkInRegister', 'RegisterStaff', 'RegisterAdmin'].includes(action)) return;
        if (pending.has(form)) { event.preventDefault(); return; }
        const status = document.createElement('p');
        status.setAttribute('role', 'status');
        status.setAttribute('aria-live', 'polite');
        status.textContent = action === 'RegisterStaff'
            ? 'Submitting registration and sending the invitation email. Please wait…'
            : action === 'RegisterAdmin'
                ? 'Submitting registration. Please wait…'
                : 'Submitting registration and preparing the receipt. Please wait…';
        form.prepend(status);
        form.setAttribute('aria-busy', 'true');
        const buttons = [...form.querySelectorAll('button[type="submit"], input[type="submit"]')]
            .map(button => ({ button, disabled: button.disabled }));
        buttons.forEach(({ button }) => { button.disabled = true; });
        pending.set(form, { status, buttons });
    });
    // Back/forward navigation can restore the old form from the browser cache.
    window.addEventListener('pageshow', () => {
        for (const [form, { status, buttons }] of pending) {
            form.removeAttribute('aria-busy');
            status.remove();
            buttons.forEach(({ button, disabled }) => { button.disabled = disabled; });
        }
        pending.clear();
    });
})();

// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Dismiss transient menus across layouts, including menus inserted by modals.
// Capture clicks so page handlers calling stopPropagation cannot trap a menu open.
(() => {
    const dismissMenus = (target, escape = false) => {
        document.querySelectorAll('details[open]').forEach(menu => {
            if (escape || !menu.contains(target)) menu.open = false;
        });
        document.querySelectorAll('.action-menu.open, #exportMenu.open, #adminLogout.show').forEach(menu => {
            const trigger = menu.previousElementSibling;
            const insideTrigger = trigger?.matches('button, a, summary') && trigger.contains(target);
            const logoutTrigger = menu.id === 'adminLogout' && target?.closest?.('.admin-Popup-btn');
            if (!escape && (menu.contains(target) || insideTrigger || logoutTrigger)) return;
            menu.classList.remove('open', 'show');
            if (trigger?.hasAttribute('aria-expanded')) trigger.setAttribute('aria-expanded', 'false');
        });
        // Let Bootstrap manage its own state and accessibility attributes.
        document.querySelectorAll('[data-bs-toggle="dropdown"][aria-expanded="true"]').forEach(trigger => {
            if (escape || !trigger.parentElement.contains(target)) {
                window.bootstrap?.Dropdown?.getInstance(trigger)?.hide();
            }
        });
    };
    document.addEventListener('click', event => dismissMenus(event.target), true);
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') dismissMenus(null, true);
    });
})();

// Mirror server validation in all layouts, including dynamically inserted forms.
(() => {
    const phoneNames = new Set(['phonenumber', 'contactnumber', 'emergencycontact']);
    const validateBirthDate = field => {
        const today = new Date();
        const [year, month, day] = field.value.split('-').map(Number);
        let age = today.getFullYear() - year;
        if (month > today.getMonth() + 1 ||
            (month === today.getMonth() + 1 && day > today.getDate())) age--;
        const restricted = Boolean(field.value && (!year || !month || !day || age < 13));
        const attempted = field.form?.dataset.ageValidationAttempted === 'true';
        field.setCustomValidity(attempted && restricted ? 'Registrant must be at least 13 years old.' : '');
        const hint = document.getElementById(field.dataset.ageHint);
        if (hint) hint.hidden = !(attempted && restricted);
    };
    const configure = root => {
        const fields = root.querySelectorAll ? [...root.querySelectorAll('input, textarea, select')] : [];
        if (root.matches?.('input, textarea, select')) fields.unshift(root);
        fields.forEach(field => {
            const name = (field.name || '').split('.').pop().toLowerCase();
            if (['birthday', 'dateofbirth', 'birthdate'].includes(name)) {
                field.type = 'date';
                field.removeAttribute('max');
                // Start a fresh calendar on the local current date, not the age cutoff.
                // Existing values (including restored drafts) remain untouched.
                if (!field.dataset.registrationBirthDate && (!field.value || field.value === '0001-01-01')) {
                    const today = new Date();
                    field.value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
                }
                field.title = 'Registrant must be at least 13 years old.';
                field.dataset.registrationBirthDate = 'true';
                if (!field.dataset.ageHint) {
                    const hint = document.createElement('small');
                    hint.id = `age-hint-${document.querySelectorAll('[data-age-hint]').length}`;
                    hint.className = 'registration-age-hint';
                    hint.setAttribute('role', 'status');
                    hint.textContent = 'Registrant must be at least 13 years old. Please correct the date of birth before submitting.';
                    hint.hidden = true;
                    field.dataset.ageHint = hint.id;
                    field.insertAdjacentElement('afterend', hint);
                    field.setAttribute('aria-describedby', `${field.getAttribute('aria-describedby') || ''} ${hint.id}`.trim());
                }
                validateBirthDate(field);
            }
            if (phoneNames.has(name)) {
                field.type = 'tel';
                field.inputMode = 'numeric';
                field.maxLength = 11;
                field.minLength = 11;
                field.pattern = '[0-9]{11}';
                field.title = 'Enter exactly 11 digits (0–9). Letters, spaces, and symbols are not allowed.';
                field.placeholder = '09123456789';
                field.dataset.phoneDigits = 'true';
                if (!field.dataset.phoneHint) {
                    const hint = document.createElement('small');
                    hint.id = `phone-hint-${document.querySelectorAll('[data-phone-hint]').length}`;
                    hint.textContent = `${name === 'emergencycontact' ? 'Optional. If provided: ' : ''}11 digits only (0–9), with no letters, spaces, or symbols. Example: 09123456789.`;
                    hint.style.cssText = 'display:block;margin-top:6px;color:#c4c9c6;';
                    field.insertAdjacentElement('afterend', hint);
                    field.setAttribute('aria-describedby', `${field.getAttribute('aria-describedby') || ''} ${hint.id}`.trim());
                    field.dataset.phoneHint = hint.id;
                }
            }
            const max = field.dataset.valLengthMax || field.dataset.valMaxlengthMax;
            const min = field.dataset.valLengthMin || field.dataset.valMinlengthMin;
            if (max) field.maxLength = Number(max);
            if (min) field.minLength = Number(min);
            if (field.dataset.valRequired && !['checkbox', 'radio', 'hidden'].includes(field.type)) field.required = true;
            if (field.dataset.valEmail || name === 'email') field.type = 'email';
            // Plain HTML modal inputs do not carry MVC validation attributes.
            const textLimits = { firstname: 100, lastname: 100, fullname: 100,
                email: 100, reasonforvisit: 50 };
            if (!max && textLimits[name]) field.maxLength = textLimits[name];
            if (field.type === 'number') {
                if (field.dataset.valRangeMin) field.min = field.dataset.valRangeMin;
                if (field.dataset.valRangeMax) field.max = field.dataset.valRangeMax;
            }
        });
    };
    document.addEventListener('input', event => {
        const field = event.target;
        if (field.dataset?.phoneDigits) field.value = field.value.replace(/[^0-9]/g, '').slice(0, 11);
        if (field.dataset?.registrationBirthDate) {
            validateBirthDate(field);
        }
    });
    document.addEventListener('change', event => {
        if (event.target.dataset?.registrationBirthDate) validateBirthDate(event.target);
    });
    // The summary requests fresh validity before collecting errors, independent
    // of script/listener order and including restored or programmatic values.
    const validateFormBirthDates = form => {
        const fields = form.querySelectorAll('[data-registration-birth-date]');
        if (fields.length) form.dataset.ageValidationAttempted = 'true';
        fields.forEach(validateBirthDate);
        return [...fields].find(field => !field.validity.valid);
    };
    document.addEventListener('form-validation-requested', event => {
        validateFormBirthDates(event.target);
    });
    // Keep native validation as a fallback on pages without the shared summary.
    document.addEventListener('submit', event => {
        if (window.formErrorSummaryLoaded || event.submitter?.formNoValidate) return;
        const invalid = validateFormBirthDates(event.target);
        if (invalid) {
            event.preventDefault();
            event.stopImmediatePropagation();
            invalid.reportValidity();
        }
    }, true);
    document.addEventListener('DOMContentLoaded', () => {
        configure(document);
        new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(configure)))
            .observe(document.body, { childList: true, subtree: true });
    });
})();

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
