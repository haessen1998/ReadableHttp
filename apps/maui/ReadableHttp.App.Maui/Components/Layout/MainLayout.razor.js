let controller;

export function initialize(workbench) {
    dispose();

    if (!workbench) {
        return;
    }

    controller = new AbortController();
    const signal = controller.signal;
    const shell = workbench.closest('.api-client-shell') ?? workbench;

    wireHandle(workbench.querySelector('.left-resizer'), shell, 'left', signal);
    wireHandle(workbench.querySelector('.right-resizer'), shell, 'right', signal);
    wireRequestSplit(workbench.querySelector('.request-split-resizer'), shell, signal);
}

export function dispose() {
    controller?.abort();
    controller = undefined;
    document.body.classList.remove('workbench-is-resizing');
}

function wireHandle(handle, shell, side, signal) {
    if (!handle) {
        return;
    }

    handle.addEventListener('pointerdown', event => beginResize(event, handle, shell, side), { signal });
    handle.addEventListener('keydown', event => nudgeResize(event, handle, shell, side), { signal });
}

function wireRequestSplit(handle, shell, signal) {
    if (!handle) {
        return;
    }

    handle.addEventListener('pointerdown', event => {
        if (event.button !== 0) {
            return;
        }

        const split = handle.closest('.split-editor');
        const requestPane = handle.previousElementSibling;
        const responsePane = handle.nextElementSibling;
        if (!split || !requestPane || !responsePane) {
            return;
        }

        event.preventDefault();
        handle.setPointerCapture(event.pointerId);
        document.body.classList.add('workbench-is-resizing');

        const startX = event.clientX;
        const startWidth = requestPane.getBoundingClientRect().width;
        const totalWidth = split.getBoundingClientRect().width - handle.getBoundingClientRect().width;
        const minLeft = parsePixels(getComputedStyle(requestPane).minWidth, 360);
        const minRight = parsePixels(getComputedStyle(responsePane).minWidth, 420);
        const maxLeft = Math.max(minLeft, totalWidth - minRight);

        const onPointerMove = moveEvent => {
            const width = clamp(startWidth + moveEvent.clientX - startX, minLeft, maxLeft);
            shell.style.setProperty('--request-editor-width', `${Math.round(width)}px`);
        };

        const endResize = () => {
            handle.removeEventListener('pointermove', onPointerMove);
            handle.removeEventListener('pointerup', endResize);
            handle.removeEventListener('pointercancel', endResize);
            document.body.classList.remove('workbench-is-resizing');
        };

        handle.addEventListener('pointermove', onPointerMove);
        handle.addEventListener('pointerup', endResize);
        handle.addEventListener('pointercancel', endResize);
    }, { signal });
}

function beginResize(event, handle, shell, side) {
    if (event.button !== 0) {
        return;
    }

    const panel = side === 'left' ? handle.previousElementSibling : handle.nextElementSibling;
    if (!panel) {
        return;
    }

    event.preventDefault();
    handle.setPointerCapture(event.pointerId);
    document.body.classList.add('workbench-is-resizing');
    shell.dataset.resizingSide = side;

    const startX = event.clientX;
    const startWidth = panel.getBoundingClientRect().width;
    const bounds = readBounds(panel, shell);

    const onPointerMove = moveEvent => {
        const delta = moveEvent.clientX - startX;
        const width = side === 'left'
            ? startWidth + delta
            : startWidth - delta;

        setPanelWidth(shell, side, clamp(width, bounds.min, bounds.max));
    };

    const endResize = () => {
        handle.removeEventListener('pointermove', onPointerMove);
        handle.removeEventListener('pointerup', endResize);
        handle.removeEventListener('pointercancel', endResize);
        document.body.classList.remove('workbench-is-resizing');
        delete shell.dataset.resizingSide;
    };

    handle.addEventListener('pointermove', onPointerMove);
    handle.addEventListener('pointerup', endResize);
    handle.addEventListener('pointercancel', endResize);
}

function nudgeResize(event, handle, shell, side) {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
        return;
    }

    const panel = side === 'left' ? handle.previousElementSibling : handle.nextElementSibling;
    if (!panel) {
        return;
    }

    event.preventDefault();
    const direction = event.key === 'ArrowRight' ? 1 : -1;
    const step = event.shiftKey ? 32 : 12;
    const currentWidth = panel.getBoundingClientRect().width;
    const bounds = readBounds(panel, shell);
    const width = side === 'left'
        ? currentWidth + (direction * step)
        : currentWidth - (direction * step);

    setPanelWidth(shell, side, clamp(width, bounds.min, bounds.max));
}

function setPanelWidth(shell, side, width) {
    shell.style.setProperty(side === 'left' ? '--sidebar-width' : '--inspector-width', `${Math.round(width)}px`);
}

function readBounds(panel, shell) {
    const panelStyle = getComputedStyle(panel);
    const shellWidth = shell.getBoundingClientRect().width;
    const min = parsePixels(panelStyle.minWidth, 180);
    const cssMax = parsePixels(panelStyle.maxWidth, shellWidth);
    const layoutMax = Math.max(min, shellWidth - 360);

    return {
        min,
        max: Math.max(min, Math.min(cssMax, layoutMax))
    };
}

function parsePixels(value, fallback) {
    const parsed = Number.parseFloat(value);
    return Number.isFinite(parsed) ? parsed : fallback;
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}
