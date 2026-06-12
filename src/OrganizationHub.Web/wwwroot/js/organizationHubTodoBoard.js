const todoBoardStates = new WeakMap();
const dragStartDistanceInPixels = 6;
function isValidTodoContainer(containerElement) {
    return containerElement instanceof HTMLElement;
}
function getTaskID(element) {
    if (element === null) {
        return "";
    }
    const taskElement = element.closest("[data-task-id]");
    if (taskElement === null) {
        return "";
    }
    const taskID = taskElement.dataset.taskId;
    return taskID ?? "";
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
function shouldPlaceAfterByRowOrder(containerElement, sourceTaskID, targetRow) {
    const rows = Array.from(containerElement.querySelectorAll(".hub-todo-row"));
    const sourceIndex = rows.findIndex((row) => getTaskID(row) === sourceTaskID);
    const targetIndex = rows.indexOf(targetRow);
    return sourceIndex >= 0 && targetIndex >= 0 && sourceIndex < targetIndex;
}
function shouldPlaceAfter(event, containerElement, sourceTaskID, targetRow) {
    const positionResult = shouldPlaceAfterByPosition(event, targetRow);
    if (positionResult !== null) {
        return positionResult;
    }
    return shouldPlaceAfterByRowOrder(containerElement, sourceTaskID, targetRow);
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
function getDropPlacement(state, sourceTaskID, clientY) {
    if (state.sourceListElement === null) {
        return null;
    }
    const sourceListElement = state.sourceListElement;
    const rows = Array.from(sourceListElement.querySelectorAll(".hub-todo-row")).filter((row) => getTaskID(row) !== sourceTaskID);
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
function getSourceRow(containerElement, sourceTaskID) {
    const rows = Array.from(containerElement.querySelectorAll(".hub-todo-row"));
    return rows.find((row) => getTaskID(row) === sourceTaskID) ?? null;
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
    const sourceRow = getSourceRow(containerElement, state.draggedTaskID);
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
        state.dropTargetTaskID = "";
        return;
    }
    const placementOrNull = getDropPlacement(state, state.draggedTaskID, event.clientY);
    if (placementOrNull === null) {
        return;
    }
    const targetRow = placementOrNull.targetRow;
    const targetTaskID = getTaskID(targetRow);
    if (targetTaskID.length === 0 || targetTaskID === state.draggedTaskID) {
        return;
    }
    clearDropTargets(containerElement);
    state.dropTargetTaskID = targetTaskID;
    state.dropAfter = placementOrNull.dropAfter;
    targetRow.classList.add("hub-todo-row-drop-target");
    const placeholderElement = createDropPlaceholder(state, getSourceRow(containerElement, state.draggedTaskID));
    if (state.dropAfter) {
        targetRow.classList.add("hub-todo-row-drop-after");
        targetRow.insertAdjacentElement("afterend", placeholderElement);
    }
    else {
        targetRow.insertAdjacentElement("beforebegin", placeholderElement);
    }
}
function resetPointerDragState(containerElement, state) {
    state.draggedTaskID = "";
    state.draggedRowHeight = 0;
    state.dropAfter = false;
    state.dropTargetTaskID = "";
    state.isPointerDragging = false;
    state.pointerID = -1;
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
    const state = { abortController: abortController, draggedRowHeight: 0, draggedTaskID: "", dropAfter: false, dropTargetTaskID: "", isPointerDragging: false, placeholderElement: null, pointerID: -1, previewElement: null, previewOffsetX: 0, previewOffsetY: 0, sourceColumnElement: null, sourceListElement: null, startX: 0, startY: 0 };
    const signal = abortController.signal;
    containerElement.addEventListener("pointerdown", (event) => {
        if (event.button !== 0) {
            return;
        }
        const handleElement = event.target?.closest(".hub-todo-drag-handle") ?? null;
        if (handleElement === null) {
            return;
        }
        const taskID = getTaskID(handleElement);
        if (taskID.length === 0) {
            return;
        }
        state.draggedTaskID = taskID;
        state.pointerID = event.pointerId;
        state.startX = event.clientX;
        state.startY = event.clientY;
        handleElement.setPointerCapture(event.pointerId);
        event.preventDefault();
    }, { signal: signal });
    containerElement.addEventListener("pointermove", (event) => {
        if (state.pointerID !== event.pointerId || state.draggedTaskID.length === 0) {
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
        if (state.pointerID !== event.pointerId || state.draggedTaskID.length === 0) {
            return;
        }
        if (state.isPointerDragging == false) {
            resetPointerDragState(containerElement, state);
            return;
        }
        const sourceTaskID = state.draggedTaskID;
        const targetRow = getPointerTargetRow(event);
        let targetTaskID = state.dropTargetTaskID;
        let shouldMoveAfter = state.dropAfter;
        if (targetTaskID.length === 0 && state.sourceListElement !== null && isPointerInsideSourceColumn(state, event)) {
            const placementOrNull = getDropPlacement(state, sourceTaskID, event.clientY);
            if (placementOrNull !== null) {
                targetTaskID = getTaskID(placementOrNull.targetRow);
                shouldMoveAfter = placementOrNull.dropAfter;
            }
            else if (targetRow !== null) {
                targetTaskID = getTaskID(targetRow);
                shouldMoveAfter = shouldPlaceAfter(event, containerElement, sourceTaskID, targetRow);
            }
        }
        if (targetTaskID.length === 0) {
            resetPointerDragState(containerElement, state);
            return;
        }
        resetPointerDragState(containerElement, state);
        if (targetTaskID.length === 0 || sourceTaskID === targetTaskID) {
            return;
        }
        const methodName = shouldMoveAfter ? "MoveTaskAfterFromScript" : "MoveTaskBeforeFromScript";
        await dotNetReference.invokeMethodAsync(methodName, sourceTaskID, targetTaskID);
        event.preventDefault();
    }, { signal: signal });
    containerElement.addEventListener("pointercancel", (event) => {
        if (state.pointerID !== event.pointerId) {
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
        const taskID = getTaskID(handleElement);
        if (taskID.length === 0) {
            event.preventDefault();
            return;
        }
        state.draggedTaskID = taskID;
        const taskRow = getTaskRow(handleElement);
        if (taskRow !== null) {
            state.sourceColumnElement = getSourceColumn(taskRow);
            state.sourceListElement = getSourceList(taskRow);
            state.draggedRowHeight = taskRow.getBoundingClientRect().height;
            taskRow.classList.add("hub-todo-row-dragging");
        }
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", taskID);
    }, { signal: signal });
    containerElement.addEventListener("dragover", (event) => {
        const targetRow = getTaskRow(event.target);
        if (targetRow === null) {
            return;
        }
        const targetTaskID = getTaskID(targetRow);
        if (targetTaskID.length === 0 || targetTaskID === state.draggedTaskID) {
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
        const sourceTaskID = event.dataTransfer.getData("text/plain") || state.draggedTaskID;
        const placementOrNull = state.sourceListElement !== null && isPointerInsideSourceColumn(state, event) ? getDropPlacement(state, sourceTaskID, event.clientY) : null;
        if (state.dropTargetTaskID.length === 0 && placementOrNull === null) {
            resetPointerDragState(containerElement, state);
            return;
        }
        const targetTaskID = state.dropTargetTaskID.length > 0 ? state.dropTargetTaskID : placementOrNull !== null ? getTaskID(placementOrNull.targetRow) : getTaskID(targetRow);
        const shouldMoveAfter = state.dropTargetTaskID.length > 0 ? state.dropAfter : placementOrNull !== null ? placementOrNull.dropAfter : shouldPlaceAfter(event, containerElement, sourceTaskID, targetRow);
        resetPointerDragState(containerElement, state);
        if (sourceTaskID.length === 0 || targetTaskID.length === 0 || sourceTaskID === targetTaskID) {
            return;
        }
        event.preventDefault();
        const methodName = shouldMoveAfter ? "MoveTaskAfterFromScript" : "MoveTaskBeforeFromScript";
        await dotNetReference.invokeMethodAsync(methodName, sourceTaskID, targetTaskID);
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