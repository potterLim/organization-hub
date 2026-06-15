const sortableStateByRoot = new WeakMap();

function getSortableContainers(rootElement) {
    if (rootElement === null || rootElement === undefined) {
        return [];
    }

    const containers = [];
    if (rootElement.matches("[data-sortable-list]")) {
        containers.push(rootElement);
    }

    rootElement.querySelectorAll("[data-sortable-list]").forEach(container => containers.push(container));
    return containers;
}

function getSortableItem(element) {
    if (element === null || element === undefined) {
        return null;
    }

    return element.closest("[data-sortable-item-id]");
}

function clearDropState(container) {
    container.querySelectorAll(".hub-sortable-row-drop-target").forEach(item => item.classList.remove("hub-sortable-row-drop-target"));
    container.querySelectorAll(".hub-sortable-row-drop-after").forEach(item => item.classList.remove("hub-sortable-row-drop-after"));
}

function createContainerState(container, dotNetReference) {
    const state = {
        sourceItem: null,
        canStartDrag: false,
        dotNetReference
    };

    const pointerDownHandler = event => {
        state.canStartDrag = event.target.closest(".hub-sortable-handle") !== null;
    };

    const pointerUpHandler = () => {
        state.canStartDrag = false;
    };

    const dragStartHandler = event => {
        if (state.canStartDrag === false) {
            event.preventDefault();
            return;
        }

        const sourceItem = getSortableItem(event.target);
        if (sourceItem === null) {
            event.preventDefault();
            return;
        }

        state.sourceItem = sourceItem;
        sourceItem.classList.add("hub-sortable-row-dragging");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", sourceItem.dataset.sortableItemId);
    };

    const dragOverHandler = event => {
        if (state.sourceItem === null) {
            return;
        }

        const targetItem = getSortableItem(event.target);
        if (targetItem === null || targetItem === state.sourceItem) {
            return;
        }

        event.preventDefault();
        const targetBounds = targetItem.getBoundingClientRect();
        const shouldMoveAfter = event.clientY > targetBounds.top + targetBounds.height / 2;
        clearDropState(container);
        targetItem.classList.add("hub-sortable-row-drop-target");
        if (shouldMoveAfter) {
            targetItem.classList.add("hub-sortable-row-drop-after");
        }
    };

    const dropHandler = async event => {
        if (state.sourceItem === null) {
            return;
        }

        const targetItem = getSortableItem(event.target);
        if (targetItem === null || targetItem === state.sourceItem) {
            return;
        }

        event.preventDefault();
        const sourceItemId = state.sourceItem.dataset.sortableItemId;
        const targetItemId = targetItem.dataset.sortableItemId;
        const shouldMoveAfter = targetItem.classList.contains("hub-sortable-row-drop-after");
        const methodName = shouldMoveAfter ? "MoveSortableItemAfterFromScript" : "MoveSortableItemBeforeFromScript";
        clearDropState(container);
        state.sourceItem.classList.remove("hub-sortable-row-dragging");
        state.sourceItem = null;

        await dotNetReference.invokeMethodAsync(methodName, sourceItemId, targetItemId);
    };

    const dragEndHandler = () => {
        if (state.sourceItem !== null) {
            state.sourceItem.classList.remove("hub-sortable-row-dragging");
        }

        state.sourceItem = null;
        state.canStartDrag = false;
        clearDropState(container);
    };

    container.addEventListener("pointerdown", pointerDownHandler);
    container.addEventListener("pointerup", pointerUpHandler);
    container.addEventListener("dragstart", dragStartHandler);
    container.addEventListener("dragover", dragOverHandler);
    container.addEventListener("drop", dropHandler);
    container.addEventListener("dragend", dragEndHandler);

    return {
        dispose() {
            container.removeEventListener("pointerdown", pointerDownHandler);
            container.removeEventListener("pointerup", pointerUpHandler);
            container.removeEventListener("dragstart", dragStartHandler);
            container.removeEventListener("dragover", dragOverHandler);
            container.removeEventListener("drop", dropHandler);
            container.removeEventListener("dragend", dragEndHandler);
            clearDropState(container);
        }
    };
}

export function initializeSortableLists(rootElement, dotNetReference) {
    disposeSortableLists(rootElement);

    const containers = getSortableContainers(rootElement);
    const containerStates = containers.map(container => createContainerState(container, dotNetReference));
    sortableStateByRoot.set(rootElement, containerStates);
}

export function disposeSortableLists(rootElement) {
    const containerStates = sortableStateByRoot.get(rootElement);
    if (containerStates === undefined) {
        return;
    }

    containerStates.forEach(containerState => containerState.dispose());
    sortableStateByRoot.delete(rootElement);
}
