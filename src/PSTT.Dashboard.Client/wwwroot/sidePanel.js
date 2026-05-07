window.SidePanel = (() => {
    const STORAGE_KEY = 'edit-side-panel-width';
    const MIN_WIDTH = 200;
    const MAX_WIDTH = 800;

    function startResize(dotNetRef, startX, startWidth) {
        function onMouseMove(e) {
            const delta = startX - e.clientX; // panel is on the right, drag left = wider
            const newWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, startWidth + delta));
            dotNetRef.invokeMethodAsync('SetWidth', newWidth);
        }

        function onMouseUp() {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        }

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
    }

    function saveWidth(width) {
        try { localStorage.setItem(STORAGE_KEY, String(width)); } catch { }
    }

    function loadWidth() {
        try {
            const v = localStorage.getItem(STORAGE_KEY);
            if (v) return parseInt(v, 10);
        } catch { }
        return null;
    }

    return { startResize, saveWidth, loadWidth };
})();
