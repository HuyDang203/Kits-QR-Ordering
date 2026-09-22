/**
 * Lightweight drink detail modal — JSON + DrinkDetailRenderer when available;
 * fresh mount/unmount per open; minimal scroll-lock surface.
 *
 * Temperature contract (Step 3.3 hardened):
 *   - Server sends SupportsHotIced + default Temperature in the DTO.
 *   - Renderer paints the toggle only when SupportsHotIced is true.
 *   - Each new drink open resets CURRENT_TEMPERATURE to a safe default
 *     BEFORE fetch ("Hot"). The server payload may refine to "Iced"
 *     when explicitly set; for unsupported drinks the payload has no
 *     temperature, so the in-card CTA dataset is cleared.
 *   - syncTopCta now reads ONLY the payload — never falls back to the
 *     previous drink's CURRENT_TEMPERATURE — preventing leakage from
 *     a previously-toggled Iced drink into an unrelated drink.
 *   - annapAddToCart reads dataset.temperature from the clicked button;
 *     when empty, falls back to the catalog's supportsHotIced rule.
 *   - Pairings never carry temperature (bakery items don't support it).
 */
(function (global) {
    "use strict";

    var BOUND = false;
    var OPEN = false;
    var FETCH_GEN = 0;
    var LAST_OPEN_ID = "";
    var LAST_OPEN_AT = 0;
    var OPEN_GUARD_MS = 420;
    var DETAIL_TIMEOUT_MS = 12000;
    var DETAIL_FETCH = null;
    var CURRENT_TEMPERATURE = "Hot"; // default; updated from server DTO

    function $(id) {
        return document.getElementById(id);
    }

    function nav() {
        return typeof GuestInteractionContract !== "undefined" && GuestInteractionContract
            ? GuestInteractionContract.navigation
            : null;
    }

    function renderer() {
        return typeof global.DrinkDetailRenderer !== "undefined" ? global.DrinkDetailRenderer : null;
    }

    function ddT(path, enFallback) {
        if (typeof global.LuxuryI18n !== "undefined" && typeof global.LuxuryI18n.t === "function") {
            var s = global.LuxuryI18n.t(path);
            if (s && String(s).trim()) return String(s).trim();
        }
        return enFallback;
    }

    function ddTTemp(key, enFallback) {
        if (typeof global.LuxuryI18n !== "undefined" && typeof global.LuxuryI18n.t === "function") {
            var s = global.LuxuryI18n.t(key);
            if (s && String(s).trim()) return String(s).trim();
        }
        return enFallback;
    }

    function lockScroll() {
        global.AnnapScrollLock && global.AnnapScrollLock.lock();
    }

    function unlockScroll() {
        global.AnnapScrollLock && global.AnnapScrollLock.unlock();
    }

    function showRoot(root) {
        root.classList.remove("hidden");
        root.classList.add("is-open");
        root.setAttribute("aria-hidden", "false");
    }

    function hideRoot(root) {
        root.classList.remove("is-open");
        root.classList.add("hidden");
        root.setAttribute("aria-hidden", "true");
    }

    function releaseImages(container) {
        if (!container) return;
        container.querySelectorAll("img").forEach(function (img) {
            img.removeAttribute("srcset");
            img.src = "";
            img.removeAttribute("src");
        });
    }

    function clearMount(mount) {
        if (!mount) return;
        releaseImages(mount);
        var r = renderer();
        if (r && typeof r.unmount === "function") {
            r.unmount(mount);
        } else if (typeof mount.replaceChildren === "function") {
            mount.replaceChildren();
        } else {
            mount.innerHTML = "";
        }
    }

    function wireClose(mount) {
        var root = $("drink-detail-modal");
        var closeBtn = $("drink-detail-close");
        function onClose(e) {
            if (e) e.preventDefault();
            close();
        }
        if (closeBtn && !closeBtn.dataset.annapBound) {
            closeBtn.dataset.annapBound = "1";
            closeBtn.addEventListener("click", onClose);
        }
        if (!mount) return;
        mount.querySelectorAll(".drink-modal-close, .drink-poster-back").forEach(function (btn) {
            if (btn.dataset.annapBound) return;
            btn.dataset.annapBound = "1";
            btn.addEventListener("click", onClose);
        });
    }

    function wireTemperature(mount) {
        if (!mount) return;
        var tempButtons = mount.querySelectorAll("[data-temp]");
        if (!tempButtons.length) return;
        tempButtons.forEach(function (btn) {
            if (btn.dataset.kitsTempBound) return;
            btn.dataset.kitsTempBound = "1";
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                var temp = btn.getAttribute("data-temp");
                if (!temp) return;
                CURRENT_TEMPERATURE = temp;
                // Update aria-pressed on all temp buttons within mount
                tempButtons.forEach(function (b) {
                    var isActive = b === btn;
                    b.classList.toggle("dd-card__temp-btn--active", isActive);
                    b.setAttribute("aria-pressed", isActive ? "true" : "false");
                });
                // Sync temperature to all [data-detail-add] buttons within mount
                mount.querySelectorAll("[data-detail-add]").forEach(function (ctaBtn) {
                    ctaBtn.setAttribute("data-temperature", temp);
                });
                // Sync to top action-bar CTA
                var topBtn = $("drink-detail-add-top");
                if (topBtn) topBtn.setAttribute("data-temperature", temp);
            });
        });
    }

    function wireDetailAdd(mount) {
        if (!mount) return;
        var article = mount.querySelector(".dd-product-card,[data-drink-id]");
        var drinkId = article ? article.getAttribute("data-drink-id") : "";
        mount.querySelectorAll("[data-detail-add]").forEach(function (btn) {
            if (btn.dataset.annapBound) return;
            btn.dataset.annapBound = "1";
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                if (!drinkId || typeof global.annapAddToCart !== "function") return;
                var temp = btn.getAttribute("data-temperature") || CURRENT_TEMPERATURE || "Hot";
                // Pass temperature as second arg for cart item metadata
                global.annapAddToCart(drinkId, btn, temp);
            });
        });
    }

    function syncTopCta(data) {
        var btn = $("drink-detail-add-top");
        var r = renderer();
        if (!btn) return;
        var d = r && typeof r.normalize === "function" ? r.normalize(data) : data || {};
        var disabled = d.canAdd === false;
        btn.hidden = false;
        btn.disabled = disabled;
        btn.classList.toggle("drink-detail-add-top--disabled", disabled);
        btn.setAttribute("data-item-name", d.name || "");
        btn.setAttribute("data-item-price", d.price != null ? String(d.price) : "0");
        btn.setAttribute("data-drink-id", String(d.id || ""));
        // Payload-derived temperature only; never inherit a previous drink's toggle.
        // For unsupported drinks, the payload has no temperature → leave cleared.
        if (d.supportsHotIced && d.temperature) {
            btn.setAttribute("data-temperature", d.temperature);
            CURRENT_TEMPERATURE = d.temperature;
        } else {
            btn.removeAttribute("data-temperature");
        }
    }

    function resetTopCta() {
        var btn = $("drink-detail-add-top");
        if (!btn) return;
        btn.hidden = true;
        btn.disabled = true;
        btn.classList.remove("drink-detail-add-top--disabled");
        btn.removeAttribute("data-drink-id");
        btn.removeAttribute("data-temperature");
    }

    function wireTopAdd() {
        var btn = $("drink-detail-add-top");
        if (!btn || btn.dataset.annapBound) return;
        btn.dataset.annapBound = "1";
        btn.addEventListener("click", function (e) {
            e.preventDefault();
            if (btn.disabled) return;
            var drinkId = btn.getAttribute("data-drink-id");
            if (!drinkId || typeof global.annapAddToCart !== "function") return;
            var temp = btn.getAttribute("data-temperature") || CURRENT_TEMPERATURE || "Hot";
            global.annapAddToCart(drinkId, btn, temp);
        });
    }

    function wirePairings(mount) {
        if (!mount) return;
        mount.querySelectorAll("[data-pairing-add]").forEach(function (btn) {
            if (btn.dataset.annapBound) return;
            btn.dataset.annapBound = "1";
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                var id = btn.getAttribute("data-pairing-id");
                if (!id || typeof global.annapAddToCart !== "function") return;
                global.annapAddToCart(id, btn);
            });
        });
    }

    function close() {
        var root = $("drink-detail-modal");
        var mount = $("drink-detail-mount");
        if (!OPEN || !root) return;
        OPEN = false;
        FETCH_GEN += 1;
        // Empty (not "Hot") — so an immediately-following open of an
        // unsupported drink does not silently inherit a stale value.
        CURRENT_TEMPERATURE = "";
        if (DETAIL_FETCH) {
            try {
                DETAIL_FETCH.abort();
            } catch (_ab) {}
            DETAIL_FETCH = null;
        }
        clearMount(mount);
        resetTopCta();
        hideRoot(root);
        unlockScroll();
        document.body.classList.remove("drink-detail-open");
        try {
            document.dispatchEvent(new CustomEvent("annap-drink-detail-closed"));
        } catch (_ev) {}
    }

    async function openWithJson(mount, n, drinkId, gen, options) {
        options = options || {};
        var url =
            typeof n.drinkDetailDataUrl === "function"
                ? n.drinkDetailDataUrl(drinkId)
                : null;
        var r = renderer();
        if (!url || !r) return false;

        if (DETAIL_FETCH) {
            try {
                DETAIL_FETCH.abort();
            } catch (_ab0) {}
        }
        DETAIL_FETCH = typeof AbortController !== "undefined" ? new AbortController() : null;
        var requestController = DETAIL_FETCH;
        var fetchOpts = { headers: { Accept: "application/json" } };
        if (requestController) fetchOpts.signal = requestController.signal;
        var timeoutId = requestController
            ? global.setTimeout(function () {
                  try {
                      requestController.abort();
                  } catch (_ab1) {}
              }, DETAIL_TIMEOUT_MS)
            : null;

        var res;
        try {
            res = await fetch(url, fetchOpts);
        } catch (err) {
            if (err && err.name === "AbortError") {
                if (gen === FETCH_GEN && OPEN) {
                    mount.replaceChildren();
                    var soft = document.createElement("p");
                    soft.className = "dd-loading";
                    soft.textContent = ddT(
                        "drink.loading",
                        "Loading…"
                    );
                    mount.appendChild(soft);
                }
                return true;
            }
            throw err;
        } finally {
            if (timeoutId) global.clearTimeout(timeoutId);
            if (DETAIL_FETCH === requestController) DETAIL_FETCH = null;
        }
        if (gen !== FETCH_GEN || !OPEN) return true;
        if (!res.ok) {
            mount.replaceChildren();
            var err = document.createElement("p");
            err.className = "dd-loading";
            err.textContent = ddT(
                "drink.unavailable",
                "Unavailable right now."
            );
            mount.appendChild(err);
            return true;
        }
        var data = await res.json();
        if (gen !== FETCH_GEN || !OPEN) return true;
        // Temperature lifecycle reset:
        // For SupportsHotIced === false, do NOT carry CURRENT_TEMPERATURE forward.
        // The renderer will not paint a toggle, and the top CTA dataset is cleared.
        if (data && data.supportsHotIced) {
            CURRENT_TEMPERATURE = data.temperature || "Hot";
        } else {
            CURRENT_TEMPERATURE = "";
        }
        r.mount(mount, data);
        syncTopCta(data);
        if (typeof global.LuxuryI18n !== "undefined" && global.LuxuryI18n.applyDom) {
            global.LuxuryI18n.applyDom(mount);
        }
        wireClose(mount);
        wireDetailAdd(mount);
        wirePairings(mount);
        wireTemperature(mount);
        return true;
    }

    async function open(id, options) {
        options = options || {};
        var root = $("drink-detail-modal");
        var mount = $("drink-detail-mount");
        var n = nav();
        var r = renderer();
        if (!root || !mount || !n || !r) return;
        if (typeof n.drinkDetailDataUrl !== "function") return;

        var drinkId = String(id || "").trim();
        if (!drinkId) return;

        var now = Date.now();
        if (OPEN && drinkId === LAST_OPEN_ID) return;
        if (drinkId === LAST_OPEN_ID && now - LAST_OPEN_AT < OPEN_GUARD_MS) return;
        LAST_OPEN_ID = drinkId;
        LAST_OPEN_AT = now;

        if (OPEN) close();

        // Reset module-level temperature state for this new drink open.
        // Prevents leakage from a previously-toggled Iced drink into this one.
        // The actual payload temperature is set after fetch resolves.
        CURRENT_TEMPERATURE = "";
        resetTopCta();

        FETCH_GEN += 1;
        var gen = FETCH_GEN;
        OPEN = true;

        lockScroll();
        document.body.classList.add("drink-detail-open");
        clearMount(mount);
        var loading = document.createElement("p");
        loading.className = "dd-loading";
        loading.textContent = ddT(
            "drink.loading",
            "Loading…"
        );
        mount.appendChild(loading);
        showRoot(root);
        try {
            document.dispatchEvent(new CustomEvent("annap-drink-detail-opened", { detail: { id: drinkId } }));
        } catch (_ev2) {}

        try {
            await openWithJson(mount, n, drinkId, gen, options);
        } catch (_err) {
            if (gen !== FETCH_GEN || !OPEN) return;
            clearMount(mount);
            var fail = document.createElement("p");
            fail.className = "dd-loading";
            fail.textContent = ddT(
                "drink.unavailable",
                "Unavailable right now."
            );
            mount.appendChild(fail);
        }
    }

    function bindOnce() {
        if (BOUND) return;
        BOUND = true;

        var root = $("drink-detail-modal");
        var backdrop = $("drink-detail-backdrop");

        wireClose(null);
        wireTopAdd();

        if (backdrop) {
            backdrop.addEventListener("click", function (e) {
                e.preventDefault();
                close();
            });
        }
        if (root) {
            root.addEventListener("click", function (e) {
                if (e.target === root) close();
            });
        }

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && OPEN) {
                e.preventDefault();
                close();
            }
        });

        global.addEventListener("pagehide", function () {
            if (OPEN) close();
        });

        global.addEventListener("pageshow", function (ev) {
            if (!ev.persisted) return;
            if (OPEN) close();
            if (global.AnnapScrollLock) global.AnnapScrollLock.reset();
        });

        global.addEventListener("popstate", function () {
            if (OPEN) close();
        });

        document.addEventListener("visibilitychange", function () {
            if (document.visibilityState === "hidden") {
                return;
            }
            if (OPEN) {
                var root = $("drink-detail-modal");
                if (root && root.classList.contains("hidden")) {
                    close();
                }
                return;
            }
            if (global.AnnapScrollLock) global.AnnapScrollLock.reset();
        });
    }

    function init() {
        bindOnce();
    }

    function isOpen() {
        return OPEN;
    }

    global.DrinkDetailModal = {
        init: init,
        open: open,
        close: close,
        isOpen: isOpen
    };

    if ($("drink-detail-modal")) init();
})(typeof window !== "undefined" ? window : globalThis);
