/**
 * Menu operator — Phase 2: server-side filtering.
 *
 * The page now owns filter state via the URL query string (q, category,
 * status). Filtering and search no longer happen in the browser. This file
 * only keeps the interactive bits that the GET form cannot do on its own:
 *
 *   - Toggle the category-ordering panel.
 *   - Drag categories up / down inside that panel.
 *   - The data-confirm UX guard is rendered inline on Index.cshtml.
 *
 * Do NOT reintroduce a client-side filter cache here. The URL is the only
 * source of truth for "what is the user currently looking at".
 */
(function () {
    "use strict";

    function init() {
        var root = document.getElementById("menu-operator-root");
        if (!root) return;

        var panel = document.getElementById("menu-operator-category-panel");
        var catList = document.getElementById("menu-operator-category-list");

        // Category ordering panel toggle (unchanged from Phase 1)
        var toggles = root.querySelectorAll("[data-action='toggle-category-order']");
        toggles.forEach(function (btn) {
            btn.addEventListener("click", function () {
                if (!panel) return;
                var open = !panel.hidden;
                panel.hidden = open;
                btn.setAttribute("aria-expanded", open ? "false" : "true");
            });
        });

        // Category up/down inside the ordering panel (unchanged from Phase 1)
        if (catList) {
            catList.addEventListener("click", function (e) {
                var btn = e.target.closest("[data-action]");
                if (!btn) return;
                var action = btn.getAttribute("data-action");
                if (action !== "cat-up" && action !== "cat-down") return;
                var row = btn.closest(".menu-operator__category-row");
                if (!row) return;
                var sibling = action === "cat-up" ? row.previousElementSibling : row.nextElementSibling;
                if (!sibling || !sibling.classList.contains("menu-operator__category-row")) return;
                if (action === "cat-up") {
                    catList.insertBefore(row, sibling);
                } else {
                    sibling.parentNode.insertBefore(sibling, row);
                }
                refreshSortInputs();
            });
        }

        function refreshSortInputs() {
            if (!catList) return;
            var rows = catList.querySelectorAll(".menu-operator__category-row");
            rows.forEach(function (row, idx) {
                var input = row.querySelector("[data-cat-sort-input]");
                if (input) input.value = String(idx);
                var upBtn = row.querySelector("[data-action='cat-up']");
                var downBtn = row.querySelector("[data-action='cat-down']");
                if (upBtn) upBtn.disabled = idx === 0;
                if (downBtn) downBtn.disabled = idx === rows.length - 1;
            });
        }

        // Clear-filters link is a normal anchor; nothing else needs to happen.
        // Phase 1 quick-action forms (sold-out, available, archive, restore)
        // remain real POST forms with antiforgery tokens — untouched.
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
