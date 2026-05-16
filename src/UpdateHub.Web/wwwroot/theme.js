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
//
// Two MutationObservers keep things consistent across Blazor's enhanced
// navigation:
//   1. body subtree — re-wire any dropdowns that appear in fresh DOM
//   2. <html> data-theme attribute — Blazor patches the document element
//      when it re-renders the root component, and the new HTML doesn't
//      carry data-theme. Without this watcher, navigating away from
//      /account would flip the page back to whatever prefers-color-scheme
//      reports because the explicit override would silently vanish.
(function () {
    var KEY = 'updatehub-theme';

    function apply(mode) {
        var expected = (mode === 'light' || mode === 'dark') ? mode : null;
        var current  = document.documentElement.getAttribute('data-theme');
        if (current === expected) return;          // no-op guard prevents observer loops
        if (expected) {
            document.documentElement.setAttribute('data-theme', expected);
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

    function reassertTheme() {
        // Re-apply whenever Blazor enhanced navigation strips data-theme.
        apply(getMode());
    }

    window.updateHubTheme = { get: getMode, set: setMode };

    // Apply saved theme as early as possible (script is in <head>)
    apply(getMode());

    function startObservers() {
        // 1) Body subtree — pick up newly-rendered dropdowns + re-assert theme
        new MutationObserver(function () {
            reassertTheme();
            syncSelects();
        }).observe(document.body, { childList: true, subtree: true });

        // 2) <html> attribute — if Blazor strips data-theme during a page
        //    transition, restore it instantly so the theme flicker is invisible.
        new MutationObserver(reassertTheme).observe(document.documentElement, {
            attributes: true,
            attributeFilter: ['data-theme']
        });

        syncSelects();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startObservers);
    } else {
        startObservers();
    }
})();
