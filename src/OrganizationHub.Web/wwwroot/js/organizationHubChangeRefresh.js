const MIN_CHECK_INTERVAL_MS = 1000;
const BACKGROUND_CHECK_INTERVAL_MS = 3000;
const changeRefreshStates = new WeakMap();
export function initializeChangeRefresh(containerElement, dotNetReference) {
    disposeChangeRefresh(containerElement);
    const abortController = new AbortController();
    const state = { abortController: abortController, isChecking: false, lastCheckedAt: 0, intervalID: 0 };
    const signal = abortController.signal;
    const checkForChanges = async () => {
        if (document.hidden || state.isChecking) {
            return;
        }
        const now = Date.now();
        if (now - state.lastCheckedAt < MIN_CHECK_INTERVAL_MS) {
            return;
        }
        state.isChecking = true;
        state.lastCheckedAt = now;
        try {
            await dotNetReference.invokeMethodAsync("CheckForExternalChanges");
        }
        catch {
        }
        finally {
            state.isChecking = false;
        }
    };
    window.addEventListener("focus", () => {
        void checkForChanges();
    }, { signal: signal });
    document.addEventListener("visibilitychange", () => {
        void checkForChanges();
    }, { signal: signal });
    window.addEventListener("pageshow", () => {
        void checkForChanges();
    }, { signal: signal });
    state.intervalID = window.setInterval(() => {
        void checkForChanges();
    }, BACKGROUND_CHECK_INTERVAL_MS);
    changeRefreshStates.set(containerElement, state);
}
export function disposeChangeRefresh(containerElement) {
    const state = changeRefreshStates.get(containerElement);
    if (state === undefined) {
        return;
    }
    state.abortController.abort();
    window.clearInterval(state.intervalID);
    changeRefreshStates.delete(containerElement);
}
//# sourceMappingURL=organizationHubChangeRefresh.js.map