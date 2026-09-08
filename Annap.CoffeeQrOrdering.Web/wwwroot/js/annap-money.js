/**
 * Canonical VND display for the entire application: "125.000 ₫"
 * (vi-VN thousands separator, no decimals, suffix).
 */
(function (global) {
    "use strict";

    function formatVnd(amount) {
        var n = Math.round(Number(amount) || 0);
        return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 }).format(n) + " ₫";
    }

    global.AnnapMoney = {
        format: formatVnd,
        formatVnd: formatVnd
    };
})(typeof window !== "undefined" ? window : globalThis);
