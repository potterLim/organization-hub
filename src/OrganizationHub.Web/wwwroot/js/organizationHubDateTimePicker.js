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

function positionPanel(root) {
    const panel = root.querySelector('.hub-calendar-date-time-panel');
    if (!panel) return;
    panel.style.translate = '';
    panel.style.maxHeight = '';
    if (matchMedia('(max-width: 720px)').matches) return;
    const margin = 16;
    const header = document.querySelector('.hub-appbar');
    const minimumTop = Math.max(margin, (header?.getBoundingClientRect().bottom || 0) + 8);
    panel.style.maxHeight = `${Math.max(100, innerHeight - minimumTop - margin)}px`;
    const bounds = panel.getBoundingClientRect();
    const trigger = root.querySelector('.hub-calendar-date-time-field-button').getBoundingClientRect();
    const preferredTop = trigger.bottom + 8;
    const aboveTop = trigger.top - bounds.height - 8;
    const top = preferredTop + bounds.height <= innerHeight - margin ? preferredTop
        : aboveTop >= minimumTop ? aboveTop : Math.max(minimumTop, innerHeight - margin - bounds.height);
    const left = Math.max(margin, Math.min(bounds.left, document.documentElement.clientWidth - margin - bounds.width));
    panel.style.translate = `${left - bounds.left}px ${top - bounds.top}px`;
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
    let frame;
    const reposition = () => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(() => positionPanel(root));
    };
    const observer = new ResizeObserver(reposition);
    observer.observe(root.querySelector('.hub-calendar-date-time-panel'));
    window.addEventListener('resize', reposition);
    window.addEventListener('scroll', reposition, true);
    sessions.set(root, { handler, reposition, observer, cancel: () => cancelAnimationFrame(frame) });
    positionPanel(root);
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
    const session = sessions.get(root);
    if (session) {
        root.removeEventListener('keydown', session.handler);
        window.removeEventListener('resize', session.reposition);
        window.removeEventListener('scroll', session.reposition, true);
        session.observer.disconnect();
        session.cancel();
    }
    sessions.delete(root);
}
