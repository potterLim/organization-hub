interface BlazorCircuit {
    reconnect(): Promise<boolean>;
    resumeCircuit(): Promise<boolean>;
}

interface ReconnectStateChangedEvent extends Event {
    detail: {
        state: string;
    };
}

declare const Blazor: BlazorCircuit;

const reconnectModalElementOrNull: HTMLElement | null = document.getElementById("components-reconnect-modal");
const reconnectModalOrNull: HTMLDialogElement | null = reconnectModalElementOrNull instanceof HTMLDialogElement ? reconnectModalElementOrNull : null;
const retryButtonOrNull: HTMLElement | null = document.getElementById("components-reconnect-button");
const resumeButtonOrNull: HTMLElement | null = document.getElementById("components-resume-button");

if (reconnectModalOrNull !== null) {
    reconnectModalOrNull.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged as EventListener);
}

if (retryButtonOrNull !== null) {
    retryButtonOrNull.addEventListener("click", retry);
}

if (resumeButtonOrNull !== null) {
    resumeButtonOrNull.addEventListener("click", resume);
}

function handleReconnectStateChanged(event: ReconnectStateChangedEvent): void {
    if (reconnectModalOrNull === null) {
        return;
    }

    if (event.detail.state === "show") {
        openModal();
    } else if (event.detail.state === "hide") {
        document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        closeModal();
    } else if (event.detail.state === "failed") {
        openModal();
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (event.detail.state === "rejected") {
        location.reload();
    }
}

async function retry(): Promise<void> {
    if (reconnectModalOrNull === null) {
        return;
    }

    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    try {
        const successful: boolean = await Blazor.reconnect();
        if (successful === false) {
            const resumeSuccessful: boolean = await Blazor.resumeCircuit();
            if (resumeSuccessful === false) {
                location.reload();
            } else {
                closeModal();
            }
        }
    } catch {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume(): Promise<void> {
    if (reconnectModalOrNull === null) {
        return;
    }

    try {
        const successful: boolean = await Blazor.resumeCircuit();
        if (successful === false) {
            location.reload();
        } else {
            closeModal();
        }
    } catch {
        reconnectModalOrNull.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

function openModal(): void {
    if (reconnectModalOrNull !== null && reconnectModalOrNull.open === false) {
        reconnectModalOrNull.showModal();
    }
}

function closeModal(): void {
    if (reconnectModalOrNull !== null && reconnectModalOrNull.open) {
        reconnectModalOrNull.close();
    }
}

async function retryWhenDocumentBecomesVisible(): Promise<void> {
    if (document.visibilityState === "visible") {
        await retry();
    }
}

export {};
