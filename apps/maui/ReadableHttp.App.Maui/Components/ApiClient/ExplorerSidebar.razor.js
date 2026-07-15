const VIEWPORT_MARGIN = 8;
const MENU_GAP = 4;

export function positionOpenMenu(sidebar) {
    const host = sidebar?.querySelector(".row-actions.open, .title-actions.open");
    const menu = host?.querySelector(":scope > .more-menu");
    const triggers = host?.querySelectorAll(":scope > button");
    const trigger = triggers?.item(triggers.length - 1);

    if (!menu || !trigger) {
        return;
    }

    menu.dataset.positioned = "false";
    menu.style.right = "auto";
    menu.style.bottom = "auto";

    const triggerRect = trigger.getBoundingClientRect();
    const menuRect = menu.getBoundingClientRect();
    const maxLeft = Math.max(VIEWPORT_MARGIN, window.innerWidth - menuRect.width - VIEWPORT_MARGIN);
    const left = Math.min(maxLeft, Math.max(VIEWPORT_MARGIN, triggerRect.right - menuRect.width));

    const below = triggerRect.bottom + MENU_GAP;
    const above = triggerRect.top - menuRect.height - MENU_GAP;
    const fitsBelow = below + menuRect.height <= window.innerHeight - VIEWPORT_MARGIN;
    const top = fitsBelow
        ? below
        : Math.max(VIEWPORT_MARGIN, above);

    menu.style.left = `${left}px`;
    menu.style.top = `${top}px`;
    menu.dataset.positioned = "true";
}
