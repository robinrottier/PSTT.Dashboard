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

    return { startDrag, clampToViewport };
})();
