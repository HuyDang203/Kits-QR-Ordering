/**
 * ANNAP AtmosphereManager — session-persistent mode thermal + ambient tempo.
 * Doctrine Phase 3: CSS variables on documentElement; sessionStorage.
 * Maps guest flows: sommelier → solo, group → group, discovery → adventurous.
 */
(function (global) {
    "use strict";

    var STORAGE_KEY = "annap_atmosphere_mode_v1";

    /** @type {Record<string, { r: number; g: number; b: number; strength: number; tempo: number }>} */
    var ATMOSPHERES = {
        solo: {
            r: 58,
            g: 64,
            b: 72,
            strength: 0.48,
            tempo: 1.15
        },
        group: {
            r: 154,
            g: 98,
            b: 50,
            strength: 0.52,
            tempo: 1.0
        },
        adventurous: {
            r: 154,
            g: 108,
            b: 48,
            strength: 0.18,
            tempo: 0.92
        }
    };

    var FLOW_TO_MODE = {
        sommelier: "solo",
        group: "group",
        discovery: "adventurous"
    };

    var doc = global.document;
    var html = doc.documentElement;

    function normalizeMode(mode) {
        if (!mode || typeof mode !== "string") return null;
        var m = String(mode).toLowerCase();
        if (m === "solo" || m === "group" || m === "adventurous") return m;
        return null;
    }

    function mapGuestFlow(flowName) {
        if (!flowName || typeof flowName !== "string") return null;
        return FLOW_TO_MODE[String(flowName).toLowerCase()] || null;
    }

    function applyTokens(mode, spec) {
        html.setAttribute("data-annap-atmosphere", mode);
        html.style.setProperty("--thermal-tint-r", String(spec.r));
        html.style.setProperty("--thermal-tint-g", String(spec.g));
        html.style.setProperty("--thermal-tint-b", String(spec.b));
        html.style.setProperty("--thermal-strength", String(spec.strength));
        html.style.setProperty("--annap-ambient-tempo", String(spec.tempo));
    }

    function persist(mode) {
        try {
            global.sessionStorage.setItem(STORAGE_KEY, mode);
        } catch (_e) {
            /* private mode / quota */
        }
    }

    function readStored() {
        try {
            return global.sessionStorage.getItem(STORAGE_KEY);
        } catch (_e2) {
            return null;
        }
    }

    function commit(mode, opts) {
        var m = normalizeMode(mode);
        if (!m) return false;
        var spec = ATMOSPHERES[m];
        if (!spec) return false;
        applyTokens(m, spec);
        if (!opts || !opts.skipStorage) persist(m);
        try {
            global.dispatchEvent(
                new CustomEvent("annap-atmosphere-committed", {
                    detail: { mode: m, tempo: spec.tempo }
                })
            );
        } catch (_ev) {
            /* ignore */
        }
        return true;
    }

    function clearSession() {
        try {
            global.sessionStorage.removeItem(STORAGE_KEY);
        } catch (_c) {
            /* ignore */
        }
        html.removeAttribute("data-annap-atmosphere");
        html.style.removeProperty("--thermal-tint-r");
        html.style.removeProperty("--thermal-tint-g");
        html.style.removeProperty("--thermal-tint-b");
        html.style.removeProperty("--thermal-strength");
        html.style.removeProperty("--annap-ambient-tempo");
    }

    function restore() {
        var raw = readStored();
        var m = normalizeMode(raw);
        if (!m) return false;
        return commit(m, { skipStorage: true });
    }

    function getMode() {
        return normalizeMode(html.getAttribute("data-annap-atmosphere")) || normalizeMode(readStored());
    }

    global.AnnapAtmosphereManager = {
        commit: commit,
        restore: restore,
        clearSession: clearSession,
        getMode: getMode,
        mapGuestFlow: mapGuestFlow,
        /** @deprecated use mapGuestFlow */
        mapFlowToMode: mapGuestFlow
    };

    restore();
})(typeof window !== "undefined" ? window : globalThis);
