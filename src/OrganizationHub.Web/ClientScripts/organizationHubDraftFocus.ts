export function focusEditor(selector: string): void {
    if (selector.length === 0) return;
    const editor = document.querySelector<HTMLElement>(selector);
    editor?.focus();
}
