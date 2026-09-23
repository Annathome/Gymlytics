document.addEventListener('DOMContentLoaded', () => {
    const selector = '.am-filters select, .ap-filter select, .dropdown-filters select, select.pay-status';
    document.querySelectorAll(selector).forEach(select => {
        const menu = document.createElement('details');
        menu.className = 'styled-filter';
        const trigger = document.createElement('summary');
        trigger.setAttribute('aria-label', select.getAttribute('aria-label') || select.name);
        const options = document.createElement('div');
        options.className = 'styled-filter-options';
        const sync = () => { trigger.textContent = select.selectedOptions[0]?.textContent || ''; };
        Array.from(select.options).forEach(option => {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = option.textContent;
            button.disabled = option.disabled;
            button.addEventListener('click', () => {
                select.value = option.value;
                sync();
                menu.open = false;
                trigger.focus();
                select.dispatchEvent(new Event('change', { bubbles: true }));
            });
            options.append(button);
        });
        menu.append(trigger, options);
        select.after(menu);
        select.hidden = true;
        select.style.setProperty('display', 'none', 'important');
        sync();
        select.addEventListener('change', sync);
        menu.addEventListener('toggle', () => {
            if (menu.open) document.querySelectorAll('.styled-filter[open]').forEach(other => {
                if (other !== menu) other.open = false;
            });
        });
        menu.addEventListener('keydown', event => {
            if (event.key === 'Escape') { menu.open = false; trigger.focus(); }
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                menu.open = true;
                const buttons = Array.from(options.querySelectorAll('button:not(:disabled)'));
                const index = buttons.indexOf(document.activeElement);
                buttons[(index + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length]?.focus();
            }
        });
    });
    document.addEventListener('click', event => {
        document.querySelectorAll('.styled-filter[open]').forEach(menu => {
            if (!menu.contains(event.target)) menu.open = false;
        });
    });
});
