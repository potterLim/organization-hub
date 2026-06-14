const todoBoardStates = new WeakMap();
const dragStartDistanceInPixels = 6;
function isValidTodoContainer(containerElement) {
    return containerElement instanceof HTMLElement;
}
function getTaskId(element) {
    if (element === null) {
        return "";
    }
    const taskElement = element.closest("[data-task-id]");
    if (taskElement === null) {
        return "";
    }
    const taskId = taskElement.dataset.taskId;
    return taskId ?? "";
}
function getTaskRow(element) {
    if (element === null) {
        return null;
    }
    return element.closest(".hub-todo-row");
}
function clearDropTargets(containerElement) {
    const dropTargets = containerElement.querySelectorAll(".hub-todo-row-drop-target");
    dropTargets.forEach((dropTarget) => {
        dropTarget.classList.remove("hub-todo-row-drop-target");
        dropTarget.classList.remove("hub-todo-row-drop-after");
    });
}
function removeDropPlaceholder(state) {
    if (state.placeholderElement === null) {
        return;
    }
    state.placeholderElement.remove();
    state.placeholderElement = null;
}
function shouldPlaceAfterByPosition(event, targetRow) {
    const targetRect = targetRow.getBoundingClientRect();
    if (event.clientY < targetRect.top || event.clientY > targetRect.bottom) {
        return null;
    }
    return event.clientY >= targetRect.top + targetRect.height / 2;
}
function shouldPlaceAfterByRowOrder(containerElement, sourceTaskId, targetRow) {
    const rows = Array.from(containerElement.querySelectorAll(".hub-todo-row"));
    const sourceIndex = rows.findIndex((row) => getTaskId(row) === sourceTaskId);
    const targetIndex = rows.indexOf(targetRow);
    return sourceIndex >= 0 && targetIndex >= 0 && sourceIndex < targetIndex;
}
function shouldPlaceAfter(event, containerElement, sourceTaskId, targetRow) {
    const positionResult = shouldPlaceAfterByPosition(event, targetRow);
    if (positionResult !== null) {
        return positionResult;
    }
    return shouldPlaceAfterByRowOrder(containerElement, sourceTaskId, targetRow);
}
function clearDraggingRows(containerElement) {
    const draggingRows = containerElement.querySelectorAll(".hub-todo-row-dragging");
    draggingRows.forEach((draggingRow) => draggingRow.classList.remove("hub-todo-row-dragging"));
}
function getSourceColumn(element) {
    if (element === null) {
        return null;
    }
    return element.closest(".hub-todo-column");
}
function getSourceList(element) {
    if (element === null) {
        return null;
    }
    return element.closest(".hub-todo-list");
}
function isPointerInsideSourceColumn(state, event) {
    if (state.sourceColumnElement === null) {
        return false;
    }
    const sourceRect = state.sourceColumnElement.getBoundingClientRect();
    return event.clientX >= sourceRect.left && event.clientX <= sourceRect.right && event.clientY >= sourceRect.top && event.clientY <= sourceRect.bottom;
}
function getDropPlacement(state, sourceTaskId, clientY) {
    if (state.sourceListElement === null) {
        return null;
    }
    const sourceListElement = state.sourceListElement;
    const rows = Array.from(sourceListElement.querySelectorAll(".hub-todo-row")).filter((row) => getTaskId(row) !== sourceTaskId);
    if (rows.length === 0) {
        return null;
    }
    for (const row of rows) {
        const rowRect = row.getBoundingClientRect();
        if (clientY < rowRect.top + rowRect.height / 2) {
            return { dropAfter: false, targetRow: row };
        }
    }
    return { dropAfter: true, targetRow: rows[rows.length - 1] };
}
function getSourceRow(containerElement, sourceTaskId) {
    const rows = Array.from(containerElement.querySelectorAll(".hub-todo-row"));
    return rows.find((row) => getTaskId(row) === sourceTaskId) ?? null;
}
function removeDragPreview(state) {
    if (state.previewElement === null) {
        return;
    }
    state.previewElement.remove();
    state.previewElement = null;
}
function moveDragPreview(state, event) {
    if (state.previewElement === null) {
        return;
    }
    const previewX = event.clientX - state.previewOffsetX;
    const previewY = event.clientY - state.previewOffsetY;
    state.previewElement.style.transform = `translate3d(${previewX}px, ${previewY}px, 0)`;
}
function createDragPreview(state, sourceRow, event) {
    removeDragPreview(state);
    const sourceRect = sourceRow.getBoundingClientRect();
    state.draggedRowHeight = sourceRect.height;
    const previewElement = sourceRow.cloneNode(true);
    previewElement.classList.remove("hub-todo-row-dragging");
    previewElement.classList.add("hub-todo-drag-preview");
    previewElement.style.width = `${sourceRect.width}px`;
    state.previewOffsetX = Math.min(Math.max(event.clientX - sourceRect.left, 18), sourceRect.width - 18);
    state.previewOffsetY = Math.min(Math.max(event.clientY - sourceRect.top, 18), sourceRect.height - 18);
    state.previewElement = previewElement;
    document.body.appendChild(previewElement);
    moveDragPreview(state, event);
}
function createDropPlaceholder(state, sourceRow) {
    removeDropPlaceholder(state);
    const placeholderElement = document.createElement("div");
    placeholderElement.className = "hub-todo-drop-placeholder";
    if (sourceRow !== null) {
        const sourceHeight = state.draggedRowHeight > 0 ? state.draggedRowHeight : sourceRow.getBoundingClientRect().height;
        placeholderElement.style.minHeight = `${sourceHeight}px`;
    }
    state.placeholderElement = placeholderElement;
    return placeholderElement;
}
function getPointerDistance(state, event) {
    const deltaX = event.clientX - state.startX;
    const deltaY = event.clientY - state.startY;
    return Math.sqrt(deltaX * deltaX + deltaY * deltaY);
}
function beginPointerDrag(containerElement, state, event) {
    const sourceRow = getSourceRow(containerElement, state.draggedTaskId);
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
function getPointerTargetRow(event) {
    const element = document.elementFromPoint(event.clientX, event.clientY);
    return getTaskRow(element);
}
function applyDropTarget(containerElement, state, event) {
    removeDropPlaceholder(state);
    if (state.sourceListElement === null || isPointerInsideSourceColumn(state, event) == false) {
        clearDropTargets(containerElement);
        state.dropTargetTaskId = "";
        return;
    }
    const placementOrNull = getDropPlacement(state, state.draggedTaskId, event.clientY);
    if (placementOrNull === null) {
        return;
    }
    const targetRow = placementOrNull.targetRow;
    const targetTaskId = getTaskId(targetRow);
    if (targetTaskId.length === 0 || targetTaskId === state.draggedTaskId) {
        return;
    }
    clearDropTargets(containerElement);
    state.dropTargetTaskId = targetTaskId;
    state.dropAfter = placementOrNull.dropAfter;
    targetRow.classList.add("hub-todo-row-drop-target");
    const placeholderElement = createDropPlaceholder(state, getSourceRow(containerElement, state.draggedTaskId));
    if (state.dropAfter) {
        targetRow.classList.add("hub-todo-row-drop-after");
        targetRow.insertAdjacentElement("afterend", placeholderElement);
    }
    else {
        targetRow.insertAdjacentElement("beforebegin", placeholderElement);
    }
}
function resetPointerDragState(containerElement, state) {
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
export function initializeTodoSortable(containerElement, dotNetReference) {
    if (isValidTodoContainer(containerElement) == false) {
        return;
    }
    disposeTodoSortable(containerElement);
    const abortController = new AbortController();
    const state = { abortController: abortController, draggedRowHeight: 0, draggedTaskId: "", dropAfter: false, dropTargetTaskId: "", isPointerDragging: false, placeholderElement: null, pointerId: -1, previewElement: null, previewOffsetX: 0, previewOffsetY: 0, sourceColumnElement: null, sourceListElement: null, startX: 0, startY: 0 };
    const signal = abortController.signal;
    containerElement.addEventListener("pointerdown", (event) => {
        if (event.button !== 0) {
            return;
        }
        const handleElement = event.target?.closest(".hub-todo-drag-handle") ?? null;
        if (handleElement === null) {
            return;
        }
        const taskId = getTaskId(handleElement);
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
    containerElement.addEventListener("pointermove", (event) => {
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
    containerElement.addEventListener("pointerup", async (event) => {
        if (state.pointerId !== event.pointerId || state.draggedTaskId.length === 0) {
            return;
        }
        if (state.isPointerDragging == false) {
            resetPointerDragState(containerElement, state);
            return;
        }
        const sourceTaskId = state.draggedTaskId;
        const targetRow = getPointerTargetRow(event);
        let targetTaskId = state.dropTargetTaskId;
        let shouldMoveAfter = state.dropAfter;
        if (targetTaskId.length === 0 && state.sourceListElement !== null && isPointerInsideSourceColumn(state, event)) {
            const placementOrNull = getDropPlacement(state, sourceTaskId, event.clientY);
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
        const methodName = shouldMoveAfter ? "MoveTaskAfterFromScript" : "MoveTaskBeforeFromScript";
        await dotNetReference.invokeMethodAsync(methodName, sourceTaskId, targetTaskId);
        event.preventDefault();
    }, { signal: signal });
    containerElement.addEventListener("pointercancel", (event) => {
        if (state.pointerId !== event.pointerId) {
            return;
        }
        resetPointerDragState(containerElement, state);
    }, { signal: signal });
    containerElement.addEventListener("dragstart", (event) => {
        const handleElement = event.target?.closest(".hub-todo-drag-handle") ?? null;
        if (handleElement === null || event.dataTransfer === null) {
            event.preventDefault();
            return;
        }
        const taskId = getTaskId(handleElement);
        if (taskId.length === 0) {
            event.preventDefault();
            return;
        }
        state.draggedTaskId = taskId;
        const taskRow = getTaskRow(handleElement);
        if (taskRow !== null) {
            state.sourceColumnElement = getSourceColumn(taskRow);
            state.sourceListElement = getSourceList(taskRow);
            state.draggedRowHeight = taskRow.getBoundingClientRect().height;
            taskRow.classList.add("hub-todo-row-dragging");
        }
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", taskId);
    }, { signal: signal });
    containerElement.addEventListener("dragover", (event) => {
        const targetRow = getTaskRow(event.target);
        if (targetRow === null) {
            return;
        }
        const targetTaskId = getTaskId(targetRow);
        if (targetTaskId.length === 0 || targetTaskId === state.draggedTaskId) {
            return;
        }
        event.preventDefault();
        applyDropTarget(containerElement, state, event);
    }, { signal: signal });
    containerElement.addEventListener("drop", async (event) => {
        const targetRow = getTaskRow(event.target);
        if (targetRow === null || event.dataTransfer === null) {
            return;
        }
        const sourceTaskId = event.dataTransfer.getData("text/plain") || state.draggedTaskId;
        const placementOrNull = state.sourceListElement !== null && isPointerInsideSourceColumn(state, event) ? getDropPlacement(state, sourceTaskId, event.clientY) : null;
        if (state.dropTargetTaskId.length === 0 && placementOrNull === null) {
            resetPointerDragState(containerElement, state);
            return;
        }
        const targetTaskId = state.dropTargetTaskId.length > 0 ? state.dropTargetTaskId : placementOrNull !== null ? getTaskId(placementOrNull.targetRow) : getTaskId(targetRow);
        const shouldMoveAfter = state.dropTargetTaskId.length > 0 ? state.dropAfter : placementOrNull !== null ? placementOrNull.dropAfter : shouldPlaceAfter(event, containerElement, sourceTaskId, targetRow);
        resetPointerDragState(containerElement, state);
        if (sourceTaskId.length === 0 || targetTaskId.length === 0 || sourceTaskId === targetTaskId) {
            return;
        }
        event.preventDefault();
        const methodName = shouldMoveAfter ? "MoveTaskAfterFromScript" : "MoveTaskBeforeFromScript";
        await dotNetReference.invokeMethodAsync(methodName, sourceTaskId, targetTaskId);
    }, { signal: signal });
    containerElement.addEventListener("dragend", () => {
        resetPointerDragState(containerElement, state);
    }, { signal: signal });
    todoBoardStates.set(containerElement, state);
}
export function disposeTodoSortable(containerElement) {
    if (isValidTodoContainer(containerElement) == false) {
        return;
    }
    const state = todoBoardStates.get(containerElement);
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
//# sourceMappingURL=organizationHubTodoBoard.js.map