/**
 * Production-safe runtime diagnostics — window.__ANNAP_RUNTIME_REPORT()
 * Lightweight overlay/compositor/animation audit for mobile stability QA.
 */
(function (global) {
    "use strict";

    var overlayOpens = 0;
    var overlayCloses = 0;

    function countListeners() {
        var out = { document: 0, window: 0 };
        try {
            if (typeof getEventListeners === "function") {
                var doc = getEventListeners(document) || {};
                var win = getEventListeners(window) || {};
                Object.keys(doc).forEach(function (k) { out.document += (doc[k] || []).length; });
                Object.keys(win).forEach(function (k) { out.window += (win[k] || []).length; });
            }
        } catch (_) {}
        return out;
    }

    function scanCompositorHeavy() {
        var nodes = [];
        var all = document.querySelectorAll ? document.querySelectorAll("*") : [];
        var cap = 80;
        for (var i = 0; i < all.length && nodes.length < cap; i++) {
            var el = all[i];
            if (!el || !el.getBoundingClientRect) continue;
            var st = global.getComputedStyle(el);
            if (!st) continue;
            var heavy = false;
            var reasons = [];
            if (st.willChange && st.willChange !== "auto") {
                heavy = true;
                reasons.push("will-change:" + st.willChange);
            }
            if ((st.backdropFilter && st.backdropFilter !== "none")
                || (st.webkitBackdropFilter && st.webkitBackdropFilter !== "none")) {
                heavy = true;
                reasons.push("backdrop-filter");
            }
            if (st.filter && st.filter !== "none" && st.filter.indexOf("drop-shadow") >= 0) {
                heavy = true;
                reasons.push("drop-shadow");
            }
            if (parseInt(st.zIndex, 10) > 200 && (st.position === "fixed" || st.position === "sticky")) {
                heavy = true;
                reasons.push("fixed-layer");
            }
            if (heavy) {
                nodes.push({
                    tag: el.tagName,
                    id: el.id || null,
                    className: (el.className && String(el.className).slice(0, 80)) || null,
                    reasons: reasons
                });
            }
        }
        return { count: nodes.length, nodes: nodes };
    }

    function scanAnimations() {
        var running = 0;
        var infinite = 0;
        try {
            var anims = document.getAnimations ? document.getAnimations() : [];
            for (var i = 0; i < anims.length; i++) {
                if (anims[i].playState === "running") running++;
                if (anims[i].effect && anims[i].effect.getTiming
                    && anims[i].effect.getTiming().iterations === Infinity) {
                    infinite++;
                }
            }
        } catch (_) {}
        return { running: running, infinite: infinite };
    }

    function overlayState() {
        var modal = document.getElementById("drink-detail-modal");
        var open = false;
        try {
            if (global.DrinkDetailModal && typeof global.DrinkDetailModal.isOpen === "function") {
                open = global.DrinkDetailModal.isOpen();
            } else if (modal) {
                open = !modal.classList.contains("hidden") && modal.getAttribute("aria-hidden") !== "true";
            }
        } catch (_) {}
        return {
            drinkDetailOpen: open,
            overlayOpens: overlayOpens,
            overlayCloses: overlayCloses
        };
    }

    function memoryEstimate() {
        var heap = null;
        try {
            if (performance && performance.memory) {
                heap = {
                    usedMb: Math.round(performance.memory.usedJSHeapSize / 1048576),
                    totalMb: Math.round(performance.memory.totalJSHeapSize / 1048576),
                    limitMb: Math.round(performance.memory.jsHeapSizeLimit / 1048576)
                };
            }
        } catch (_) {}
        return heap;
    }

    function buildReport() {
        return {
            at: new Date().toISOString(),
            ua: navigator.userAgent,
            overlay: overlayState(),
            compositorNodes: scanCompositorHeavy(),
            animations: scanAnimations(),
            listenerCounts: countListeners(),
            memory: memoryEstimate(),
            scrollLocked: document.documentElement.classList.contains("annap-overlay-open")
                || document.body.classList.contains("guest-scroll-lock")
        };
    }

    global.__ANNAP_RUNTIME_REPORT = function () {
        var r = buildReport();
        try {
            if (global.__ANNAP_DEBUG === true) {
                console.log("[ANNAP RUNTIME REPORT]", r);
            }
        } catch (_) {}
        return r;
    };

    document.addEventListener("annap-drink-detail-opened", function () {
        overlayOpens++;
    });
    document.addEventListener("annap-drink-detail-closed", function () {
        overlayCloses++;
    });
})(typeof window !== "undefined" ? window : globalThis);
