/**
 * menu-browse-cards.js — ANNAP physical correspondence cards
 * Load settle · proximity drift · compression · ceremonial lift hooks
 * Image crop is CSS-only (4:3 + object-fit: cover) — no JS aspect resizing.
 */
(function (global, document) {
    "use strict";

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

    function finePointer() {
        try {
            return global.matchMedia && global.matchMedia("(hover: hover) and (pointer: fine)").matches;
        } catch (_e2) {
            return false;
        }
    }

    function markLoaded(frame) {
        frame.classList.add("img-loaded");
    }

    function clamp(n, min, max) {
        return Math.max(min, Math.min(max, n));
    }

    function initCards() {
        var cards = document.querySelectorAll(".menu-editorial-card");
        for (var i = 0; i < cards.length; i++) {
            var card = cards[i];
            var frame = card.querySelector(".menu-editorial-card__frame");
            var img = frame && frame.querySelector("img");
            if (!frame) continue;

            if (!img) {
                markLoaded(frame);
                continue;
            }
            if (img.complete && img.naturalWidth > 0) {
                markLoaded(frame);
            } else {
                img.addEventListener(
                    "load",
                    function () {
                        markLoaded(frame);
                    },
                    { once: true }
                );
                img.addEventListener(
                    "error",
                    function () {
                        markLoaded(frame);
                    },
                    { once: true }
                );
            }
        }
        return cards;
    }

    function bindProximityDrift(cards) {
        if (reducedMotion() || !finePointer()) return;

        for (var i = 0; i < cards.length; i++) {
            (function (card) {
                if (card.classList.contains("menu-editorial-card--support")) return;

                var frame = card.querySelector(".menu-editorial-card__frame");
                if (!frame) return;

                var currentX = 0;
                var currentY = 0;
                var targetX = 0;
                var targetY = 0;
                var rafId = 0;

                function tick() {
                    rafId = 0;
                    currentX += (targetX - currentX) * 0.14;
                    currentY += (targetY - currentY) * 0.14;
                    card.style.setProperty("--card-drift-x", currentX.toFixed(2) + "px");
                    card.style.setProperty("--card-drift-y", currentY.toFixed(2) + "px");
                    card.style.setProperty("--card-tilt-x", (currentY * -0.09).toFixed(3) + "deg");
                    card.style.setProperty("--card-tilt-y", (currentX * 0.07).toFixed(3) + "deg");
                    if (Math.abs(targetX - currentX) > 0.04 || Math.abs(targetY - currentY) > 0.04) {
                        rafId = global.requestAnimationFrame(tick);
                    }
                }

                function queueTick() {
                    if (!rafId) rafId = global.requestAnimationFrame(tick);
                }

                card.addEventListener("pointermove", function (ev) {
                    if (!card.classList.contains("is-tactile")) return;
                    var r = frame.getBoundingClientRect();
                    if (!r.width || !r.height) return;
                    var px = clamp((ev.clientX - r.left) / r.width, 0, 1);
                    var py = clamp((ev.clientY - r.top) / r.height, 0, 1);
                    targetX = (px - 0.5) * 5;
                    targetY = (py - 0.5) * -3.5;
                    card.style.setProperty("--menu-card-glow-x", Math.round(px * 100) + "%");
                    card.style.setProperty("--menu-card-glow-y", Math.round(py * 100) + "%");
                    queueTick();
                });

                card.addEventListener("pointerenter", function () {
                    if (card.classList.contains("menu-card--ceremonial-depart")) return;
                    card.classList.add("is-tactile");
                });

                card.addEventListener("pointerleave", function () {
                    card.classList.remove("is-tactile");
                    targetX = 0;
                    targetY = 0;
                    queueTick();
                    card.style.setProperty("--menu-card-glow-x", "50%");
                    card.style.setProperty("--menu-card-glow-y", "38%");
                });
            })(cards[i]);
        }
    }

    function bindReleaseOvershoot() {
        document.addEventListener(
            "pointerup",
            function (ev) {
                var card = ev.target && ev.target.closest ? ev.target.closest(".menu-editorial-card") : null;
                if (!card || !card.classList.contains("menu-card-reveal--pressed")) return;
                if (reducedMotion()) return;
                card.classList.add("menu-card--release");
                global.setTimeout(function () {
                    card.classList.remove("menu-card--release");
                }, 440);
            },
            true
        );
    }

    function ceremonialDepart(card) {
        if (!card || reducedMotion()) return;
        var grid = card.closest(".menu-editorial-grid");
        var browse = card.closest(".menu-browse");
        card.classList.remove("is-tactile", "menu-card--release");
        card.classList.add("menu-card--ceremonial-depart");
        if (grid) {
            grid.classList.add("menu-editorial-grid--card-departing");
            var siblings = grid.querySelectorAll(".menu-editorial-card");
            for (var i = 0; i < siblings.length; i++) {
                if (siblings[i] !== card) siblings[i].classList.add("menu-card--neighbor-yield");
            }
        }
        if (browse) browse.classList.add("annap-menu-atmosphere-shift");
    }

    function clearCeremonialDepart(card) {
        if (!card) return;
        var grid = card.closest(".menu-editorial-grid");
        var browse = card.closest(".menu-browse");
        card.classList.remove("menu-card--ceremonial-depart");
        if (grid) {
            grid.classList.remove("menu-editorial-grid--card-departing");
            var siblings = grid.querySelectorAll(".menu-editorial-card");
            for (var i = 0; i < siblings.length; i++) {
                siblings[i].classList.remove("menu-card--neighbor-yield");
            }
        }
        if (browse) browse.classList.remove("annap-menu-atmosphere-shift");
    }

    function ensureAtmosphere() {
        var browse = document.querySelector(".menu-browse");
        if (!browse || browse.classList.contains("menu-browse--catalogue")) return;
        if (browse.querySelector(".menu-browse-atmosphere")) return;
        var layer = document.createElement("div");
        layer.className = "menu-browse-atmosphere";
        layer.setAttribute("aria-hidden", "true");
        layer.innerHTML =
            '<div class="menu-browse-atmosphere__grain"></div>' +
            '<div class="menu-browse-atmosphere__fog"></div>' +
            '<div class="menu-browse-atmosphere__light"></div>';
        browse.insertBefore(layer, browse.firstChild);
    }

    var cards = initCards();
    ensureAtmosphere();
    bindProximityDrift(cards);
    bindReleaseOvershoot();

    global.AnnapMenuBrowsePhysical = {
        ceremonialDepart: ceremonialDepart,
        clearCeremonialDepart: clearCeremonialDepart,
        refresh: function () {
            cards = initCards();
            bindProximityDrift(cards);
        }
    };

    document.addEventListener("annap-add-ceremony-complete", function () {
        document.querySelectorAll(".menu-editorial-card.menu-card--ceremonial-depart").forEach(function (c) {
            clearCeremonialDepart(c);
        });
    });
})(typeof window !== "undefined" ? window : globalThis, document);
