/**
 * ANNAP OrderTrayProvider
 * Centralized tray state + portal-based fly-to-tray choreography for the guest ordering surface.
 * Exposes a small `useFlyToTray()` facade for non-React pages.
 */
(function (global, document) {
    "use strict";

    var copyIx = 0;
    var queue = [];
    var running = false;
    var cartTarget = null;
    var detailTrayVisible = false;
    var labelTimers = typeof WeakMap !== "undefined" ? new WeakMap() : null;
    var copyLineKeys = [
        "toast.ceremonyPlaced",
        "toast.ceremonySaved",
        "toast.ceremonyWaiting"
    ];
    var copyLineVi = [
        "Đã thêm vào khay",
        "Giữ cho vòng nếm này",
        "Chờ bên quầy Kits"
    ];

    function reducedMotion() {
        try {
            return (
                document.documentElement.classList.contains("annap-guest-no-motion") ||
                (global.matchMedia && global.matchMedia("(prefers-reduced-motion: reduce)").matches)
            );
        } catch (_e) {
            return false;
        }
    }

    function asElement(target) {
        if (!target) return null;
        if (typeof target === "string") return document.querySelector(target);
        return target.nodeType === 1 ? target : null;
    }

    function ensureLayer() {
        var layer = document.getElementById("annap-add-order-layer");
        if (layer) return layer;
        layer = document.createElement("div");
        layer.id = "annap-add-order-layer";
        layer.className = "annap-add-order-layer";
        layer.setAttribute("aria-hidden", "true");
        document.body.appendChild(layer);
        return layer;
    }

    function cartLines() {
        try {
            if (global.GuestInteractionContract && typeof global.GuestInteractionContract.getCartLines === "function") {
                return global.GuestInteractionContract.getCartLines() || [];
            }
        } catch (_e) {}
        return [];
    }

    function totalQty(lines) {
        var n = 0;
        for (var i = 0; i < lines.length; i++) n += Number(lines[i].qty) || 0;
        return n;
    }

    function openMainTray() {
        try {
            if (global.GuestInteractionContract && typeof global.GuestInteractionContract.openTray === "function") {
                global.GuestInteractionContract.openTray();
                return;
            }
        } catch (_e) {}
        var chip = document.getElementById("order-tray-chip");
        if (chip) chip.click();
    }

    function resolveCartTarget() {
        var registered = asElement(cartTarget);
        if (registered && document.documentElement.contains(registered)) return registered;
        return (
            document.getElementById("order-tray-chip-impact") ||
            document.getElementById("order-tray-chip-stack") ||
            document.getElementById("order-tray-chip") ||
            document.querySelector("[data-cart-target]")
        );
    }

    function sourceCardFor(source) {
        if (!source || !source.closest) return null;
        return (
            source.closest("[data-drink-card]") ||
            source.closest(".ge-result-card") ||
            source.closest(".ge-disc-rec") ||
            source.closest(".dd-pairing") ||
            source.closest(".dd-product-card") ||
            source.closest(".dd-passport") ||
            source.closest("article")
        );
    }

    function sourceImageFor(source, card) {
        var root = card || (source && source.closest ? source.closest("article,section,div") : null);
        return root && root.querySelector ? root.querySelector("img") : null;
    }

    function safeRect(el, fallback) {
        try {
            if (el && el.getBoundingClientRect) {
                var r = el.getBoundingClientRect();
                if (r && r.width > 0 && r.height > 0) return r;
            }
        } catch (_e) {}
        return fallback || {
            left: global.innerWidth * 0.5 - 24,
            top: global.innerHeight * 0.5 - 24,
            width: 48,
            height: 48
        };
    }

    function nextCopy(explicit) {
        if (explicit) return explicit;
        var ix = copyIx % copyLineKeys.length;
        var text = tMenu(copyLineKeys[ix], copyLineVi[ix]);
        copyIx++;
        return text;
    }

    function tMenu(key, fallback) {
        try {
            if (global.LuxuryI18n && typeof global.LuxuryI18n.t === "function") {
                var v = global.LuxuryI18n.t(key);
                if (v) return v;
            }
        } catch (_ti) {}
        return fallback || "";
    }

    function pairingAddedCopy() {
        return tMenu("menu.pairingAdded", "Đã thêm");
    }

    function pairingTapCopy() {
        return tMenu("menu.pairingTap", "Chạm để thêm");
    }

    function pairingReceiptCopy(itemName) {
        var name = String(itemName || "").trim();
        var template = tMenu("menu.pairingReceipt", "Đã thêm {name} vào khay");
        if (name && template.indexOf("{name}") >= 0) {
            return template.replace("{name}", name);
        }
        if (name) return "Đã thêm " + name + " vào khay";
        return tMenu("menu.pairingReceiptShort", "Đã thêm vào khay");
    }

    function escAttr(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/"/g, "&quot;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");
    }

    function applyButtonAcknowledgement(source, text) {
        if (!source || !source.classList) return;
        if (!source.hasAttribute("data-label-original")) {
            source.setAttribute("data-label-original", source.innerHTML);
        }
        if (labelTimers) {
            var pending = labelTimers.get(source);
            if (pending) {
                clearTimeout(pending);
                labelTimers.delete(source);
            }
        }
        source.innerHTML = text;
        source.classList.add("btn-ink--added", "annap-add-source--acknowledged");
        var timer = setTimeout(function () {
            var original = source.getAttribute("data-label-original");
            if (original !== null) source.innerHTML = original;
            source.classList.remove("btn-ink--added", "annap-add-source--acknowledged");
            if (labelTimers) labelTimers.delete(source);
        }, 2800);
        if (labelTimers) labelTimers.set(source, timer);
    }

    function pulseCart(target) {
        if (!target || !target.classList) return;
        target.classList.remove("annap-cart-impact", "annap-tray-chip--pulse", "annap-detail-tray--receive");
        void target.offsetWidth;
        target.classList.add("annap-cart-impact", "annap-tray-chip--pulse");
        setTimeout(function () {
            target.classList.remove("annap-cart-impact", "annap-tray-chip--pulse", "annap-detail-tray--receive");
        }, 980);
    }

    function acknowledgePairing(card) {
        if (!card || !card.classList || !card.classList.contains("dd-pairing")) return;
        card.classList.remove("dd-pairing--selected-for-table");
        card.classList.add("dd-pairing--added");
        var hint = card.querySelector(".dd-pairing__hint");
        var added = pairingAddedCopy();
        if (hint && added) hint.textContent = added;
        if (!card.querySelector(".dd-pairing__stamp")) {
            var stamp = document.createElement("span");
            stamp.className = "dd-pairing__stamp";
            stamp.setAttribute("aria-hidden", "true");
            stamp.textContent = added || "Đã thêm";
            card.appendChild(stamp);
        }
        setTimeout(function () {
            card.classList.remove("dd-pairing--added");
            var stampEl = card.querySelector(".dd-pairing__stamp");
            if (stampEl && stampEl.parentNode) stampEl.parentNode.removeChild(stampEl);
            if (hint) {
                var tap = pairingTapCopy();
                if (tap) hint.textContent = tap;
            }
        }, reducedMotion() ? 1200 : 1800);
    }

    function createReceiptToast(text, targetRect) {
        var layer = ensureLayer();
        var toast = document.createElement("div");
        toast.className = "annap-tray-receipt-toast";
        toast.setAttribute("role", "status");
        toast.setAttribute("aria-live", "polite");
        var mark = document.createElement("span");
        mark.className = "annap-tray-receipt-toast__mark";
        mark.setAttribute("aria-hidden", "true");
        mark.textContent = "✓";
        var copy = document.createElement("span");
        copy.className = "annap-tray-receipt-toast__copy";
        copy.textContent = text;
        toast.appendChild(mark);
        toast.appendChild(copy);

        var trayEl =
            document.getElementById("order-tray-chip") ||
            document.getElementById("order-tray-root");
        var rect = safeRect(trayEl, targetRect);
        var bottomGap = Math.max(10, global.innerHeight - rect.top + 8);
        toast.style.left = "50%";
        toast.style.bottom = Math.round(bottomGap) + "px";
        layer.appendChild(toast);

        var hideMs = reducedMotion() ? 1200 : 1600;
        var removeMs = reducedMotion() ? 1400 : 1800;
        setTimeout(function () {
            toast.classList.add("annap-tray-receipt-toast--out");
        }, hideMs);
        setTimeout(function () {
            if (toast.parentNode) toast.parentNode.removeChild(toast);
        }, removeMs);
    }

    function createAmbientConfirmation(text, targetRect) {
        var layer = ensureLayer();
        var note = document.createElement("div");
        note.className = "annap-add-confirmation";
        note.textContent = text;
        var x = targetRect.left + targetRect.width / 2;
        var y = Math.max(80, targetRect.top - 18);
        note.style.left = Math.round(x) + "px";
        note.style.top = Math.round(y) + "px";
        layer.appendChild(note);
        note.addEventListener("animationend", function () {
            if (note.parentNode) note.parentNode.removeChild(note);
        }, { once: true });
    }

    function createImpactRipple(targetRect) {
        var layer = ensureLayer();
        var ripple = document.createElement("div");
        ripple.className = "annap-cart-impact-ripple";
        ripple.style.left = Math.round(targetRect.left + targetRect.width / 2) + "px";
        ripple.style.top = Math.round(targetRect.top + targetRect.height / 2) + "px";
        layer.appendChild(ripple);
        ripple.addEventListener("animationend", function () {
            if (ripple.parentNode) ripple.parentNode.removeChild(ripple);
        }, { once: true });
    }

    function createFlyer(source, sourceRect, targetRect, opts) {
        var layer = ensureLayer();
        var card = opts.card || sourceCardFor(source);
        var sourceImg = sourceImageFor(source, card);
        var flyer = document.createElement("div");
        var size = Math.max(46, Math.min(opts.elevated ? 124 : 104, sourceRect.width * (opts.elevated ? 0.5 : 0.42) || 64));
        var startX = sourceRect.left + sourceRect.width / 2;
        var startY = sourceRect.top + sourceRect.height / 2;
        var endX = targetRect.left + targetRect.width / 2;
        var endY = targetRect.top + targetRect.height / 2;

        flyer.className = "annap-tasting-flyer" + (opts.elevated ? " annap-tasting-flyer--sommelier" : "");
        flyer.style.left = Math.round(startX - size / 2) + "px";
        flyer.style.top = Math.round(startY - size / 2) + "px";
        flyer.style.width = Math.round(size) + "px";
        flyer.style.height = Math.round(size) + "px";
        flyer.style.setProperty("--annap-fly-x", Math.round(endX - startX) + "px");
        flyer.style.setProperty("--annap-fly-y", Math.round(endY - startY) + "px");

        if (sourceImg && sourceImg.getAttribute("src")) {
            var img = document.createElement("img");
            img.alt = "";
            img.decoding = "async";
            img.src = sourceImg.getAttribute("src");
            flyer.appendChild(img);
        } else {
            flyer.innerHTML = '<span class="annap-tasting-flyer__glyph">A</span>';
        }

        layer.appendChild(flyer);
        flyer.addEventListener("animationend", function () {
            if (flyer.parentNode) flyer.parentNode.removeChild(flyer);
        }, { once: true });
        return flyer;
    }

    function compressCard(card) {
        if (!card || !card.classList) return;
        if (isMenuEditorialCard(card)) {
            card.classList.add("menu-card-reveal--pressed");
            setTimeout(function () {
                card.classList.remove("menu-card-reveal--pressed");
            }, 160);
            return;
        }
        card.classList.remove("annap-card-ceremony-compress");
        void card.offsetWidth;
        card.classList.add("annap-card-ceremony-compress");
        setTimeout(function () {
            card.classList.remove("annap-card-ceremony-compress");
        }, 160);
    }

    function isMenuEditorialCard(card) {
        return !!(card && card.classList && card.classList.contains("menu-editorial-card"));
    }

    function liftCard(card, elevated) {
        if (!card || !card.classList) return;
        if (isMenuEditorialCard(card)) {
            try {
                if (global.AnnapMenuBrowsePhysical && typeof global.AnnapMenuBrowsePhysical.ceremonialDepart === "function") {
                    global.AnnapMenuBrowsePhysical.ceremonialDepart(card);
                }
            } catch (_menuLift) {}
        }
        card.classList.remove("annap-card-ceremony-lift", "annap-card-ceremony-lift--sommelier");
        void card.offsetWidth;
        if (!isMenuEditorialCard(card)) {
            card.classList.add("annap-card-ceremony-lift");
            if (elevated) card.classList.add("annap-card-ceremony-lift--sommelier");
        }
        setTimeout(function () {
            card.classList.remove("annap-card-ceremony-lift", "annap-card-ceremony-lift--sommelier");
            if (isMenuEditorialCard(card)) {
                try {
                    if (global.AnnapMenuBrowsePhysical && typeof global.AnnapMenuBrowsePhysical.clearCeremonialDepart === "function") {
                        global.AnnapMenuBrowsePhysical.clearCeremonialDepart(card);
                    }
                } catch (_menuClear) {}
            }
        }, elevated ? 1020 : 860);
    }

    function settleChipAfterImpact() {
        try {
            document.dispatchEvent(new CustomEvent("annap:tray-chip-settle"));
        } catch (_settle) {}
    }

    function resolveFlavor(source, opts) {
        if (opts.variant) return opts.variant;
        if (source && source.closest && source.closest(".ge-result-card")) return "sommelier";
        if (source && source.closest && source.closest(".dd-pairing")) return "pairing";
        return "standard";
    }

    function run(job, done) {
        var source = asElement(job.sourceElement);
        var target = asElement(job.cartTarget) || resolveCartTarget();
        var targetRect = safeRect(target, {
            left: global.innerWidth * 0.5 - 28,
            top: global.innerHeight - 72,
            width: 56,
            height: 40
        });
        var sourceRect = safeRect(source);
        var variant = resolveFlavor(source, job);
        var elevated = variant === "sommelier";
        var copy = nextCopy(job.copy);
        var card = sourceCardFor(source);
        var itemName =
            (job.name && String(job.name).trim()) ||
            (source && source.getAttribute && source.getAttribute("data-item-name")) ||
            "";

        if (variant === "pairing") {
            pulseCart(target);
            acknowledgePairing(card);
            createReceiptToast(pairingReceiptCopy(itemName), targetRect);
            settleChipAfterImpact();
            done();
            return;
        }

        applyButtonAcknowledgement(source, copy);
        if (reducedMotion()) {
            pulseCart(target);
            createAmbientConfirmation(copy, targetRect);
            settleChipAfterImpact();
            done();
            return;
        }

        document.body.classList.add("annap-add-ceremony-active");
        if (elevated) document.body.classList.add("annap-add-ceremony-sommelier");
        compressCard(card);
        setTimeout(function () {
            liftCard(card, elevated);
            createFlyer(source, sourceRect, targetRect, { elevated: elevated, card: card });
        }, 110);

        var impactAt = elevated ? 640 : 580;
        var settleAt = 740;
        var chipEl = document.getElementById("order-tray-chip");

        setTimeout(function () {
            pulseCart(chipEl || target);
            createImpactRipple(targetRect);
        }, impactAt);
        setTimeout(function () {
            createAmbientConfirmation(copy, targetRect);
        }, impactAt + 80);
        setTimeout(function () {
            settleChipAfterImpact();
        }, settleAt);
        setTimeout(function () {
            document.body.classList.remove("annap-add-ceremony-active", "annap-add-ceremony-sommelier");
            try {
                document.dispatchEvent(new CustomEvent("annap-add-ceremony-complete", { bubbles: true }));
            } catch (_doneEv) {}
            done();
        }, elevated ? 1040 : 920);
    }

    function drain() {
        if (running || queue.length === 0) return;
        running = true;
        var job = queue.shift();
        run(job, function () {
            running = false;
            if (queue.length) setTimeout(drain, 80);
        });
    }

    function flyToTray(options) {
        options = options || {};
        queue.push(options);
        if (queue.length > 3) queue.splice(0, queue.length - 3);
        drain();
    }

    function handleGuestInteraction(e) {
        var d = (e && e.detail) || {};
        if (d.type !== "itemAdded") return;
        flyToTray({
            sourceElement: d.sourceElement || null,
            menuItemId: d.menuItemId,
            name: d.name || "",
            variant: d.variant || "",
            copy: d.copy || ""
        });
    }

    var provider = {
        flyToTray: flyToTray,
        flyToCart: flyToTray,
        registerCartTarget: function (target) { cartTarget = target; },
        setDetailTrayVisible: function (visible) {
            detailTrayVisible = !!visible;
            document.body.classList.toggle("annap-tray-detail-context", detailTrayVisible);
        },
        openTray: openMainTray,
        handlesAmbientConfirmation: true
    };

    global.OrderTrayProvider = provider;
    global.AddToOrderAnimationProvider = provider;
    global.useFlyToTray = function () {
        return {
            flyToTray: flyToTray,
            registerCartTarget: provider.registerCartTarget
        };
    };
    global.useFlyToCart = global.useFlyToTray;

    document.addEventListener("annap:guest-interaction", handleGuestInteraction);
    document.addEventListener("annap-drink-detail-opened", function () {
        provider.setDetailTrayVisible(true);
    });
    document.addEventListener("annap-drink-detail-closed", function () {
        provider.setDetailTrayVisible(false);
    });
})(typeof window !== "undefined" ? window : globalThis, document);
