/**
 * Menu operator — list filtering, archive toggle, category ordering panel.
 * No framework. No server round-trips beyond the category-ordering POST.
 */
(function () {
    "use strict";

    function normalize(s) {
        return (s || "")
            .toString()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .trim();
    }

    function init() {
        var root = document.getElementById("menu-operator-root");
        if (!root) return;

        var searchInput = document.getElementById("menu-operator-search");
        var filterChips = root.querySelectorAll("[data-category-filter]");
        var archiveToggle = document.getElementById("menu-operator-archive-toggle");
        var list = document.getElementById("menu-operator-list");
        var noResults = document.getElementById("menu-operator-no-results");
        var panel = document.getElementById("menu-operator-category-panel");
        var catList = document.getElementById("menu-operator-category-list");

        var activeCategory = "";

        function applyFilters() {
            if (!list) return;
            var q = normalize(searchInput && searchInput.value);
            var showArchived = !!(archiveToggle && archiveToggle.checked);
            var rows = list.querySelectorAll(".menu-operator__row");
            var visible = 0;
            rows.forEach(function (row) {
                var name = normalize(row.getAttribute("data-name"));
                var sub = normalize(row.getAttribute("data-subtitle"));
                var cat = normalize(row.getAttribute("data-category"));
                var catId = row.getAttribute("data-category-id") || "";
                var archived = row.getAttribute("data-archived") === "1";
                var matchesText = !q || name.indexOf(q) >= 0 || sub.indexOf(q) >= 0 || cat.indexOf(q) >= 0;
                var matchesCat = !activeCategory || catId === activeCategory;
                var matchesArchived = showArchived || !archived;
                var show = matchesText && matchesCat && matchesArchived;
                row.hidden = !show;
                if (show) visible++;
            });
            if (noResults) noResults.hidden = visible > 0;
        }

        if (searchInput) {
            searchInput.addEventListener("input", applyFilters);
        }

        filterChips.forEach(function (chip) {
            chip.addEventListener("click", function () {
                filterChips.forEach(function (c) {
                    c.classList.remove("is-active");
                    c.setAttribute("aria-selected", "false");
                });
                chip.classList.add("is-active");
                chip.setAttribute("aria-selected", "true");
                activeCategory = chip.getAttribute("data-category-filter") || "";
                applyFilters();
            });
        });

        if (archiveToggle) {
            archiveToggle.addEventListener("change", applyFilters);
        }

        // Category ordering panel toggle
        var toggles = root.querySelectorAll("[data-action='toggle-category-order']");
        toggles.forEach(function (btn) {
            btn.addEventListener("click", function () {
                if (!panel) return;
                var open = !panel.hidden;
                panel.hidden = open;
                btn.setAttribute("aria-expanded", open ? "false" : "true");
            });
        });

        // Category up/down
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
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();