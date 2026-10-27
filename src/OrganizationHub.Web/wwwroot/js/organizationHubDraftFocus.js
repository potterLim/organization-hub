export function focusEditor(selector) {
    if (selector.length === 0)
        return;
    const editor = document.querySelector(selector);
    editor?.focus();
}
//# sourceMappingURL=organizationHubDraftFocus.js.map