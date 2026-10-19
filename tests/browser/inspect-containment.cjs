// Inspect painted content, distinguishing intentional clipping/scrolling from visible cell overflow.
module.exports = async function inspectContainment(page, selectors) {
    return page.locator(selectors).evaluateAll(scopes => {
        const violations = [];
        let inspected = 0;
        let clipped = 0;
        const tolerance = 1;
        function isClipped(element, scope, rectangle) {
            for (let ancestor = element; ancestor; ancestor = ancestor.parentElement) {
                const style = getComputedStyle(ancestor);
                const box = ancestor.getBoundingClientRect();
                if (['hidden', 'clip', 'auto', 'scroll'].includes(style.overflowX) && (rectangle.left < box.left - tolerance || rectangle.right > box.right + tolerance)) return true;
                if (ancestor === scope) break;
            }
            return false;
        }
        for (const scope of scopes) {
            const boundary = scope.getBoundingClientRect();
            if (!boundary.width || !boundary.height) continue;
            inspected++;
            for (const element of scope.querySelectorAll('*')) {
                if (element instanceof SVGElement) continue;
                const box = element.getBoundingClientRect();
                if (!box.width || !box.height) continue;
                const rectangles = [box];
                for (const node of element.childNodes) {
                    if (node.nodeType !== Node.TEXT_NODE || !node.textContent.trim()) continue;
                    const range = document.createRange();
                    range.selectNodeContents(node);
                    rectangles.push(...range.getClientRects());
                }
                for (const rectangle of rectangles) {
                    if (rectangle.left >= boundary.left - tolerance && rectangle.right <= boundary.right + tolerance) continue;
                    if (isClipped(element, scope, rectangle)) { clipped++; continue; }
                    violations.push({ scope: scope.className, element: element.className, text: element.textContent.slice(0, 90), left: rectangle.left - boundary.left, right: rectangle.right - boundary.right });
                    break;
                }
            }
        }
        return { inspected, clipped, violations };
    });
};
