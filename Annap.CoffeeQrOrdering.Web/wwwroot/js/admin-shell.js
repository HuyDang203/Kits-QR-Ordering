/**
 * Annap Admin shell — mobile drawer, optional dropdown, reduced-motion aware.
 */
(function () {
    "use strict";

    function initMobileNav() {
        var toggle = document.getElementById("admin-nav-toggle");
        var drawer = document.getElementById("admin-drawer");
        var backdrop = document.getElementById("admin-drawer-backdrop");
        var closeBtn = document.getElementById("admin-drawer-close");
        if (!toggle || !drawer || !backdrop) return;

        var open = false;

        function setOpen(v) {
            open = v;
            drawer.classList.toggle("is-open", v);
            backdrop.classList.toggle("is-open", v);
            toggle.setAttribute("aria-expanded", v ? "true" : "false");
            if (v) {
                document.body.classList.add("admin-scroll-lock");
            } else {
                document.body.classList.remove("admin-scroll-lock");
            }
        }

        toggle.addEventListener("click", function () {
            setOpen(!open);
        });
        backdrop.addEventListener("click", function () {
            setOpen(false);
        });
        if (closeBtn) closeBtn.addEventListener("click", function () {
            setOpen(false);
        });

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape") setOpen(false);
        });

        drawer.querySelectorAll("a.admin-nav-link").forEach(function (a) {
            a.addEventListener("click", function () {
                setOpen(false);
            });
        });
    }

    function initUserMenu() {
        var btn = document.getElementById("admin-user-menu-btn");
        var root = document.getElementById("admin-user-menu");
        if (!btn || !root) return;

        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            root.classList.toggle("is-open");
        });

        var panel = root.querySelector(".admin-dropdown__panel");
        if (panel) {
            panel.addEventListener("click", function (e) {
                e.stopPropagation();
            });
        }

        document.addEventListener("click", function () {
            root.classList.remove("is-open");
        });
    }

    function initDemoModal() {
        var openBtn = document.getElementById("admin-demo-modal-open");
        var backdrop = document.getElementById("admin-demo-modal");
        if (!openBtn || !backdrop) return;

        var panel = backdrop.querySelector(".admin-modal");

        function setOpen(v) {
            backdrop.classList.toggle("is-open", v);
            backdrop.setAttribute("aria-hidden", v ? "false" : "true");
        }

        openBtn.addEventListener("click", function () {
            setOpen(true);
        });

        backdrop.addEventListener("click", function (e) {
            if (e.target === backdrop) setOpen(false);
        });

        backdrop.querySelectorAll("[data-admin-modal-close]").forEach(function (el) {
            el.addEventListener("click", function () {
                setOpen(false);
            });
        });

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && backdrop.classList.contains("is-open")) setOpen(false);
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        initMobileNav();
        initUserMenu();
        initDemoModal();
    });
})();
