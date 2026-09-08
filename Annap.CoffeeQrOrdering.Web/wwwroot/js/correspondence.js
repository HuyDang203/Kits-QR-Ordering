/**
 * ANNAP Correspondence Runtime — page transitions + tray chrome helpers.
 * Tray state, chip summary, fly-to-tray: order-tray-dock.js + add-to-order-animation-provider.js
 */
(function () {
  "use strict";

  function noMotion() {
    return (
      document.documentElement.classList.contains("annap-guest-no-motion") ||
      window.matchMedia("(prefers-reduced-motion: reduce)").matches
    );
  }

  function isCorrespond() {
    return document.documentElement.classList.contains("annap-correspond-ui");
  }

  function initPageIn() {
    if (noMotion()) return;
    var root = document.getElementById("annap-root");
    if (!root) return;
    root.classList.add("annap-page-in");
    root.addEventListener(
      "animationend",
      function () { root.classList.remove("annap-page-in"); },
      { once: true }
    );
  }

  function initTrayClose() {
    var btn = document.getElementById("annap-tray-close");
    if (!btn) return;
    btn.addEventListener("click", function () {
      var chip = document.getElementById("order-tray-chip");
      if (chip && chip.getAttribute("aria-expanded") === "true") chip.click();
    });
  }

  function initPageTransitions() {
    if (!isCorrespond()) return;
    var root = document.getElementById("annap-root");
    if (!root) return;

    if (!noMotion()) {
      document.addEventListener("click", function (e) {
        var link = e.target.closest ? e.target.closest("[data-guest-vt-link]") : null;
        if (!link) return;
        if (e.defaultPrevented) return;
        var href = link.getAttribute("href");
        if (!href || href.charAt(0) === "#") return;
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;

        e.preventDefault();
        root.classList.add("is-leaving");
        setTimeout(function () { window.location.href = href; }, 290);
      });
    }

    window.addEventListener("pageshow", function (e) {
      if (e.persisted) root.classList.remove("is-leaving");
    });
  }

  function boot() {
    initPageIn();
    initTrayClose();
    initPageTransitions();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
