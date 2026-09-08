/**
 * DrinkDetailRenderer — Kits product-card detail (dd-* markup only).
 *
 * Guest-visible information hierarchy:
 *   Back/close controls  ← action bar (protected, not rendered here)
 *   Product image
 *   Category (eyebrow)
 *   Product name
 *   Price
 *   Short subtitle / tasting description
 *   Long description (if present)
 *   Hot / Iced selection (when supported by catalog contract)
 *   Pairing suggestions (when available)
 *   Add to Order CTA
 *
 * Preserved contracts (DO NOT rename without coordinated change):
 *   - public API:  render(raw), mount(el, raw), unmount(el), normalize(raw)
 *   - data attrs:  data-drink-id, data-detail-add, data-pairing-add,
 *                  data-pairing-id, data-item-name, data-item-price
 *   - i18n keys:   menu.prepareForTable, menu.pairingsLead, menu.pairingTap
 *   - temperature: data-temperature, [data-detail-add] dataset.temperature
 */
(function (global) {
    "use strict";

    /* ── Utilities ──────────────────────────────────────────────────────────── */

    function esc(s) {
        if (s == null) return "";
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function escAttr(s) {
        return esc(s).replace(/'/g, "&#39;");
    }

    function guestCategoryDisplay(categoryName) {
        if (categoryName == null || categoryName === "") return "";
        var s = String(categoryName).trim();
        if (s === "Signature") return "Good Things";
        return s;
    }

    /* ── Normalize raw JSON to internal shape ──────────────────────────────── */

    function normalizeInput(raw) {
        if (!raw || typeof raw !== "object") return {};
        return {
            id: raw.id,
            image: raw.image || raw.cardImageUrl || "",
            name: raw.name || "",
            price: raw.price,
            priceDisplay: raw.priceDisplay || "",
            subtitle: raw.subtitle || null,
            description: raw.description || null,
            isSignature: !!raw.isSignature,
            supportsHotIced: !!raw.supportsHotIced,
            temperature: raw.temperature || "Hot",
            accentColor: raw.accentColor || "#a06a45",
            categoryName: raw.categoryName || null,
            canAdd: raw.canAdd !== false,
            serviceNote: raw.serviceNote || null,
            isBakery: !!raw.isBakery,
            pairings: Array.isArray(raw.pairings) ? raw.pairings : []
        };
    }

    /* ── Sub-renderers ──────────────────────────────────────────────────────── */

    /**
     * Renders the product image figure, or a placeholder glyph when no image
     * is available.
     */
    function renderProductImage(data) {
        if (!data.image) return renderNoPoster(data);
        return (
            '<figure class="dd-card__figure">' +
            '<img class="dd-card__img" ' +
            'src="' + escAttr(data.image) + '" ' +
            'alt="' + escAttr(data.name || "Drink") + '" ' +
            'width="480" height="640" ' +
            'decoding="async" loading="eager" ' +
            'fetchpriority="high" referrerpolicy="no-referrer" ' +
            'onerror="this.onerror=null;this.style.display=\'none\';" />' +
            "</figure>"
        );
    }

    function renderNoPoster(data) {
        var accent = data.accentColor || "#a06a45";
        return (
            '<div class="dd-card__no-poster" aria-hidden="true">' +
            '<span class="dd-card__no-poster-glyph">' +
            esc(data.name ? data.name[0].toUpperCase() : "·") +
            '</span>' +
            '<span class="dd-card__no-poster-cat">' +
            esc(guestCategoryDisplay(data.categoryName)) +
            '</span>' +
            '</div>'
        );
    }

    /**
     * Hot / Iced toggle row. Only rendered when supportsHotIced is true.
     * Updates data-temperature on the Add-to-Order button via JS wiring
     * in drink-detail-modal.js.
     */
    function renderTemperatureSelector(data) {
        if (!data.supportsHotIced) return "";
        var temp = data.temperature === "Iced" ? "Iced" : "Hot";
        var hotActive = temp === "Hot";
        var icedActive = temp === "Iced";
        var hotLabel = ddT("drink.hot", "Hot");
        var icedLabel = ddT("drink.iced", "Iced");
        return (
            '<div class="dd-card__temp-row" role="group" aria-label="' +
            escAttr(ddT("drink.chooseTemperature", "How would you like it?")) + '">' +
            '<button type="button" class="dd-card__temp-btn' +
            (hotActive ? " dd-card__temp-btn--active" : "") + '" ' +
            'data-temp="Hot" aria-pressed="' + hotActive + '">' +
            esc(hotLabel) +
            '</button>' +
            '<button type="button" class="dd-card__temp-btn' +
            (icedActive ? " dd-card__temp-btn--active" : "") + '" ' +
            'data-temp="Iced" aria-pressed="' + icedActive + '">' +
            esc(icedLabel) +
            '</button>' +
            '</div>'
        );
    }

    /**
     * Suggestion chips — simple category-based hints shown below subtitle.
     * Replaces the old sensory map. No editorial theatre.
     */
    function renderSuggestionChips(data) {
        var text = ((data.categoryName || "") + " " + (data.name || "")).toLowerCase();
        var chips;
        if (/(espresso|americano|cold brew|cà phê đen|ca phe den|đen đá|den da|robusta|pour-over|pourover|v60)/i.test(text)) {
            chips = ["Bold", "Deep", "Concentrated"];
        } else if (/(latte|mocha|bạc xỉu|bac xiu|caramel|cappuccino|cà phê sữa|ca phe sua)/i.test(text)) {
            chips = ["Smooth", "Soft", "Milky"];
        } else if (/(matcha)/i.test(text)) {
            chips = ["Green", "Calm", "Creamy"];
        } else if (/(juice|smoothie|coco|avocado|trái cây|fruit|cam|apple|táo)/i.test(text)) {
            chips = ["Fresh", "Cool", "Light"];
        } else if (data.isBakery) {
            chips = ["Pastry", "Soft", "Light bite"];
        } else {
            chips = ["Balanced", "House style"];
        }
        var html = chips.map(function (c) {
            return '<span class="dd-card__chip">' + esc(c) + '</span>';
        }).join("");
        return '<div class="dd-card__chips">' + html + '</div>';
    }

    /**
     * Long description block — only rendered when description is present.
     */
    function renderDescription(data) {
        if (!data.description) return "";
        return (
            '<div class="dd-card__desc-block">' +
            '<p class="dd-card__desc-label" data-i18n="drink.description">About</p>' +
            '<p class="dd-card__desc-body">' + esc(data.description) + '</p>' +
            '</div>'
        );
    }

    /**
     * Service note — pantry/availability hold.
     */
    function renderServiceNote(note) {
        if (!note) return "";
        return '<p class="dd-card__service-note" role="status">' + esc(note) + '</p>';
    }

    /**
     * Price block.
     */
    function renderPrice(priceDisplay) {
        if (!priceDisplay) return "";
        return '<p class="dd-card__price" aria-label="Price">' + esc(priceDisplay) + '</p>';
    }

    /**
     * Pairing strip — up to 3 bakery pairing suggestions.
     */
    function renderPairings(pairings) {
        if (!pairings || !pairings.length) return "";
        var cards = pairings.slice(0, 3).map(function (p) {
            var img =
                p.image && String(p.image).trim()
                    ? '<img class="dd-card__pairing-img" ' +
                      'src="' + escAttr(p.image) + '" ' +
                      'alt="" width="72" height="72" ' +
                      'decoding="async" loading="lazy" ' +
                      'referrerpolicy="no-referrer" ' +
                      'onerror="this.onerror=null;this.style.display=\'none\';" />'
                    : '<span class="dd-card__pairing-ph" aria-hidden="true"></span>';
            return (
                '<button type="button" class="dd-card__pairing guest-hit" ' +
                'data-pairing-add ' +
                'data-pairing-id="' + escAttr(String(p.id || "")) + '" ' +
                'data-item-name="' + escAttr(p.name || "") + '" ' +
                'data-item-price="' + escAttr(p.price != null ? String(p.price) : "0") + '">' +
                img +
                '<span class="dd-card__pairing-name">' + esc(p.name || "") + '</span>' +
                '<span class="dd-card__pairing-hint" data-i18n="menu.pairingTap">Tap to add</span>' +
                '</button>'
            );
        }).join("");
        return (
            '<section class="dd-card__pairings" aria-labelledby="dd-pairings-lead">' +
            '<p id="dd-pairings-lead" class="dd-card__pairings-lead" ' +
            'data-i18n="menu.pairingsLead">We\'d pair this with</p>' +
            '<div class="dd-card__pairings-strip" role="list">' +
            cards +
            '</div>' +
            '</section>'
        );
    }

    /**
     * Add-to-Order CTA inside the detail card.
     * The top action-bar button (protected) handles the actual cart call.
     * This button updates that button's temperature via the dataset contract.
     */
    function renderAddToOrder(data) {
        var disabled = data.canAdd === false ? " disabled" : "";
        var disabledClass = disabled ? " dd-card__cta--disabled" : "";
        return (
            '<button type="button" class="dd-card__cta' + disabledClass + '" ' +
            'data-detail-add ' +
            'data-drink-id="' + escAttr(String(data.id || "")) + '" ' +
            'data-item-name="' + escAttr(data.name || "") + '" ' +
            'data-item-price="' + escAttr(data.price != null ? String(data.price) : "0") + '" ' +
            'data-temperature="' + escAttr(data.temperature || "Hot") + '"' +
            disabled +
            '>' +
            '<span data-i18n="menu.prepareForTable">Pha cho bàn tôi</span>' +
            '</button>'
        );
    }

    /* ── I18n bridge ─────────────────────────────────────────────────────────── */

    function ddT(key, enFallback) {
        if (typeof global.LuxuryI18n !== "undefined" && typeof global.LuxuryI18n.t === "function") {
            var s = global.LuxuryI18n.t(key);
            if (s && String(s).trim()) return String(s).trim();
        }
        return enFallback;
    }

    /* ── Main render ───────────────────────────────────────────────────────── */

    /**
     * Builds the Kits product-card HTML.
     * @param {object} raw  — DrinkDetailRendererDto
     * @returns {string}    — HTML string
     */
    function render(raw) {
        var data = normalizeInput(raw);

        var catLabel = guestCategoryDisplay(data.categoryName);

        var subtitleHtml = data.subtitle
            ? '<p class="dd-card__subtitle">' + esc(data.subtitle) + '</p>'
            : '';

        var descriptionHtml = renderDescription(data);

        var tempHtml = renderTemperatureSelector(data);

        var chipsHtml = renderSuggestionChips(data);

        var serviceNoteHtml = renderServiceNote(data.serviceNote);

        var pairingsHtml =
            !data.isBakery && data.pairings.length
                ? renderPairings(data.pairings)
                : '';

        return (
            '<article class="dd-product-card" data-drink-id="' +
            escAttr(String(data.id || "")) + '">' +

            renderProductImage(data) +

            '<div class="dd-card__body">' +

            '<div class="dd-card__meta">' +
            (catLabel
                ? '<p class="dd-card__category" aria-label="Category: ' + escAttr(catLabel) + '">' +
                  esc(catLabel) + '</p>'
                : '') +
            '<h1 id="dd-detail-title" class="dd-card__name">' +
            esc(data.name || "") + '</h1>' +
            chipsHtml +
            subtitleHtml +
            '</div>' +

            descriptionHtml +

            tempHtml +

            serviceNoteHtml +

            '<div class="dd-card__footer">' +
            renderPrice(data.priceDisplay) +
            renderAddToOrder(data) +
            '</div>' +

            pairingsHtml +

            '</div>' +
            '</article>'
        );
    }

    /* ── Mount / Unmount ────────────────────────────────────────────────────── */

    function mount(container, raw) {
        if (!container) return null;
        container.innerHTML = render(raw);
        return container.querySelector(".dd-product-card");
    }

    function unmount(container) {
        if (!container) return;
        container.querySelectorAll("img").forEach(function (img) {
            img.src = "";
            img.removeAttribute("src");
        });
        container.replaceChildren();
    }

    /* ── Public API ────────────────────────────────────────────────────────── */

    global.DrinkDetailRenderer = {
        esc: esc,
        render: render,
        mount: mount,
        unmount: unmount,
        normalize: normalizeInput
    };
})(typeof window !== "undefined" ? window : globalThis);
