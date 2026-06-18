// OmniReport.Viewer · client-side helpers.
// Loaded automatically by hosts that wire the static-web-asset:
//   <script src="_content/Reporting.Viewer.Blazor/reporting-viewer.js"></script>
window.omniViewer = window.omniViewer || {
    /**
     * Triggers a browser download from a byte array shipped by the .NET runtime.
     * @param {string} fileName
     * @param {string} mimeType
     * @param {Uint8Array|number[]} bytes
     */
    download: function (fileName, mimeType, bytes) {
        const buffer = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        const blob = new Blob([buffer], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        anchor.style.display = "none";
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        // Release the object URL after the click; setTimeout gives the browser a tick
        // to start the download before we revoke.
        setTimeout(function () { URL.revokeObjectURL(url); }, 60_000);
    }
};
