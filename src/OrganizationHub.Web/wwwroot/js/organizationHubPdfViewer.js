const MIN_ZOOM_LEVEL = 0.8;
const MAX_ZOOM_LEVEL = 1.45;
const ZOOM_STEP = 0.15;
let pdfjsModulePromise = null;
function getPdfjsModule() {
    if (pdfjsModulePromise === null) {
        const importModule = new Function("source", "return import(source)");
        pdfjsModulePromise = importModule("/vendor/pdfjs/pdf.mjs");
    }
    return pdfjsModulePromise;
}
function clearElement(element) {
    while (element.firstChild !== null) {
        element.removeChild(element.firstChild);
    }
}
function createStatus(text) {
    const statusElement = document.createElement("div");
    statusElement.className = "hub-pdfjs-status";
    statusElement.textContent = text;
    return statusElement;
}
function getPageScale(containerElement, viewport, zoomLevel) {
    const availableWidth = Math.max(containerElement.clientWidth - 40, 280);
    const scale = availableWidth / viewport.width;
    return Math.min(Math.max(scale * zoomLevel, 0.72), 1.8);
}
function clampZoomLevel(zoomLevel) {
    return Math.min(Math.max(zoomLevel, MIN_ZOOM_LEVEL), MAX_ZOOM_LEVEL);
}
function updateToolbar(state, controls, isRendering) {
    controls.pageCountElement.textContent = state.pdfDocument.numPages + "쪽";
    controls.zoomLabelElement.textContent = Math.round(state.zoomLevel * 100) + "%";
    controls.zoomOutButton.disabled = isRendering || state.zoomLevel <= MIN_ZOOM_LEVEL;
    controls.zoomInButton.disabled = isRendering || state.zoomLevel >= MAX_ZOOM_LEVEL;
}
function createToolbar(state) {
    const toolbarElement = document.createElement("div");
    const pageCountElement = document.createElement("span");
    const zoomGroupElement = document.createElement("div");
    const zoomOutButton = document.createElement("button");
    const zoomLabelElement = document.createElement("span");
    const zoomInButton = document.createElement("button");
    const controls = {
        pageCountElement,
        zoomLabelElement,
        zoomOutButton,
        zoomInButton
    };
    toolbarElement.className = "hub-pdfjs-toolbar";
    pageCountElement.className = "hub-pdfjs-page-count";
    zoomGroupElement.className = "hub-pdfjs-zoom-controls";
    zoomOutButton.type = "button";
    zoomOutButton.className = "hub-pdfjs-zoom-button";
    zoomOutButton.textContent = "-";
    zoomOutButton.setAttribute("aria-label", "축소");
    zoomLabelElement.className = "hub-pdfjs-zoom-label";
    zoomInButton.type = "button";
    zoomInButton.className = "hub-pdfjs-zoom-button";
    zoomInButton.textContent = "+";
    zoomInButton.setAttribute("aria-label", "확대");
    zoomOutButton.addEventListener("click", () => {
        state.zoomLevel = clampZoomLevel(state.zoomLevel - ZOOM_STEP);
        void renderAllPages(state, controls);
    });
    zoomInButton.addEventListener("click", () => {
        state.zoomLevel = clampZoomLevel(state.zoomLevel + ZOOM_STEP);
        void renderAllPages(state, controls);
    });
    zoomGroupElement.appendChild(zoomOutButton);
    zoomGroupElement.appendChild(zoomLabelElement);
    zoomGroupElement.appendChild(zoomInButton);
    toolbarElement.appendChild(pageCountElement);
    toolbarElement.appendChild(zoomGroupElement);
    state.pagesElement.parentElement?.insertBefore(toolbarElement, state.pagesElement);
    updateToolbar(state, controls, false);
    return controls;
}
async function renderPage(viewerElement, pagesElement, pdfPage, pageNumber, pageCount, zoomLevel) {
    const baseViewport = pdfPage.getViewport({ scale: 1 });
    const scale = getPageScale(viewerElement, baseViewport, zoomLevel);
    const viewport = pdfPage.getViewport({ scale: scale });
    const pixelRatio = window.devicePixelRatio || 1;
    const pageElement = document.createElement("section");
    const pageCaptionElement = document.createElement("div");
    const canvasElement = document.createElement("canvas");
    const canvasContextOrNull = canvasElement.getContext("2d");
    if (canvasContextOrNull === null) {
        throw new Error("Canvas rendering context is unavailable.");
    }
    pageElement.className = "hub-pdfjs-page";
    pageCaptionElement.className = "hub-pdfjs-page-caption";
    pageCaptionElement.textContent = pageNumber + " / " + pageCount;
    canvasElement.width = Math.floor(viewport.width * pixelRatio);
    canvasElement.height = Math.floor(viewport.height * pixelRatio);
    canvasElement.style.width = Math.floor(viewport.width) + "px";
    canvasElement.style.height = Math.floor(viewport.height) + "px";
    pageElement.appendChild(pageCaptionElement);
    pageElement.appendChild(canvasElement);
    pagesElement.appendChild(pageElement);
    await pdfPage.render({ canvasContext: canvasContextOrNull, viewport: viewport, transform: pixelRatio !== 1 ? [pixelRatio, 0, 0, pixelRatio, 0, 0] : null }).promise;
}
async function renderAllPages(state, controls) {
    state.renderToken += 1;
    const currentRenderToken = state.renderToken;
    updateToolbar(state, controls, true);
    clearElement(state.pagesElement);
    state.pagesElement.appendChild(createStatus("PDF를 준비하는 중입니다."));
    try {
        clearElement(state.pagesElement);
        for (let pageNumber = 1; pageNumber <= state.pdfDocument.numPages; pageNumber += 1) {
            if (state.renderToken !== currentRenderToken) {
                return;
            }
            const pdfPage = await state.pdfDocument.getPage(pageNumber);
            await renderPage(state.pagesElement.parentElement ?? state.pagesElement, state.pagesElement, pdfPage, pageNumber, state.pdfDocument.numPages, state.zoomLevel);
        }
    }
    finally {
        if (state.renderToken === currentRenderToken) {
            updateToolbar(state, controls, false);
        }
    }
}
export async function renderPdf(containerElement, sourceUrl) {
    clearElement(containerElement);
    containerElement.appendChild(createStatus("PDF를 불러오는 중입니다."));
    const pdfjsModule = await getPdfjsModule();
    pdfjsModule.GlobalWorkerOptions.workerSrc = "/vendor/pdfjs/pdf.worker.mjs";
    const response = await fetch(sourceUrl, { credentials: "same-origin" });
    if (response.ok === false) {
        clearElement(containerElement);
        containerElement.appendChild(createStatus("PDF를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요."));
        return;
    }
    const fileBuffer = await response.arrayBuffer();
    const loadingTask = pdfjsModule.getDocument({ data: fileBuffer });
    const pdfDocument = await loadingTask.promise;
    const pagesElement = document.createElement("div");
    const state = {
        pdfDocument,
        pagesElement,
        zoomLevel: 1,
        renderToken: 0
    };
    pagesElement.className = "hub-pdfjs-pages";
    clearElement(containerElement);
    containerElement.appendChild(pagesElement);
    const controls = createToolbar(state);
    await renderAllPages(state, controls);
}
//# sourceMappingURL=organizationHubPdfViewer.js.map