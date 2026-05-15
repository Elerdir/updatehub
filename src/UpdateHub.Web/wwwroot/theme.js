// Theme manager — keeps the chosen theme in localStorage so it survives
// reloads, and is applied before <body> renders to avoid a flash of
// the wrong theme. Modes:
//   "auto"  → follow prefers-color-scheme (no attribute on <html>)
//   "light" → force light (data-theme="light")
//   "dark"  → force dark  (data-theme="dark")
//
// Any <select class="js-theme-select"> on the page is auto-wired:
//   - On render its value is set to the saved choice
//   - On change the chosen mode is applied + persisted
// A MutationObserver re-runs the sync whenever Blazor swaps in new
// DOM (e.g. when navigating to /account) so the dropdown shows up
// already wired without needing JS interop from the server.
(function () {
    var KEY = 'updatehub-theme';

    function apply(mode) {
        if (mode === 'light' || mode === 'dark') {
            document.documentElement.setAttribute('data-theme', mode);
        } else {
            document.documentElement.removeAttribute('data-theme');
        }
    }

    function getMode() { return localStorage.getItem(KEY) || 'auto'; }

    function setMode(mode) {
        localStorage.setItem(KEY, mode);
        apply(mode);
        syncSelects();
    }

    function onSelectChange(ev) { setMode(ev.target.value); }

    function syncSelects() {
        var current = getMode();
        document.querySelectorAll('select.js-theme-select').forEach(function (sel) {
            if (sel.value !== current) sel.value = current;
            if (!sel.__uhWired) {
                sel.addEventListener('change', onSelectChange);
                sel.__uhWired = true;
            }
        });
    }

    window.updateHubTheme = { get: getMode, set: setMode };

    // Apply saved theme as early as possible (script is in <head>)
    apply(getMode());

    // Whenever the DOM changes (Blazor renders new components), re-sync
    function startObserver() {
        new MutationObserver(syncSelects).observe(document.body, {
            childList: true, subtree: true
        });
        syncSelects();
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startObserver);
    } else {
        startObserver();
    }
})();
