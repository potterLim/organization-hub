const sessions = new WeakMap();

function focusDay(root, date) {
    const days = [...root.querySelectorAll('[data-picker-date]:not(:disabled)')];
    const selected = days.find(button => button.dataset.pickerDate === date)
        ?? days.find(button => button.getAttribute('aria-pressed') === 'true')
        ?? days.find(button => button.getAttribute('aria-current') === 'date') ?? days[0];
    for (const button of days) button.tabIndex = button === selected ? 0 : -1;
    selected?.focus({ preventScroll: true });
}

function prepareTimeGroups(root) {
    for (const group of root.querySelectorAll('[data-picker-time-group]')) {
        const buttons = [...group.querySelectorAll('button:not(:disabled)')];
        const selected = buttons.find(button => button.getAttribute('aria-pressed') === 'true') ?? buttons[0];
        for (const button of buttons) button.tabIndex = button === selected ? 0 : -1;
    }
}

export function openPicker(root, reference) {
    disposePicker(root);
    const handler = event => {
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            void reference.invokeMethodAsync('ClosePickerFromKeyboard');
            return;
        }
        const target = event.target.closest('button');
        if (!target) return;
        if (target.dataset.pickerDate && ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'].includes(event.key)) {
            event.preventDefault();
            void reference.invokeMethodAsync('MovePickerDateFromKeyboard', target.dataset.pickerDate, event.key);
            return;
        }
        const group = target.closest('[data-picker-time-group]');
        if (!group || !['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault();
        const buttons = [...group.querySelectorAll('button:not(:disabled)')];
        const index = buttons.indexOf(target);
        let next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1
            : (index + (['ArrowLeft', 'ArrowUp'].includes(event.key) ? -1 : 1) + buttons.length) % buttons.length;
        for (const button of buttons) button.tabIndex = button === buttons[next] ? 0 : -1;
        buttons[next]?.focus();
    };
    root.addEventListener('keydown', handler);
    sessions.set(root, handler);
    prepareTimeGroups(root);
    focusDay(root);
}

export function focusPickerDate(root, date) {
    focusDay(root, date);
    prepareTimeGroups(root);
}

export function closePicker(root) {
    disposePicker(root);
    root.querySelector('.hub-calendar-date-time-field-button')?.focus({ preventScroll: true });
}

export function disposePicker(root) {
    const handler = sessions.get(root);
    if (handler) root.removeEventListener('keydown', handler);
    sessions.delete(root);
}
