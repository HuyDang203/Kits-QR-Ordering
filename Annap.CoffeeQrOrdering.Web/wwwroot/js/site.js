(function () {
    "use strict";

    function readCssPx(name) {
        const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        const n = parseFloat(raw);
        return Number.isFinite(n) ? n : 0;
    }

    /* --vv-h, --vv-w, offsets: owned by annap-viewport.js (VisualViewport + in-app stability). */

    /* When the on-screen keyboard shrinks the visual viewport, lift fixed footers slightly (guest cart / toasts). */
    let keyboardRaf = 0;

    function syncKeyboardInset() {
        const vv = window.visualViewport;
        if (vv && vv.scale > 1.02) return;
        if (document.documentElement.classList.contains("annap-overlay-open")) return;
        const layoutH = window.innerHeight;
        const visible = vv && vv.height > 0 ? vv.height : layoutH;
        const inset = Math.max(0, Math.round(layoutH - visible - readCssPx("--vv-keyboard-ignore")));
        document.documentElement.style.setProperty("--keyboard-inset", `${inset}px`);
    }

    function requestKeyboardInset() {
        if (keyboardRaf) return;
        keyboardRaf = window.requestAnimationFrame(function () {
            keyboardRaf = 0;
            syncKeyboardInset();
        });
    }

    document.documentElement.style.setProperty("--vv-keyboard-ignore", "0px");
    requestKeyboardInset();
    window.addEventListener("resize", requestKeyboardInset, { passive: true });
    if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", requestKeyboardInset, { passive: true });
        window.visualViewport.addEventListener("scroll", requestKeyboardInset, { passive: true });
    }

    /* Keep focused fields visible above the iOS keyboard (no layout system change). */
    document.addEventListener(
        "focusin",
        (e) => {
            const t = e.target;
            if (!(t instanceof HTMLElement)) return;
            const tag = t.tagName;
            if (tag !== "INPUT" && tag !== "TEXTAREA" && tag !== "SELECT") return;
            window.requestAnimationFrame(() => {
                try {
                    t.scrollIntoView({ block: "center", inline: "nearest", behavior: "auto" });
                } catch (_annap) {
                    /* ignore */
                }
            });
        },
        true
    );
    window.setTimeout(function () {
        if (document.getElementById("annap-lan-debug") && window.AnnapGuestLanDebug && typeof AnnapGuestLanDebug.mark === "function") {
            AnnapGuestLanDebug.mark("site-js-loaded");
        }
    }, 0);
})();
