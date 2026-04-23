window.FloatingPanel = (function () {
    // Map of panelId → { el, left, top } for panels currently open
    const panels = {};

    function startDrag(panelId, startClientX, startClientY, dotNetRef) {
        const el = document.getElementById(panelId);
        if (!el) return;

        const startLeft = parseInt(el.style.left) || 0;
        const startTop  = parseInt(el.style.top)  || 0;

        function onMove(e) {
            const dx = e.clientX - startClientX;
            const dy = e.clientY - startClientY;
            const newLeft = Math.max(0, startLeft + dx);
            const newTop  = Math.max(0, startTop  + dy);
            el.style.left = newLeft + 'px';
            el.style.top  = newTop  + 'px';
        }

        function onUp(e) {
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup',   onUp);
            const finalLeft = parseInt(el.style.left) || startLeft;
            const finalTop  = parseInt(el.style.top)  || startTop;
            dotNetRef.invokeMethodAsync('OnDragEnd', finalLeft, finalTop);
        }

        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup',   onUp);
    }

    function startResize(panelId, startClientX, startClientY, dotNetRef) {
        const el = document.getElementById(panelId);
        if (!el) return;

        const startWidth  = el.offsetWidth;
        const startHeight = el.offsetHeight;

        function onMove(e) {
            const newWidth  = Math.max(200, startWidth  + (e.clientX - startClientX));
            const newHeight = Math.max(80,  startHeight + (e.clientY - startClientY));
            el.style.width  = newWidth  + 'px';
            el.style.height = newHeight + 'px';
        }

        function onUp(e) {
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup',   onUp);
            const finalW = parseInt(el.style.width)  || startWidth;
            const finalH = parseInt(el.style.height) || startHeight;
            dotNetRef.invokeMethodAsync('OnResizeEnd', finalW, finalH);
        }

        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup',   onUp);
    }

    function clampToViewport(panelId) {
        const el = document.getElementById(panelId);
        if (!el) return null;
        const rect = el.getBoundingClientRect();
        const vw = window.innerWidth;
        const vh = window.innerHeight;
        let left = parseInt(el.style.left) || 0;
        let top  = parseInt(el.style.top)  || 0;
        left = Math.max(0, Math.min(left, vw - rect.width  - 8));
        top  = Math.max(0, Math.min(top,  vh - rect.height - 8));
        el.style.left = left + 'px';
        el.style.top  = top  + 'px';
        return [left, top];
    }

    function saveState(key, left, top, width, height) {
        try {
            localStorage.setItem('fp-state-' + key,
                JSON.stringify({ left, top, width, height }));
        } catch { }
    }

    function loadState(key) {
        try {
            const raw = localStorage.getItem('fp-state-' + key);
            if (!raw) return null;
            return JSON.parse(raw);
        } catch { return null; }
    }

    return { startDrag, startResize, clampToViewport, saveState, loadState };
})();
