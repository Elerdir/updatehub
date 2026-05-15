// Wires drag-and-drop events on .dropzone elements to forward dropped files
// into a nested <input type="file"> (Blazor's InputFile renders one). Uses a
// MutationObserver so it works after Blazor re-renders the page.
(function () {
    function wire(zone) {
        if (zone.__uhWired) return;
        zone.__uhWired = true;
        var input = zone.querySelector('input[type=file]');
        if (!input) return;

        zone.addEventListener('click', function (e) {
            // Avoid double-click when the user clicks the input itself
            if (e.target === input) return;
            input.click();
        });

        ['dragenter', 'dragover'].forEach(function (evt) {
            zone.addEventListener(evt, function (e) {
                e.preventDefault();
                e.stopPropagation();
                zone.classList.add('dragover');
            });
        });
        ['dragleave', 'drop'].forEach(function (evt) {
            zone.addEventListener(evt, function (e) {
                e.preventDefault();
                e.stopPropagation();
                zone.classList.remove('dragover');
            });
        });

        zone.addEventListener('drop', function (e) {
            if (!e.dataTransfer || !e.dataTransfer.files || !e.dataTransfer.files.length) return;
            // Set the dropped file on the nested input and dispatch a change event
            // so Blazor's InputFile picks it up.
            input.files = e.dataTransfer.files;
            input.dispatchEvent(new Event('change', { bubbles: true }));
        });
    }

    function scan() {
        document.querySelectorAll('.dropzone').forEach(wire);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            scan();
            new MutationObserver(scan).observe(document.body, { childList: true, subtree: true });
        });
    } else {
        scan();
        new MutationObserver(scan).observe(document.body, { childList: true, subtree: true });
    }
})();
