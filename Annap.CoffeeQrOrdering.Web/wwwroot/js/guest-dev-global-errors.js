/**
 * Development-only: global errors, unhandled rejections, resource load failures, fetch aborts.
 */
(function () {
    "use strict";

    function ensureOverlay() {
        var id = "annap-dev-error-overlay";
        var el = document.getElementById(id);
        if (el) return el;
        el = document.createElement("div");
        el.id = id;
        el.setAttribute("role", "log");
        el.setAttribute("aria-live", "assertive");
        el.style.cssText =
            "position:fixed;top:0;left:0;right:0;z-index:2147483000;max-height:42vh;overflow:auto;" +
            "background:rgba(40,10,10,0.94);color:#fecaca;font:11px/1.35 system-ui,monospace;padding:8px;" +
            "border-bottom:2px solid #f87171;box-sizing:border-box;";
        (document.body || document.documentElement).appendChild(el);
        return el;
    }

    function appendLine(kind, text) {
        try {
            console.warn("[annap-dev]", kind, text);
        } catch (e1) {
            /* ignore */
        }
        var host = ensureOverlay();
        var row = document.createElement("div");
        row.style.cssText = "border-bottom:1px solid rgba(255,255,255,0.12);padding:4px 2px;word-break:break-word;";
        row.textContent = new Date().toISOString().slice(11, 23) + " " + kind + " " + text;
        host.appendChild(row);
        while (host.childNodes.length > 40) host.removeChild(host.firstChild);
    }

    window.onerror = function (msg, src, line, col, err) {
        var extra = err && err.stack ? err.stack : "";
        appendLine("onerror", String(msg) + " @ " + String(src) + ":" + line + ":" + col + " " + extra);
        return false;
    };

    window.onunhandledrejection = function (ev) {
        var r = ev && ev.reason;
        var t = r && r.stack ? r.stack : String(r);
        appendLine("unhandledrejection", t);
    };

    document.addEventListener(
        "error",
        function (ev) {
            var t = ev && ev.target;
            if (!t || !t.tagName) return;
            var tag = t.tagName;
            if (tag === "SCRIPT" || tag === "LINK" || tag === "IMG") {
                var src = t.src || t.href || "";
                appendLine("resource-error", tag + " " + src);
            }
        },
        true
    );

    if (typeof window.fetch === "function") {
        var origFetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            var url =
                typeof input === "string"
                    ? input
                    : input && typeof input === "object" && "url" in input
                      ? input.url
                      : String(input);
            var ac = init && init.signal;
            if (ac && typeof ac.addEventListener === "function") {
                ac.addEventListener("abort", function () {
                    appendLine("fetch-abort", url);
                });
            }
            return origFetch(input, init).catch(function (err) {
                appendLine("fetch-reject", url + " " + (err && err.message ? err.message : String(err)));
                throw err;
            });
        };
    }
})();
