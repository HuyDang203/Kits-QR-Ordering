/**
 * ANNAP persistent ambient layer — slow gradient focal drift only.
 * Updates --annap-ambient-x / --annap-ambient-y on #annap-root (no layout reads, no animated blur/shadow).
 */
(function (global) {
    "use strict";

    var root = null;
    var rafId = 0;
    var running = false;
    var lastApplyMs = 0;
    /** Max ~8 Hz style writes — weak devices stay smooth; drift remains imperceptible. */
    var MIN_MS_BETWEEN_APPLIES = 125;
    /** Full Lissajous-style loop period (ms); doctrine ~45s room “breath”. */
    var CYCLE_MS = 45000;
    /** Mode atmosphere tempo (solo slower, adventurous slightly quicker). */
    var ambientTempo = 1;

    function readAmbientTempo() {
        try {
            var raw = global.getComputedStyle(global.document.documentElement).getPropertyValue("--annap-ambient-tempo").trim();
            var n = parseFloat(raw);
            if (n > 0.25 && n < 4) return n;
        } catch (_t) {
            /* ignore */
        }
        return 1;
    }

    function onAtmosphereCommitted() {
        ambientTempo = readAmbientTempo();
    }

    function motionAllowed() {
        try {
            if (global.document.documentElement.classList.contains("annap-guest-no-motion")) return false;
            if (global.matchMedia && global.matchMedia("(prefers-reduced-motion: reduce)").matches) return false;
        } catch (_e) {
            return false;
        }
        return true;
    }

    function applyFocal(nowMs) {
        if (!root) return;
        var cycleMs = CYCLE_MS * ambientTempo;
        var t = (nowMs % cycleMs) / cycleMs;
        var a = t * Math.PI * 2;
        /* ±1.2% around center — sub-pixel feel, minimal repaint area change */
        var x = 50 + 1.2 * Math.sin(a);
        var y = 40 + 0.9 * Math.sin(a * 0.73 + 0.4);
        root.style.setProperty("--annap-ambient-x", x.toFixed(2) + "%");
        root.style.setProperty("--annap-ambient-y", y.toFixed(2) + "%");
    }

    function tick(nowMs) {
        if (!running || !root) return;
        var minGap = MIN_MS_BETWEEN_APPLIES * Math.max(0.65, ambientTempo);
        if (nowMs - lastApplyMs >= minGap) {
            lastApplyMs = nowMs;
            applyFocal(nowMs);
        }
        rafId = global.requestAnimationFrame(tick);
    }

    function start() {
        if (!root || !motionAllowed()) return;
        if (running) return;
        running = true;
        lastApplyMs = 0;
        rafId = global.requestAnimationFrame(tick);
    }

    function stop() {
        running = false;
        if (rafId) {
            global.cancelAnimationFrame(rafId);
            rafId = 0;
        }
    }

    function onVisibility() {
        try {
            if (global.document.hidden) stop();
            else if (motionAllowed()) start();
        } catch (_e) {
            /* ignore */
        }
    }

    function boot() {
        root = global.document.getElementById("annap-root");
        if (!root) return;
        ambientTempo = readAmbientTempo();
        global.addEventListener("annap-atmosphere-committed", onAtmosphereCommitted, { passive: true });
        if (!motionAllowed()) return;
        start();
        global.document.addEventListener("visibilitychange", onVisibility, false);
        global.addEventListener("pagehide", stop, false);
        global.addEventListener("pageshow", onVisibility, false);
    }

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", boot, { once: true });
    } else {
        boot();
    }
})(typeof window !== "undefined" ? window : globalThis);
