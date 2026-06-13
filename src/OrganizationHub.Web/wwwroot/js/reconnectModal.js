const reconnectModalElementOrNull = document.getElementById("components-reconnect-modal");
const reconnectModalOrNull = reconnectModalElementOrNull instanceof HTMLDialogElement ? reconnectModalElementOrNull : null;
const retryButtonOrNull = document.getElementById("components-reconnect-button");
const resumeButtonOrNull = document.getElementById("components-resume-button");
if (reconnectModalOrNull !== null) {
    reconnectModalOrNull.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);
}
if (retryButtonOrNull !== null) {
    retryButtonOrNull.addEventListener("click", retry);
}
if (resumeButtonOrNull !== null) {
    resumeButtonOrNull.addEventListener("click", resume);
}
function handleReconnectStateChanged(event) {
    if (reconnectModalOrNull === null) {
        return;
    }
    if (event.detail.state === "show") {
        openModal();
    }
    else if (event.detail.state === "hide") {
        document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        closeModal();
    }
    else if (event.detail.state === "failed") {
        openModal();
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
    else if (event.detail.state === "rejected") {
        location.reload();
    }
}
async function retry() {
    if (reconnectModalOrNull === null) {
        return;
    }
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    try {
        const successful = await Blazor.reconnect();
        if (successful === false) {
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (resumeSuccessful === false) {
                location.reload();
            }
            else {
                closeModal();
            }
        }
    }
    catch {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}
async function resume() {
    if (reconnectModalOrNull === null) {
        return;
    }
    try {
        const successful = await Blazor.resumeCircuit();
        if (successful === false) {
            location.reload();
        }
        else {
            closeModal();
        }
    }
    catch {
        reconnectModalOrNull.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}
function openModal() {
    if (reconnectModalOrNull !== null && reconnectModalOrNull.open === false) {
        reconnectModalOrNull.showModal();
    }
}
function closeModal() {
    if (reconnectModalOrNull !== null && reconnectModalOrNull.open) {
        reconnectModalOrNull.close();
    }
}
async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
export {};
//# sourceMappingURL=reconnectModal.js.map