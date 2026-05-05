/**
 * Column resize support for the TableNodeWidget.
 * Follows the same document-level mousemove/mouseup pattern as floatingPanel.js.
 */
window.TableResize = (function () {

    /**
     * Begin a column resize drag.
     * @param {DotNetObjectReference} dotNetRef  Blazor component reference (has [JSInvokable] methods)
     * @param {string} colKey                    Unique column key
     * @param {number} startX                    ClientX at mousedown
     */
    function startColumnResize(dotNetRef, colKey, startX) {
        // Derive the actual rendered width of the <th> from the DOM so resizing
        // auto-sized columns starts at the correct width rather than a hardcoded default.
        const escaped = colKey.replace(/"/g, '\\"');
        const th = document.querySelector(`th[data-colkey="${escaped}"]`);
        const startWidth = th ? th.offsetWidth : 80;

        let lastX = startX;

        function onMove(e) {
            const delta = e.clientX - lastX;
            lastX = e.clientX;
            dotNetRef.invokeMethodAsync('OnColResizeDelta', colKey, delta);
        }

        function onUp() {
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
            dotNetRef.invokeMethodAsync('OnColResizeEnd', colKey);
        }

        // Set initial width so the first delta is relative to the actual rendered width
        dotNetRef.invokeMethodAsync('OnColResizeBegin', colKey, startWidth);

        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
    }

    return { startColumnResize };
})();
