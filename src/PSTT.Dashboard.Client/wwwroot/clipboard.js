// Clipboard interop helpers for PSTT.Dashboard node copy/paste across browser windows.
// Uses the async Clipboard API (requires user gesture and HTTPS / localhost).
// Both functions return null on failure so callers can fall back to the in-memory clipboard.

// Prevent the browser's native Ctrl+S / Cmd+S save dialog — the dashboard handles it.
document.addEventListener('keydown', function (e) {
    if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
        e.preventDefault();
    }
}, true);

window.psttClipboard = {
    /**
     * Write text to the OS clipboard.
     * @param {string} text
     * @returns {Promise<boolean>} true on success, false on failure
     */
    writeText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },

    /**
     * Read text from the OS clipboard.
     * @returns {Promise<string|null>} clipboard text, or null on failure/permission denied
     */
    readText: async function () {
        try {
            return await navigator.clipboard.readText();
        } catch {
            return null;
        }
    }
};
