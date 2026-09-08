/**
 * Annap Interaction Feedback — single entry point for guest tactile UX.
 * Visual (CSS) + Web Audio + haptic. Staff/admin must not load this file.
 */
(function (global) {
    "use strict";

    var STORAGE_SOUND = "annap_if_sound";
    var STORAGE_HAPTIC = "annap_if_haptic";
    var FLASH_MS = 110;
    var PRESS_THROTTLE_MS = 90;
    var KIND_THROTTLE_MS = Object.create(null);

    var ctx = null;
    var unlocked = false;
    var wired = false;
    var lastPressEl = null;
    var lastPressAt = 0;

    var prefs = {
        sound: readBool(STORAGE_SOUND, true),
        haptic: readBool(STORAGE_HAPTIC, true)
    };

    function readBool(key, fallback) {
        try {
            var v = localStorage.getItem(key);
            if (v === null || v === undefined) return fallback;
            return v === "1" || v === "true";
        } catch (_e) {
            return fallback;
        }
    }

    function writeBool(key, value) {
        try {
            localStorage.setItem(key, value ? "1" : "0");
        } catch (_e) {
            /* ignore */
        }
    }

    function reducedMotion() {
        try {
            if (document.documentElement.classList.contains("annap-guest-no-motion")) return true;
            return (
                typeof matchMedia === "function" &&
                matchMedia("(prefers-reduced-motion: reduce)").matches
            );
        } catch (_e) {
            return false;
        }
    }

    function ensureContext() {
        if (ctx) return ctx;
        var AC = global.AudioContext || global.webkitAudioContext;
        if (!AC) return null;
        ctx = new AC();
        return ctx;
    }

    function unlock() {
        if (unlocked) return;
        unlocked = true;
        var c = ensureContext();
        if (!c) return;
        if (c.state === "suspended") {
            c.resume().catch(function () {
                /* ignore autoplay policy */
            });
        }
    }

    function now() {
        return (ctx && ctx.currentTime) || 0;
    }

    function tone(freq, dur, type, peak, when) {
        var c = ensureContext();
        if (!c || c.state !== "running") return;
        var t0 = when != null ? when : c.currentTime;
        var osc = c.createOscillator();
        var g = c.createGain();
        var vol = reducedMotion() ? peak * 0.35 : peak;
        osc.type = type || "sine";
        osc.frequency.setValueAtTime(freq, t0);
        g.gain.setValueAtTime(0.0001, t0);
        g.gain.exponentialRampToValueAtTime(Math.max(0.0002, vol), t0 + 0.006);
        g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
        osc.connect(g);
        g.connect(c.destination);
        osc.start(t0);
        osc.stop(t0 + dur + 0.03);
    }

    function sweep(f0, f1, dur, peak) {
        var c = ensureContext();
        if (!c || c.state !== "running") return;
        var t0 = c.currentTime;
        var osc = c.createOscillator();
        var g = c.createGain();
        var vol = reducedMotion() ? peak * 0.3 : peak;
        osc.type = "sine";
        osc.frequency.setValueAtTime(f0, t0);
        osc.frequency.exponentialRampToValueAtTime(Math.max(40, f1), t0 + dur);
        g.gain.setValueAtTime(0.0001, t0);
        g.gain.exponentialRampToValueAtTime(Math.max(0.0002, vol), t0 + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
        osc.connect(g);
        g.connect(c.destination);
        osc.start(t0);
        osc.stop(t0 + dur + 0.04);
    }

    var SOUND = {
        click: function () {
            tone(1850, 0.022, "sine", 0.028);
        },
        add: function () {
            var t = now();
            tone(390, 0.055, "triangle", 0.04, t);
            tone(620, 0.04, "sine", 0.022, t + 0.012);
        },
        whoosh: function () {
            sweep(720, 180, 0.14, 0.03);
        },
        submit: function () {
            var t = now();
            tone(520, 0.07, "sine", 0.035, t);
            tone(780, 0.09, "sine", 0.028, t + 0.06);
        },
        success: function () {
            var t = now();
            tone(660, 0.08, "sine", 0.032, t);
            tone(990, 0.1, "sine", 0.026, t + 0.07);
            tone(1320, 0.14, "sine", 0.018, t + 0.14);
        },
        complete: function () {
            var t = now();
            tone(880, 0.05, "sine", 0.03, t);
            tone(1760, 0.12, "triangle", 0.016, t + 0.04);
        },
        error: function () {
            tone(210, 0.045, "triangle", 0.022);
        },
        lang: function () {
            tone(1400, 0.028, "sine", 0.024);
        }
    };

    var HAPTIC = {
        click: [8],
        add: [10],
        whoosh: [6],
        submit: [12],
        success: [12],
        complete: [8, 30, 10],
        error: [20],
        lang: [8]
    };

    function throttleKind(kind) {
        var t = Date.now();
        var last = KIND_THROTTLE_MS[kind] || 0;
        if (t - last < 70) return true;
        KIND_THROTTLE_MS[kind] = t;
        return false;
    }

    function playSound(kind) {
        if (!prefs.sound) return;
        unlock();
        var c = ensureContext();
        if (!c) return;
        if (c.state === "suspended") {
            c.resume()
                .then(function () {
                    var fn = SOUND[kind];
                    if (fn) fn();
                })
                .catch(function () {});
            return;
        }
        var fn = SOUND[kind];
        if (fn) fn();
    }

    function playHaptic(kind) {
        if (!prefs.haptic || reducedMotion()) return;
        if (typeof navigator === "undefined" || typeof navigator.vibrate !== "function") return;
        try {
            var pattern = HAPTIC[kind] || HAPTIC.click;
            navigator.vibrate(pattern);
        } catch (_e) {
            /* unsupported */
        }
    }

    function flash(el) {
        if (!el || !el.classList || reducedMotion()) return;
        el.classList.remove("if-flash");
        void el.offsetWidth;
        el.classList.add("if-flash");
        clearTimeout(el.__ifFlashTimer);
        el.__ifFlashTimer = setTimeout(function () {
            el.classList.remove("if-flash");
        }, FLASH_MS);
    }

    /**
     * @param {string} kind click|add|whoosh|submit|success|complete|error|lang
     * @param {{ element?: Element, silentVisual?: boolean }=} opts
     */
    function trigger(kind, opts) {
        var k = kind && SOUND[kind] ? kind : "click";
        if (throttleKind(k)) return;
        opts = opts || {};
        unlock();
        if (!opts.silentVisual && opts.element) flash(opts.element);
        playSound(k);
        playHaptic(k);
    }

    function inferKind(el) {
        var explicit = el.getAttribute("data-feedback");
        if (explicit && SOUND[explicit]) return explicit;
        if (el.hasAttribute("data-lang-set")) return "lang";
        if (el.classList.contains("menu-add-btn") || el.hasAttribute("data-ge-add") || el.hasAttribute("data-item-name"))
            return "add";
        if (el.classList.contains("order-tray-payment-option")) return "click";
        if (el.id === "order-tray-submit" || el.classList.contains("order-tray-submit")) return "submit";
        if (el.classList.contains("menu-editorial-card") || el.classList.contains("ge-sig-card")) return "click";
        return "click";
    }

    function isInteractiveTarget(el) {
        if (!el || el.nodeType !== 1) return null;
        if (el.closest("[data-feedback=none], .annap-if-ignore")) return null;
        var hit = el.closest(
            "[data-feedback]:not([data-feedback=none]), .guest-hit, .btn-ink, .btn-quiet, .menu-editorial-card, .menu-add-btn, .order-tray-payment-option, .ge-sig-card, [data-lang-set]"
        );
        if (!hit) return null;
        if (hit.tagName === "INPUT" || hit.tagName === "TEXTAREA" || hit.tagName === "SELECT") return null;
        return hit;
    }

    function onPointerDown(e) {
        if (e.button != null && e.button !== 0) return;
        var el = isInteractiveTarget(e.target);
        if (!el) return;
        var t = Date.now();
        if (el === lastPressEl && t - lastPressAt < PRESS_THROTTLE_MS) return;
        lastPressEl = el;
        lastPressAt = t;
        unlock();
        flash(el);
        var kind = inferKind(el);
        /* Language: sound plays once from LuxuryI18n.setLang after DOM apply */
        if (kind === "lang") {
            playHaptic("lang");
            return;
        }
        /* Semantic confirmations fire from app hooks — press stays a soft tick */
        if (kind === "add" || kind === "submit" || kind === "success" || kind === "complete") kind = "click";
        playSound(kind);
        playHaptic(kind);
    }

    function onFirstGesture() {
        unlock();
        global.removeEventListener("pointerdown", onFirstGesture, true);
        global.removeEventListener("touchstart", onFirstGesture, true);
        global.removeEventListener("keydown", onFirstGesture, true);
    }

    function syncPrefUi() {
        document.querySelectorAll("[data-if-pref]").forEach(function (btn) {
            var key = btn.getAttribute("data-if-pref");
            var on = key === "sound" ? prefs.sound : key === "haptic" ? prefs.haptic : false;
            btn.setAttribute("aria-pressed", on ? "true" : "false");
            btn.classList.toggle("annap-if-pref--on", on);
            btn.classList.toggle("annap-if-pref--off", !on);
        });
    }

    function setSoundEnabled(on) {
        prefs.sound = !!on;
        writeBool(STORAGE_SOUND, prefs.sound);
        syncPrefUi();
        if (prefs.sound) {
            unlock();
            trigger("click", { silentVisual: true });
        }
    }

    function setHapticEnabled(on) {
        prefs.haptic = !!on;
        writeBool(STORAGE_HAPTIC, prefs.haptic);
        syncPrefUi();
        if (prefs.haptic) trigger("click", { silentVisual: true });
    }

    function onPrefClick(e) {
        var btn = e.target.closest("[data-if-pref]");
        if (!btn) return;
        e.preventDefault();
        var key = btn.getAttribute("data-if-pref");
        if (key === "sound") setSoundEnabled(!prefs.sound);
        else if (key === "haptic") setHapticEnabled(!prefs.haptic);
    }

    function onGuestInteraction(e) {
        var d = (e && e.detail) || {};
        if (d.type === "itemAdded") {
            trigger("add", { element: d.sourceElement || null, silentVisual: !d.sourceElement });
        }
    }

    function onI18nChanged() {
        /* Soft fade already applied in LuxuryI18n.setLang; avoid double sound */
        syncPrefUi();
    }

    function wire() {
        if (wired) return;
        wired = true;
        document.addEventListener("pointerdown", onPointerDown, { capture: true, passive: true });
        document.addEventListener("click", onPrefClick, false);
        document.addEventListener("annap:guest-interaction", onGuestInteraction);
        global.addEventListener("luxury:i18n-changed", onI18nChanged);
        global.addEventListener("pointerdown", onFirstGesture, true);
        global.addEventListener("touchstart", onFirstGesture, { capture: true, passive: true });
        global.addEventListener("keydown", onFirstGesture, true);
        syncPrefUi();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", wire);
    } else {
        wire();
    }

    global.InteractionFeedback = {
        trigger: trigger,
        unlock: unlock,
        setSoundEnabled: setSoundEnabled,
        setHapticEnabled: setHapticEnabled,
        isSoundEnabled: function () {
            return prefs.sound;
        },
        isHapticEnabled: function () {
            return prefs.haptic;
        },
        syncPrefUi: syncPrefUi,
        reducedMotion: reducedMotion
    };

    /* SoundService alias — same singleton */
    global.AnnapSound = {
        play: function (kind) {
            trigger(kind, { silentVisual: true });
        },
        unlock: unlock,
        setEnabled: setSoundEnabled,
        isEnabled: function () {
            return prefs.sound;
        }
    };
})(typeof window !== "undefined" ? window : this);
