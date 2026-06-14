interface DotNetTodoBoardReference {
    invokeMethodAsync(methodName: string, sourceTaskId: string, targetTaskId: string): Promise<void>;
}

interface TodoBoardState {
    abortController: AbortController;
    draggedRowHeight: number;
    draggedTaskId: string;
    dropAfter: boolean;
    dropTargetTaskId: string;
    isPointerDragging: boolean;
    placeholderElement: HTMLElement | null;
    pointerId: number;
    previewElement: HTMLElement | null;
    previewOffsetX: number;
    previewOffsetY: number;
    sourceColumnElement: HTMLElement | null;
    sourceListElement: HTMLElement | null;
    startX: number;
    startY: number;
}

interface TodoDropPlacement {
    dropAfter: boolean;
    targetRow: HTMLElement;
}

const todoBoardStates: WeakMap<HTMLElement, TodoBoardState> = new WeakMap<HTMLElement, TodoBoardState>();
const dragStartDistanceInPixels: number = 6;

function isValidTodoContainer(containerElement: HTMLElement | null | undefined): containerElement is HTMLElement {
    return containerElement instanceof HTMLElement;
}

function getTaskId(element: Element | null): string {
    if (element === null) {
        return "";
    }

    const taskElement: HTMLElement | null = element.closest<HTMLElement>("[data-task-id]");
    if (taskElement === null) {
        return "";
    }

    const taskId: string | undefined = taskElement.dataset.taskId;
    return taskId ?? "";
}

function getTaskRow(element: Element | null): HTMLElement | null {
    if (element === null) {
        return null;
    }

    return element.closest<HTMLElement>(".hub-todo-row");
}

function clearDropTargets(containerElement: HTMLElement): void {
    const dropTargets: NodeListOf<HTMLElement> = containerElement.querySelectorAll<HTMLElement>(".hub-todo-row-drop-target");
    dropTargets.forEach((dropTarget: HTMLElement) => {
        dropTarget.classList.remove("hub-todo-row-drop-target");
        dropTarget.classList.remove("hub-todo-row-drop-after");
    });
}

function removeDropPlaceholder(state: TodoBoardState): void {
    if (state.placeholderElement === null) {
        return;
    }

    state.placeholderElement.remove();
    state.placeholderElement = null;
}

function shouldPlaceAfterByPosition(event: MouseEvent, targetRow: HTMLElement): boolean | null {
    const targetRect: DOMRect = targetRow.getBoundingClientRect();
    if (event.clientY < targetRect.top || event.clientY > targetRect.bottom) {
        return null;
    }

    return event.clientY >= targetRect.top + targetRect.height / 2;
}

function shouldPlaceAfterByRowOrder(containerElement: HTMLElement, sourceTaskId: string, targetRow: HTMLElement): boolean {
    const rows: HTMLElement[] = Array.from(containerElement.querySelectorAll<HTMLElement>(".hub-todo-row"));
    const sourceIndex: number = rows.findIndex((row: HTMLElement) => getTaskId(row) === sourceTaskId);
    const targetIndex: number = rows.indexOf(targetRow);
    return sourceIndex >= 0 && targetIndex >= 0 && sourceIndex < targetIndex;
}

function shouldPlaceAfter(event: MouseEvent, containerElement: HTMLElement, sourceTaskId: string, targetRow: HTMLElement): boolean {
    const positionResult: boolean | null = shouldPlaceAfterByPosition(event, targetRow);
    if (positionResult !== null) {
        return positionResult;
    }

    return shouldPlaceAfterByRowOrder(containerElement, sourceTaskId, targetRow);
}

function clearDraggingRows(containerElement: HTMLElement): void {
    const draggingRows: NodeListOf<HTMLElement> = containerElement.querySelectorAll<HTMLElement>(".hub-todo-row-dragging");
    draggingRows.forEach((draggingRow: HTMLElement) => draggingRow.classList.remove("hub-todo-row-dragging"));
}

function getSourceColumn(element: Element | null): HTMLElement | null {
    if (element === null) {
        return null;
    }

    return element.closest<HTMLElement>(".hub-todo-column");
}

function getSourceList(element: Element | null): HTMLElement | null {
    if (element === null) {
        return null;
    }

    return element.closest<HTMLElement>(".hub-todo-list");
}

function isPointerInsideSourceColumn(state: TodoBoardState, event: DragEvent | PointerEvent): boolean {
    if (state.sourceColumnElement === null) {
        return false;
    }

    const sourceRect: DOMRect = state.sourceColumnElement.getBoundingClientRect();
    return event.clientX >= sourceRect.left && event.clientX <= sourceRect.right && event.clientY >= sourceRect.top && event.clientY <= sourceRect.bottom;
}

function getDropPlacement(state: TodoBoardState, sourceTaskId: string, clientY: number): TodoDropPlacement | null {
    if (state.sourceListElement === null) {
        return null;
    }

    const sourceListElement: HTMLElement = state.sourceListElement;
    const rows: HTMLElement[] = Array.from(sourceListElement.querySelectorAll<HTMLElement>(".hub-todo-row")).filter((row: HTMLElement) => getTaskId(row) !== sourceTaskId);
    if (rows.length === 0) {
        return null;
    }

    for (const row of rows) {
        const rowRect: DOMRect = row.getBoundingClientRect();
        if (clientY < rowRect.top + rowRect.height / 2) {
            return { dropAfter: false, targetRow: row };
        }
    }

    return { dropAfter: true, targetRow: rows[rows.length - 1] };
}

function getSourceRow(containerElement: HTMLElement, sourceTaskId: string): HTMLElement | null {
    const rows: HTMLElement[] = Array.from(containerElement.querySelectorAll<HTMLElement>(".hub-todo-row"));
    return rows.find((row: HTMLElement) => getTaskId(row) === sourceTaskId) ?? null;
}

function removeDragPreview(state: TodoBoardState): void {
    if (state.previewElement === null) {
        return;
    }

    state.previewElement.remove();
    state.previewElement = null;
}

function moveDragPreview(state: TodoBoardState, event: MouseEvent): void {
    if (state.previewElement === null) {
        return;
    }

    const previewX: number = event.clientX - state.previewOffsetX;
    const previewY: number = event.clientY - state.previewOffsetY;
    state.previewElement.style.transform = `translate3d(${previewX}px, ${previewY}px, 0)`;
}

function createDragPreview(state: TodoBoardState, sourceRow: HTMLElement, event: PointerEvent): void {
    removeDragPreview(state);
    const sourceRect: DOMRect = sourceRow.getBoundingClientRect();
    state.draggedRowHeight = sourceRect.height;
    const previewElement: HTMLElement = sourceRow.cloneNode(true) as HTMLElement;
    previewElement.classList.remove("hub-todo-row-dragging");
    previewElement.classList.add("hub-todo-drag-preview");
    previewElement.style.width = `${sourceRect.width}px`;
    state.previewOffsetX = Math.min(Math.max(event.clientX - sourceRect.left, 18), sourceRect.width - 18);
    state.previewOffsetY = Math.min(Math.max(event.clientY - sourceRect.top, 18), sourceRect.height - 18);
    state.previewElement = previewElement;
    document.body.appendChild(previewElement);
    moveDragPreview(state, event);
}

function createDropPlaceholder(state: TodoBoardState, sourceRow: HTMLElement | null): HTMLElement {
    removeDropPlaceholder(state);
    const placeholderElement: HTMLElement = document.createElement("div");
    placeholderElement.className = "hub-todo-drop-placeholder";
    if (sourceRow !== null) {
        const sourceHeight: number = state.draggedRowHeight > 0 ? state.draggedRowHeight : sourceRow.getBoundingClientRect().height;
        placeholderElement.style.minHeight = `${sourceHeight}px`;
    }

    state.placeholderElement = placeholderElement;
    return placeholderElement;
}

function getPointerDistance(state: TodoBoardState, event: PointerEvent): number {
    const deltaX: number = event.clientX - state.startX;
    const deltaY: number = event.clientY - state.startY;
    return Math.sqrt(deltaX * deltaX + deltaY * deltaY);
}

function beginPointerDrag(containerElement: HTMLElement, state: TodoBoardState, event: PointerEvent): boolean {
    const sourceRow: HTMLElement | null = getSourceRow(containerElement, state.draggedTaskId);
    if (sourceRow === null) {
        return false;
    }

    state.isPointerDragging = true;
    state.sourceColumnElement = getSourceColumn(sourceRow);
    state.sourceListElement = getSourceList(sourceRow);
    createDragPreview(state, sourceRow, event);
    sourceRow.classList.add("hub-todo-row-dragging");
    return true;
}

function getPointerTargetRow(event: PointerEvent): HTMLElement | null {
    const element: Element | null = document.elementFromPoint(event.clientX, event.clientY);
    return getTaskRow(element);
}

function applyDropTarget(containerElement: HTMLElement, state: TodoBoardState, event: DragEvent | PointerEvent): void {
    removeDropPlaceholder(state);
    if (state.sourceListElement === null || isPointerInsideSourceColumn(state, event) == false) {
        clearDropTargets(containerElement);
        state.dropTargetTaskId = "";
        return;
    }

    const placementOrNull: TodoDropPlacement | null = getDropPlacement(state, state.draggedTaskId, event.clientY);
    if (placementOrNull === null) {
        return;
    }

    const targetRow: HTMLElement = placementOrNull.targetRow;
    const targetTaskId: string = getTaskId(targetRow);
    if (targetTaskId.length === 0 || targetTaskId === state.draggedTaskId) {
        return;
    }

    clearDropTargets(containerElement);
    state.dropTargetTaskId = targetTaskId;
    state.dropAfter = placementOrNull.dropAfter;
    targetRow.classList.add("hub-todo-row-drop-target");
    const placeholderElement: HTMLElement = createDropPlaceholder(state, getSourceRow(containerElement, state.draggedTaskId));
    if (state.dropAfter) {
        targetRow.classList.add("hub-todo-row-drop-after");
        targetRow.insertAdjacentElement("afterend", placeholderElement);
    }
    else {
        targetRow.insertAdjacentElement("beforebegin", placeholderElement);
    }
}

function resetPointerDragState(containerElement: HTMLElement, state: TodoBoardState): void {
    state.draggedTaskId = "";
    state.draggedRowHeight = 0;
    state.dropAfter = false;
    state.dropTargetTaskId = "";
    state.isPointerDragging = false;
    state.pointerId = -1;
    state.sourceColumnElement = null;
    state.sourceListElement = null;
    state.startX = 0;
    state.startY = 0;
    removeDragPreview(state);
    removeDropPlaceholder(state);
    clearDropTargets(containerElement);
    clearDraggingRows(containerElement);
}

export function initializeTodoSortable(containerElement: HTMLElement | null | undefined, dotNetReference: DotNetTodoBoardReference): void {
    if (isValidTodoContainer(containerElement) == false) {
        return;
    }

    disposeTodoSortable(containerElement);

    const abortController: AbortController = new AbortController();
    const state: TodoBoardState = { abortController: abortController, draggedRowHeight: 0, draggedTaskId: "", dropAfter: false, dropTargetTaskId: "", isPointerDragging: false, placeholderElement: null, pointerId: -1, previewElement: null, previewOffsetX: 0, previewOffsetY: 0, sourceColumnElement: null, sourceListElement: null, startX: 0, startY: 0 };
    const signal: AbortSignal = abortController.signal;

    containerElement.addEventListener("pointerdown", (event: PointerEvent) => {
        if (event.button !== 0) {
            return;
        }

        const handleElement: HTMLElement | null = (event.target as Element | null)?.closest<HTMLElement>(".hub-todo-drag-handle") ?? null;
        if (handleElement === null) {
            return;
        }

        const taskId: string = getTaskId(handleElement);
        if (taskId.length === 0) {
            return;
        }

        state.draggedTaskId = taskId;
        state.pointerId = event.pointerId;
        state.startX = event.clientX;
        state.startY = event.clientY;
        handleElement.setPointerCapture(event.pointerId);

        event.preventDefault();
    }, { signal: signal });

    containerElement.addEventListener("pointermove", (event: PointerEvent) => {
        if (state.pointerId !== event.pointerId || state.draggedTaskId.length === 0) {
            return;
        }

        if (state.isPointerDragging == false) {
            if (getPointerDistance(state, event) < dragStartDistanceInPixels) {
                return;
            }

            if (beginPointerDrag(containerElement, state, event) == false) {
                resetPointerDragState(containerElement, state);
                return;
            }
        }

        moveDragPreview(state, event);
        applyDropTarget(containerElement, state, event);
        event.preventDefault();
    }, { signal: signal });

    containerElement.addEventListener("pointerup", async (event: PointerEvent) => {
        if (state.pointerId !== event.pointerId || state.draggedTaskId.length === 0) {
            return;
        }

        if (state.isPointerDragging == false) {
            resetPointerDragState(containerElement, state);
            return;
        }

        const sourceTaskId: string = state.draggedTaskId;
        const targetRow: HTMLElement | null = getPointerTargetRow(event);
        let targetTaskId: string = state.dropTargetTaskId;
        let shouldMoveAfter: boolean = state.dropAfter;
        if (targetTaskId.length === 0 && state.sourceListElement !== null && isPointerInsideSourceColumn(state, event)) {
            const placementOrNull: TodoDropPlacement | null = getDropPlacement(state, sourceTaskId, event.clientY);
            if (placementOrNull !== null) {
                targetTaskId = getTaskId(placementOrNull.targetRow);
                shouldMoveAfter = placementOrNull.dropAfter;
            }
            else if (targetRow !== null) {
                targetTaskId = getTaskId(targetRow);
                shouldMoveAfter = shouldPlaceAfter(event, containerElement, sourceTaskId, targetRow);
            }
        }

        if (targetTaskId.length === 0) {
            resetPointerDragState(containerElement, state);
            return;
        }

        resetPointerDragState(containerElement, state);
        if (targetTaskId.length === 0 || sourceTaskId === targetTaskId) {
            return;
        }

        const methodName: string = shouldMoveAfter ? "MoveTaskAfterFromScript" : "MoveTaskBeforeFromScript";
        await dotNetReference.invokeMethodAsync(methodName, sourceTaskId, targetTaskId);
        event.preventDefault();
    }, { signal: signal });

    containerElement.addEventListener("pointercancel", (event: PointerEvent) => {
        if (state.pointerId !== event.pointerId) {
            return;
        }

        resetPointerDragState(containerElement, state);
    }, { signal: signal });

    containerElement.addEventListener("dragstart", (event: DragEvent) => {
        const handleElement: HTMLElement | null = (event.target as Element | null)?.closest<HTMLElement>(".hub-todo-drag-handle") ?? null;
        if (handleElement === null || event.dataTransfer === null) {
            event.preventDefault();
            return;
        }

        const taskId: string = getTaskId(handleElement);
        if (taskId.length === 0) {
            event.preventDefault();
            return;
        }

        state.draggedTaskId = taskId;
        const taskRow: HTMLElement | null = getTaskRow(handleElement);
        if (taskRow !== null) {
            state.sourceColumnElement = getSourceColumn(taskRow);
            state.sourceListElement = getSourceList(taskRow);
            state.draggedRowHeight = taskRow.getBoundingClientRect().height;
            taskRow.classList.add("hub-todo-row-dragging");
        }

        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", taskId);
    }, { signal: signal });

    containerElement.addEventListener("dragover", (event: DragEvent) => {
        const targetRow: HTMLElement | null = getTaskRow(event.target as Element | null);
        if (targetRow === null) {
            return;
        }

        const targetTaskId: string = getTaskId(targetRow);
        if (targetTaskId.length === 0 || targetTaskId === state.draggedTaskId) {
            return;
        }

        event.preventDefault();
        applyDropTarget(containerElement, state, event);
    }, { signal: signal });

    containerElement.addEventListener("drop", async (event: DragEvent) => {
        const targetRow: HTMLElement | null = getTaskRow(event.target as Element | null);
        if (targetRow === null || event.dataTransfer === null) {
            return;
        }

        const sourceTaskId: string = event.dataTransfer.getData("text/plain") || state.draggedTaskId;
        const placementOrNull: TodoDropPlacement | null = state.sourceListElement !== null && isPointerInsideSourceColumn(state, event) ? getDropPlacement(state, sourceTaskId, event.clientY) : null;
        if (state.dropTargetTaskId.length === 0 && placementOrNull === null) {
            resetPointerDragState(containerElement, state);
            return;
        }

        const targetTaskId: string = state.dropTargetTaskId.length > 0 ? state.dropTargetTaskId : placementOrNull !== null ? getTaskId(placementOrNull.targetRow) : getTaskId(targetRow);
        const shouldMoveAfter: boolean = state.dropTargetTaskId.length > 0 ? state.dropAfter : placementOrNull !== null ? placementOrNull.dropAfter : shouldPlaceAfter(event, containerElement, sourceTaskId, targetRow);
        resetPointerDragState(containerElement, state);

        if (sourceTaskId.length === 0 || targetTaskId.length === 0 || sourceTaskId === targetTaskId) {
            return;
        }

        event.preventDefault();
        const methodName: string = shouldMoveAfter ? "MoveTaskAfterFromScript" : "MoveTaskBeforeFromScript";
        await dotNetReference.invokeMethodAsync(methodName, sourceTaskId, targetTaskId);
    }, { signal: signal });

    containerElement.addEventListener("dragend", () => {
        resetPointerDragState(containerElement, state);
    }, { signal: signal });

    todoBoardStates.set(containerElement, state);
}

export function disposeTodoSortable(containerElement: HTMLElement | null | undefined): void {
    if (isValidTodoContainer(containerElement) == false) {
        return;
    }

    const state: TodoBoardState | undefined = todoBoardStates.get(containerElement);
    if (state === undefined) {
        return;
    }

    state.abortController.abort();
    todoBoardStates.delete(containerElement);
    removeDragPreview(state);
    removeDropPlaceholder(state);
    clearDropTargets(containerElement);
    clearDraggingRows(containerElement);
}
