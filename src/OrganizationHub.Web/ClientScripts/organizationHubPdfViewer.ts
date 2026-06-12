interface PdfjsModule {
    GlobalWorkerOptions: {
        workerSrc: string;
    };
    getDocument(options: PdfDocumentOptions): PdfLoadingTask;
}

interface PdfDocumentOptions {
    data: ArrayBuffer;
}

interface PdfLoadingTask {
    promise: Promise<PdfDocument>;
}

interface PdfDocument {
    numPages: number;
    getPage(pageNumber: number): Promise<PdfPage>;
}

interface PdfPage {
    getViewport(options: PdfViewportOptions): PdfViewport;
    render(options: PdfRenderOptions): PdfRenderTask;
}

interface PdfViewportOptions {
    scale: number;
}

interface PdfViewport {
    width: number;
    height: number;
}

interface PdfRenderOptions {
    canvasContext: CanvasRenderingContext2D;
    viewport: PdfViewport;
    transform: number[] | null;
}

interface PdfRenderTask {
    promise: Promise<void>;
}

interface PdfViewerControls {
    pageCountElement: HTMLElement;
    zoomLabelElement: HTMLElement;
    zoomOutButton: HTMLButtonElement;
    zoomInButton: HTMLButtonElement;
}

interface PdfViewerState {
    pdfDocument: PdfDocument;
    pagesElement: HTMLElement;
    zoomLevel: number;
    renderToken: number;
}

const MIN_ZOOM_LEVEL: number = 0.8;
const MAX_ZOOM_LEVEL: number = 1.45;
const ZOOM_STEP: number = 0.15;
let pdfjsModulePromise: Promise<PdfjsModule> | null = null;

function getPdfjsModule(): Promise<PdfjsModule> {
    if (pdfjsModulePromise === null) {
        const importModule: (source: string) => Promise<PdfjsModule> = new Function("source", "return import(source)") as (source: string) => Promise<PdfjsModule>;
        pdfjsModulePromise = importModule("/vendor/pdfjs/pdf.mjs");
    }

    return pdfjsModulePromise;
}

function clearElement(element: HTMLElement): void {
    while (element.firstChild !== null) {
        element.removeChild(element.firstChild);
    }
}

function createStatus(text: string): HTMLElement {
    const statusElement: HTMLDivElement = document.createElement("div");
    statusElement.className = "hub-pdfjs-status";
    statusElement.textContent = text;
    return statusElement;
}

function getPageScale(containerElement: HTMLElement, viewport: PdfViewport, zoomLevel: number): number {
    const availableWidth: number = Math.max(containerElement.clientWidth - 40, 280);
    const scale: number = availableWidth / viewport.width;
    return Math.min(Math.max(scale * zoomLevel, 0.72), 1.8);
}

function clampZoomLevel(zoomLevel: number): number {
    return Math.min(Math.max(zoomLevel, MIN_ZOOM_LEVEL), MAX_ZOOM_LEVEL);
}

function updateToolbar(state: PdfViewerState, controls: PdfViewerControls, isRendering: boolean): void {
    controls.pageCountElement.textContent = state.pdfDocument.numPages + "쪽";
    controls.zoomLabelElement.textContent = Math.round(state.zoomLevel * 100) + "%";
    controls.zoomOutButton.disabled = isRendering || state.zoomLevel <= MIN_ZOOM_LEVEL;
    controls.zoomInButton.disabled = isRendering || state.zoomLevel >= MAX_ZOOM_LEVEL;
}

function createToolbar(state: PdfViewerState): PdfViewerControls {
    const toolbarElement: HTMLElement = document.createElement("div");
    const pageCountElement: HTMLSpanElement = document.createElement("span");
    const zoomGroupElement: HTMLElement = document.createElement("div");
    const zoomOutButton: HTMLButtonElement = document.createElement("button");
    const zoomLabelElement: HTMLSpanElement = document.createElement("span");
    const zoomInButton: HTMLButtonElement = document.createElement("button");
    const controls: PdfViewerControls = {
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

async function renderPage(viewerElement: HTMLElement, pagesElement: HTMLElement, pdfPage: PdfPage, pageNumber: number, pageCount: number, zoomLevel: number): Promise<void> {
    const baseViewport: PdfViewport = pdfPage.getViewport({ scale: 1 });
    const scale: number = getPageScale(viewerElement, baseViewport, zoomLevel);
    const viewport: PdfViewport = pdfPage.getViewport({ scale: scale });
    const pixelRatio: number = window.devicePixelRatio || 1;
    const pageElement: HTMLElement = document.createElement("section");
    const pageCaptionElement: HTMLDivElement = document.createElement("div");
    const canvasElement: HTMLCanvasElement = document.createElement("canvas");
    const canvasContextOrNull: CanvasRenderingContext2D | null = canvasElement.getContext("2d");
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

async function renderAllPages(state: PdfViewerState, controls: PdfViewerControls): Promise<void> {
    state.renderToken += 1;
    const currentRenderToken: number = state.renderToken;
    updateToolbar(state, controls, true);
    clearElement(state.pagesElement);
    state.pagesElement.appendChild(createStatus("PDF를 준비하는 중입니다."));
    try {
        clearElement(state.pagesElement);
        for (let pageNumber: number = 1; pageNumber <= state.pdfDocument.numPages; pageNumber += 1) {
            if (state.renderToken !== currentRenderToken) {
                return;
            }

            const pdfPage: PdfPage = await state.pdfDocument.getPage(pageNumber);
            await renderPage(state.pagesElement.parentElement ?? state.pagesElement, state.pagesElement, pdfPage, pageNumber, state.pdfDocument.numPages, state.zoomLevel);
        }
    }
    finally {
        if (state.renderToken === currentRenderToken) {
            updateToolbar(state, controls, false);
        }
    }
}

export async function renderPdf(containerElement: HTMLElement, sourceUrl: string): Promise<void> {
    clearElement(containerElement);
    containerElement.appendChild(createStatus("PDF를 불러오는 중입니다."));
    const pdfjsModule: PdfjsModule = await getPdfjsModule();
    pdfjsModule.GlobalWorkerOptions.workerSrc = "/vendor/pdfjs/pdf.worker.mjs";
    const response: Response = await fetch(sourceUrl, { credentials: "same-origin" });
    if (response.ok === false) {
        clearElement(containerElement);
        containerElement.appendChild(createStatus("PDF를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요."));
        return;
    }

    const fileBuffer: ArrayBuffer = await response.arrayBuffer();
    const loadingTask: PdfLoadingTask = pdfjsModule.getDocument({ data: fileBuffer });
    const pdfDocument: PdfDocument = await loadingTask.promise;
    const pagesElement: HTMLElement = document.createElement("div");
    const state: PdfViewerState = {
        pdfDocument,
        pagesElement,
        zoomLevel: 1,
        renderToken: 0
    };

    pagesElement.className = "hub-pdfjs-pages";
    clearElement(containerElement);
    containerElement.appendChild(pagesElement);
    const controls: PdfViewerControls = createToolbar(state);
    await renderAllPages(state, controls);
}
