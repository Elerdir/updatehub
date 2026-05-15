// Theme manager — keeps the chosen theme in localStorage so it survives
// reloads, and is applied before <body> renders to avoid a flash of
// the wrong theme. Modes:
//   "auto"  → follow prefers-color-scheme (no attribute on <html>)
//   "light" → force light (data-theme="light")
//   "dark"  → force dark  (data-theme="dark")
(function () {
    var KEY = 'updatehub-theme';

    function apply(mode) {
        if (mode === 'light' || mode === 'dark') {
            document.documentElement.setAttribute('data-theme', mode);
        } else {
            document.documentElement.removeAttribute('data-theme');
        }
    }

    window.updateHubTheme = {
        get: function () { return localStorage.getItem(KEY) || 'auto'; },
        set: function (mode) {
            localStorage.setItem(KEY, mode);
            apply(mode);
        }
    };

    // Apply immediately so the first paint matches the saved choice
    apply(window.updateHubTheme.get());
})();
