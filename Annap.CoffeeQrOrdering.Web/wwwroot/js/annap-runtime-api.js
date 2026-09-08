/**
 * Same-origin API and SignalR helpers. Depends on window.__annapRuntime from _AnnapRuntimeHead.
 * publicBaseUrl is for QR/external links only; API and hubs always use relative paths unless
 * crossOriginApiBase is explicitly set to a different origin (split deployment).
 */
(function (w) {
    "use strict";

    function crossOriginApiBase() {
        var r = w.__annapRuntime || {};
        var s = r.crossOriginApiBase || "";
        if (!s) return "";
        try {
            var configured = new URL(String(s).replace(/\/$/, "") + "/");
            if (configured.origin === w.location.origin) return "";
            return configured.origin;
        } catch (_) {
            return "";
        }
    }

    w.__annapApiUrl = function (path) {
        var p = path == null ? "/" : String(path);
        if (p.charAt(0) !== "/") p = "/" + p;
        var b = crossOriginApiBase();
        return b ? b + p : p;
    };

    w.__annapHubUrl = function () {
        var b = crossOriginApiBase();
        return b ? b + "/hubs/orders" : "/hubs/orders";
    };
})(window);
