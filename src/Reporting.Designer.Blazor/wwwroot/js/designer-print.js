// designer-print.js — print pipeline for the OmniReport Designer.
//
// The Designer ships a single in-app print dialog (per-report options like page range,
// paper size, copies). Once the user clicks "Imprimir", we generate a PDF in .NET, hand
// the bytes to this module, and let the browser/WebView take over for the OS-level
// printer-picker dialog. This pattern works identically in:
//   • Blazor Server     — SignalR delivers the bytes; the page runs in the user's browser
//   • Blazor WebAssembly — no roundtrip; the PDF is built in the browser already
//   • MAUI Blazor Hybrid — WebView's window.print() routes to the platform's native sheet
//
// Why an iframe and not a new tab?
//   • Pop-up blockers and PWA window-controls APIs can swallow window.open() silently.
//   • A hidden iframe attached to the current document survives strict CSP rules.
//   • Calling iframe.contentWindow.print() opens the host browser's print sheet exactly
//     as if the user pressed Ctrl+P on a real PDF.

(function () {
    const NS = "omniDesignerPrint";

    // Reuse the same iframe across print jobs — repeatedly creating/removing it has
    // shown to crash some Linux WebKitGTK builds and leaks the previous blob URL.
    let iframe = null;
    let lastBlobUrl = null;

    function ensureIframe() {
        if (iframe && document.body.contains(iframe)) return iframe;
        iframe = document.createElement("iframe");
        iframe.id = "__omni-print-frame";
        // Fully hidden but kept in the DOM — display:none would prevent the browser
        // from loading the PDF in some engines, so we shrink it instead.
        iframe.style.cssText = [
            "position:fixed",
            "right:0",
            "bottom:0",
            "width:0",
            "height:0",
            "border:0",
            "visibility:hidden",
            "pointer-events:none",
        ].join(";");
        document.body.appendChild(iframe);
        return iframe;
    }

    function revokePrevious() {
        if (lastBlobUrl) {
            try { URL.revokeObjectURL(lastBlobUrl); } catch (_) { /* swallow */ }
            lastBlobUrl = null;
        }
    }

    /**
     * Print a PDF whose bytes were generated server- or client-side. The arrangement:
     *   1. Wrap the byte buffer in a Blob with the correct MIME type. Without it,
     *      Chrome refuses to use its built-in PDF viewer and downloads the file.
     *   2. Mint an object URL — released when this function is called again, so we
     *      hold at most one URL alive at a time.
     *   3. Load it into the iframe; wait for the load event before printing because
     *      Edge/WebKit print blank pages if the document isn't ready yet.
     *   4. Call iframe.contentWindow.print(). The browser shows its native print
     *      sheet, with the printer list / duplex / scale options the OS provides.
     *      We don't poll — there's no portable success callback; the user closes
     *      the sheet and we don't care about the outcome (success / cancel are
     *      both fine for our flow).
     *
     * @param {Uint8Array} bytes  Raw PDF bytes (the typed array Blazor uses for byte[]).
     * @param {Object} options    Currently { title?, copies?, colorMode? }. Reserved
     *                            for future hints; copies is purely informational
     *                            (the OS dialog has its own copies counter — both
     *                            compound, so we always pass 1 here and rely on the
     *                            PDF's own duplication if the caller asked for >1).
     */
    function printPdfBlob(bytes, options) {
        try {
            revokePrevious();
            const opts = options || {};
            const buffer = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes || []);
            const blob = new Blob([buffer], { type: "application/pdf" });
            const url = URL.createObjectURL(blob);
            lastBlobUrl = url;

            const frame = ensureIframe();

            // Update title for screenreaders / window manager — purely cosmetic.
            if (opts.title) {
                try { frame.title = opts.title; } catch (_) { /* readonly in some browsers */ }
            }

            // Single-fire load handler. We MUST attach it before setting src,
            // or fast-loading data: blobs can fire load before we listen.
            const onLoaded = () => {
                frame.removeEventListener("load", onLoaded);
                // setTimeout 0 nudges the print call out of the load callback —
                // some browsers throw "Document not ready" if .print() is called
                // synchronously inside load.
                setTimeout(() => {
                    try {
                        frame.contentWindow.focus();
                        frame.contentWindow.print();
                    } catch (err) {
                        console.error("[omniDesignerPrint] printing failed:", err);
                    }
                }, 0);
            };
            frame.addEventListener("load", onLoaded);
            frame.src = url;
        } catch (err) {
            console.error("[omniDesignerPrint] printPdfBlob failed:", err);
            // Re-throw so the .NET side sees a JSException and can surface a toast.
            throw err;
        }
    }

    /**
     * Returns the user agent's capabilities so the Designer can show/hide options it
     * can't honor. Right now we report:
     *   • `directPrintSupported` — always false from JS (only the native MAUI adapter
     *     can print without a dialog). The Designer's NativePrinterAdapter override
     *     supersedes this when it's registered in DI.
     *   • `userAgent`           — for diagnostic logging.
     */
    function describeHost() {
        return {
            directPrintSupported: false,
            userAgent: navigator.userAgent || "",
        };
    }

    window[NS] = Object.freeze({
        printPdfBlob: printPdfBlob,
        describeHost: describeHost,
    });
})();
