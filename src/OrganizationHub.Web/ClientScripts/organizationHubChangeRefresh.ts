interface DotNetChangeRefreshReference {
    invokeMethodAsync(methodName: string): Promise<void>;
}

interface ChangeRefreshState {
    abortController: AbortController;
    isChecking: boolean;
    lastCheckedAt: number;
    intervalId: number;
}

const MIN_CHECK_INTERVAL_MS: number = 1000;
const BACKGROUND_CHECK_INTERVAL_MS: number = 3000;
const changeRefreshStates: WeakMap<HTMLElement, ChangeRefreshState> = new WeakMap<HTMLElement, ChangeRefreshState>();

export function initializeChangeRefresh(containerElement: HTMLElement, dotNetReference: DotNetChangeRefreshReference): void {
    disposeChangeRefresh(containerElement);

    const abortController: AbortController = new AbortController();
    const state: ChangeRefreshState = { abortController: abortController, isChecking: false, lastCheckedAt: 0, intervalId: 0 };
    const signal: AbortSignal = abortController.signal;

    const checkForChanges = async (): Promise<void> => {
        if (document.hidden || state.isChecking) {
            return;
        }

        const now: number = Date.now();
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

    state.intervalId = window.setInterval(() => {
        void checkForChanges();
    }, BACKGROUND_CHECK_INTERVAL_MS);

    changeRefreshStates.set(containerElement, state);
}

export function disposeChangeRefresh(containerElement: HTMLElement): void {
    const state: ChangeRefreshState | undefined = changeRefreshStates.get(containerElement);
    if (state === undefined) {
        return;
    }

    state.abortController.abort();
    window.clearInterval(state.intervalId);
    changeRefreshStates.delete(containerElement);
}
