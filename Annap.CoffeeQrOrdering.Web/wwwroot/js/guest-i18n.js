/**
 * Lightweight luxury guest copy (EN/VI). Bundles load once; switching is instant.
 */
(function () {
    const STORAGE_KEY = "annap_guest_lang";
    /** Bump when SharedResources.resx copy changes so clients refetch bundles. */
    const I18N_BUNDLE_REV = "2026070713";
    const bundles = { en: null, vi: null };
    let current = "en";

    const I18N_FETCH_MS = 15000;

    function i18nDevOn() {
        var adb = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
        return window.__ANNAP_DEBUG === true || adb.showBootChecklist === true;
    }
    function i18nInfo() {
        if (!i18nDevOn()) return;
        try {
            console.info.apply(console, arguments);
        } catch (_e) {}
    }
    function i18nErr() {
        if (!i18nDevOn()) return;
        try {
            console.error.apply(console, arguments);
        } catch (_e2) {}
    }

    async function fetchBundleJson(lang) {
        const ac = new AbortController();
        const t = setTimeout(function () {
            ac.abort();
        }, I18N_FETCH_MS);
        try {
            var rev = I18N_BUNDLE_REV;
            try {
                var tag = document.querySelector('script[src*="guest-i18n.js"]');
                if (tag) {
                    var src = tag.getAttribute("src") || "";
                    var m = src.match(/[?&]v=([^&]+)/);
                    if (m) rev = rev + "-" + m[1];
                }
            } catch (_rev) {
                /* ignore */
            }
            var url = "/i18n/" + lang + ".json?v=" + encodeURIComponent(rev);
            return await fetch(url, { cache: "no-store", signal: ac.signal });
        } finally {
            clearTimeout(t);
        }
    }

    const loadPromise = (async () => {
        const t0 = typeof performance !== "undefined" && performance.now ? performance.now() : Date.now();
        const [rEn, rVi] = await Promise.all([fetchBundleJson("en"), fetchBundleJson("vi")]);
        if (!rEn.ok || !rVi.ok) throw new Error("i18n load failed");
        bundles.en = await rEn.json();
        bundles.vi = await rVi.json();
        try {
            current = normalize(localStorage.getItem(STORAGE_KEY) || "vi");
        } catch (_annap) {
            current = "vi";
        }
        document.documentElement.lang = current === "vi" ? "vi" : "en";
        syncCultureCookie(current);
        const t1 = typeof performance !== "undefined" && performance.now ? performance.now() : Date.now();
        i18nInfo("[annap] i18n bundles loaded", { ms: Math.round(t1 - t0) });
    })();

    function normalize(lang) {
        return lang && String(lang).trim().toLowerCase() === "vi" ? "vi" : "en";
    }

    function getLang() {
        return current;
    }

    function syncCultureCookie(lang) {
        try {
            var culture = lang === "vi" ? "vi-VN" : "en-US";
            var val = "c=" + culture + "|uic=" + culture;
            document.cookie = ".AspNetCore.Culture=" + encodeURIComponent(val) + ";path=/;max-age=31536000;SameSite=Lax";
        } catch (_cookie) {
            /* ignore */
        }
    }

    function setLang(lang) {
        const next = normalize(lang);
        if (next === current) return;
        current = next;
        try {
            localStorage.setItem(STORAGE_KEY, current);
        } catch (_annapLs) {
            /* ignore */
        }
        document.documentElement.lang = current === "vi" ? "vi" : "en";
        syncCultureCookie(current);
        try {
            document.documentElement.classList.add("annap-if-lang-fading");
        } catch (_fade) {}
        applyDom();
        try {
            sessionStorage.removeItem("annap_sommelier_session_v1");
        } catch (_annapSs) {
            /* ignore */
        }
        if (window.InteractionFeedback && typeof window.InteractionFeedback.trigger === "function") {
            window.InteractionFeedback.trigger("lang", { silentVisual: true });
        }
        window.dispatchEvent(new CustomEvent("luxury:i18n-changed", { detail: { lang: current } }));
        syncSwitcherUi();
        setTimeout(function () {
            try {
                document.documentElement.classList.remove("annap-if-lang-fading");
            } catch (_fade2) {}
        }, 120);
    }

    function get(path) {
        const b = bundles[current];
        if (!b || !path) return "";
        const parts = String(path).split(".");
        let o = b;
        for (const p of parts) {
            if (o == null || typeof o !== "object") return "";
            o = o[p];
        }
        return o == null ? "" : String(o);
    }

    /** Replace `{key}` placeholders in a bundle string (e.g. `{n}`). */
    function tf(path, vars) {
        let s = get(path);
        if (!s || !vars || typeof vars !== "object") return s;
        for (const k of Object.keys(vars)) {
            s = s.split(`{${k}}`).join(String(vars[k]));
        }
        return s;
    }

    function applyDom() {
        document.querySelectorAll("[data-i18n]").forEach((el) => {
            const path = el.getAttribute("data-i18n");
            const v = get(path);
            if (v) el.textContent = v;
        });
        document.querySelectorAll("[data-i18n-placeholder]").forEach((el) => {
            const path = el.getAttribute("data-i18n-placeholder");
            const v = get(path);
            if (v) el.setAttribute("placeholder", v);
        });
        document.querySelectorAll("[data-i18n-aria]").forEach((el) => {
            const path = el.getAttribute("data-i18n-aria");
            const v = get(path);
            if (v) el.setAttribute("aria-label", v);
        });
    }

    function syncSwitcherUi() {
        document.querySelectorAll("[data-lang-set]").forEach((btn) => {
            const l = btn.getAttribute("data-lang-set");
            const on = l === current;
            btn.setAttribute("aria-pressed", on ? "true" : "false");
            btn.classList.toggle("ring-1", on);
            btn.classList.toggle("ring-white/25", on);
            btn.classList.toggle("text-[rgb(var(--fg))]", on);
            btn.classList.toggle("text-[rgb(var(--muted))]", !on);
        });
    }

    function wireSwitcher() {
        document.querySelectorAll("[data-lang-set]").forEach((btn) => {
            btn.addEventListener("click", () => setLang(btn.getAttribute("data-lang-set")));
        });
        syncSwitcherUi();
    }

    window.LuxuryI18n = {
        ready: loadPromise,
        getLang,
        setLang,
        t: get,
        tf,
        applyDom,
        wireSwitcher
    };
    window.AnnapI18n = window.LuxuryI18n;

    loadPromise
        .then(() => {
            applyDom();
            wireSwitcher();
        })
        .catch((e) => {
            i18nErr("[annap] i18n load failed", e);
            /* non-fatal; English DOM stays */
        });
})();
