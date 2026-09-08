/**
 * ANNAP mobile viewport stabilization — VisualViewport, iOS / in-app browser height.
 * Writes CSS variables on :root (and --vv-h/--vv-w on #annap-root when present).
 * rAF-coalesced handlers; freezes layout height during pinch-zoom to avoid tray/modal jumps.
 */
(function (global) {
    "use strict";

    var doc = global.document;
    var html = doc.documentElement;
    var rafPending = false;
    var settleTimer = null;
    var lastScale = 1;
    var frozenH = 0;
    var frozenW = 0;
    var PINCH_EPS = 0.015;

    function readVisualViewport() {
        var vv = global.visualViewport;
        if (!vv || typeof vv.height !== "number" || vv.height <= 0) return null;
        return vv;
    }

    function isPinching(vv) {
        if (!vv || vv.scale == null) return false;
        return vv.scale > 1 + PINCH_EPS;
    }

    function layoutMetrics() {
        var vv = readVisualViewport();
        var layoutH = global.innerHeight || 0;
        var layoutW = global.innerWidth || 0;
        var h = vv ? vv.height : layoutH;
        var w = vv ? vv.width : layoutW;
        if (!(h > 0)) h = layoutH;
        if (!(w > 0)) w = layoutW;
        return { vv: vv, h: h, w: w, layoutH: layoutH, layoutW: layoutW };
    }

    function writeViewportVars(m, useFrozenLayout) {
        var h = useFrozenLayout && frozenH > 0 ? frozenH : m.h;
        var w = useFrozenLayout && frozenW > 0 ? frozenW : m.w;

        html.style.setProperty("--vv-h", Math.round(h) + "px");
        html.style.setProperty("--vv-w", Math.round(w) + "px");

        if (m.vv) {
            html.style.setProperty("--vv-offset-top", Math.round(m.vv.offsetTop || 0) + "px");
            html.style.setProperty("--vv-offset-left", Math.round(m.vv.offsetLeft || 0) + "px");
            html.style.setProperty("--vv-page-top", Math.round(m.vv.pageTop || 0) + "px");
            html.style.setProperty("--vv-page-left", Math.round(m.vv.pageLeft || 0) + "px");
            html.style.setProperty(
                "--vv-scale",
                String(m.vv.scale != null && m.vv.scale > 0 ? m.vv.scale : 1)
            );
        } else {
            html.style.setProperty("--vv-offset-top", "0px");
            html.style.setProperty("--vv-offset-left", "0px");
            html.style.setProperty("--vv-page-top", "0px");
            html.style.setProperty("--vv-page-left", "0px");
            html.style.setProperty("--vv-scale", "1");
        }

        var root = doc.getElementById("annap-root");
        if (root) {
            root.style.setProperty("--vv-h", Math.round(h) + "px");
            root.style.setProperty("--vv-w", Math.round(w) + "px");
        }
    }

    function scheduleSettle() {
        if (settleTimer) global.clearTimeout(settleTimer);
        settleTimer = global.setTimeout(function () {
            settleTimer = null;
            applySync(true);
        }, 150);
    }

    function applySync(forceFull) {
        if (html.classList.contains("annap-overlay-open")) {
            return;
        }

        var m = layoutMetrics();
        var pinching = isPinching(m.vv);
        var scale = m.vv && m.vv.scale > 0 ? m.vv.scale : 1;

        if (!forceFull && pinching) {
            if (!frozenH) {
                frozenH = m.h;
                frozenW = m.w;
            }
            writeViewportVars(m, true);
            lastScale = scale;
            scheduleSettle();
            return;
        }

        if (forceFull || !pinching) {
            frozenH = m.h;
            frozenW = m.w;
            lastScale = scale;
        }

        writeViewportVars(m, false);
    }

    function requestSync(forceFull) {
        if (rafPending) return;
        rafPending = true;
        global.requestAnimationFrame(function () {
            rafPending = false;
            try {
                applySync(!!forceFull);
            } catch (_e) {
                /* ignore */
            }
        });
    }

    function wireListeners() {
        global.addEventListener("resize", function () {
            requestSync(false);
        }, { passive: true });
        global.addEventListener("orientationchange", function () {
            frozenH = 0;
            frozenW = 0;
            requestSync(true);
        }, { passive: true });
        if (readVisualViewport()) {
            global.visualViewport.addEventListener("resize", function () {
                requestSync(false);
            }, { passive: true });
            global.visualViewport.addEventListener("scroll", function () {
                var vv = readVisualViewport();
                if (isPinching(vv)) {
                    writeViewportVars(layoutMetrics(), true);
                    scheduleSettle();
                    return;
                }
                requestSync(false);
            }, { passive: true });
        }
        doc.addEventListener("visibilitychange", function () {
            if (!doc.hidden) {
                frozenH = 0;
                frozenW = 0;
                requestSync(true);
            }
        });
    }

    try {
        applySync(true);
    } catch (_e0) {
        /* ignore */
    }

    if (doc.readyState === "loading") {
        doc.addEventListener(
            "DOMContentLoaded",
            function () {
                try {
                    applySync(true);
                } catch (_e1) {
                    /* ignore */
                }
                wireListeners();
            },
            { once: true }
        );
    } else {
        try {
            applySync(true);
        } catch (_e2) {
            /* ignore */
        }
        wireListeners();
    }
})(typeof window !== "undefined" ? window : globalThis);
