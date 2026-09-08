(function () {
    "use strict";

    var BANK_TRANSFER_FLOW_VERSION = "v3A";

    function apiUrl(path) {
        return typeof window.__annapApiUrl === "function" ? window.__annapApiUrl(path) : path;
    }

    function devLog(label, detail) {
        try {
            if (window.__ANNAP_DEBUG !== true && !(window.AnnapGuestBoot && window.AnnapGuestBoot.showBootChecklist)) return;
            if (detail !== undefined) window.console.log("[annap-bank-transfer:" + BANK_TRANSFER_FLOW_VERSION + "]", label, detail);
            else window.console.log("[annap-bank-transfer:" + BANK_TRANSFER_FLOW_VERSION + "]", label);
        } catch (_e) {}
    }

    function t(key) {
        if (window.LuxuryI18n && typeof window.LuxuryI18n.t === "function") {
            var v = window.LuxuryI18n.t(key);
            if (v && v !== key) return v;
        }
        return key;
    }

    function escapeHtml(s) {
        return String(s || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function attrEscape(s) {
        return String(s || "").replace(/"/g, "&quot;");
    }

    async function fetchTransferQr(orderId, token) {
        if (!orderId || !token) return null;
        var url =
            apiUrl("/api/orders/" +
            encodeURIComponent(orderId) +
            "/transfer-qr?token=" +
            encodeURIComponent(token));
        var res = await fetch(url, { credentials: "same-origin" });
        devLog("GET transfer-qr status", res.status);
        if (!res.ok) return null;
        return res.json();
    }

    async function fetchAvailability() {
        try {
            var res = await fetch(apiUrl("/api/guest/bank-transfer"), { credentials: "same-origin" });
            devLog("GET bank-transfer availability", res.status);
            if (!res.ok) return { enabled: false };
            return await res.json();
        } catch (_e) {
            return { enabled: false };
        }
    }

    function copyText(text, feedbackEl) {
        if (!text) return Promise.resolve(false);
        var done = function () {
            if (feedbackEl) {
                feedbackEl.textContent = t("checkout.copyDone");
                window.setTimeout(function () {
                    feedbackEl.textContent = "";
                }, 1800);
            }
        };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text).then(function () {
                done();
                return true;
            }).catch(function () { return false; });
        }
        try {
            var ta = document.createElement("textarea");
            ta.value = text;
            ta.setAttribute("readonly", "");
            ta.style.position = "absolute";
            ta.style.left = "-9999px";
            document.body.appendChild(ta);
            ta.select();
            var ok = document.execCommand("copy");
            document.body.removeChild(ta);
            if (ok) done();
            return Promise.resolve(ok);
        } catch (e) {
            return Promise.resolve(false);
        }
    }

    function bindQrImageFallback(root) {
        if (!root) return;
        var img = root.querySelector(".guest-bank-transfer__qr");
        var fallback = root.querySelector(".guest-bank-transfer__qr-fallback");
        if (!img || !fallback) return;
        img.addEventListener("load", function () {
            root.classList.remove("guest-bank-transfer--fallback");
            fallback.classList.add("hidden");
        });
        img.addEventListener("error", function () {
            img.classList.add("hidden");
            fallback.classList.remove("hidden");
            root.classList.add("guest-bank-transfer--fallback");
        });
    }

    function renderTransferCard(qr, opts) {
        opts = opts || {};
        var compact = !!opts.compact;
        if (!qr) return "";
        if (!qr.enabled) {
            return (
                '<div class="guest-bank-transfer guest-bank-transfer--disabled" role="status">' +
                '<p class="guest-bank-transfer__message">' +
                escapeHtml(qr.message || t("checkout.bankTransferUnavailable")) +
                "</p></div>"
            );
        }
        if (qr.status === "paid") {
            return (
                '<div class="guest-bank-transfer guest-bank-transfer--paid" role="status">' +
                '<p class="guest-bank-transfer__message">' +
                escapeHtml(qr.message || t("checkout.bankTransferPaid")) +
                "</p></div>"
            );
        }
        var img = qr.qrImageUrl
            ? '<img class="guest-bank-transfer__qr" src="' + attrEscape(qr.qrImageUrl) + '" alt="' + escapeHtml(t("checkout.transferQrAlt")) + '" loading="eager" decoding="async" />'
            : "";
        var bankLine = [qr.bankName, qr.accountNumber, qr.accountName].filter(Boolean).join(" · ");
        var amountText = qr.amountFormatted || "";
        var bankMeta =
            qr.bankName || qr.accountNumber || qr.accountName
                ? '<div><dt>' +
                  escapeHtml(t("checkout.transferBank")) +
                  '</dt><dd>' +
                  escapeHtml(qr.bankName || "—") +
                  "</dd></div>" +
                  '<div><dt>' +
                  escapeHtml(t("checkout.transferAccountNumber")) +
                  '</dt><dd class="guest-bank-transfer__account">' +
                  escapeHtml(qr.accountNumber || "—") +
                  "</dd></div>" +
                  '<div><dt>' +
                  escapeHtml(t("checkout.transferAccountHolder")) +
                  '</dt><dd>' +
                  escapeHtml(qr.accountName || "—") +
                  "</dd></div>"
                : bankLine
                  ? '<div><dt>' +
                    escapeHtml(t("checkout.transferAccount")) +
                    "</dt><dd>" +
                    escapeHtml(bankLine) +
                    "</dd></div>"
                  : "";
        if (compact) {
            return (
                '<div class="guest-bank-transfer guest-bank-transfer--tray" role="region" aria-label="' +
                escapeHtml(t("checkout.bankTransfer")) +
                '">' +
                (img
                    ? '<div class="guest-bank-transfer__qr-wrap">' +
                      img +
                      '<p class="guest-bank-transfer__qr-fallback hidden" role="status">' +
                      escapeHtml(
                          t(
                              "checkout.transferQrLoadFailed",
                              "Không tải được mã QR. Vui lòng gọi nhân viên hoặc chuyển khoản theo thông tin bên dưới.",
                              "Could not load the QR code. Please call staff or use transfer details below."
                          )
                      ) +
                      "</p></div>"
                    : "") +
                '<div class="guest-bank-transfer__keep-open" role="status">' +
                '<p class="guest-bank-transfer__keep-open-title">' +
                escapeHtml(
                    t(
                        "checkout.bankTransferKeepOpen",
                        "Vui lòng giữ nguyên màn hình chuyển khoản để nhân viên ra kiểm tra và xác nhận thanh toán.",
                        "Please keep the bank transfer screen open so staff can come verify and confirm payment."
                    )
                ) +
                "</p>" +
                '<p class="guest-bank-transfer__keep-open-sub">' +
                escapeHtml(
                    t(
                        "checkout.bankTransferStaffConfirmNote",
                        "Đơn chỉ chuyển sang Đã thanh toán sau khi nhân viên xác nhận.",
                        "Your order is marked paid only after staff confirm payment."
                    )
                ) +
                "</p></div>" +
                '<div class="guest-bank-transfer__fallback-details" role="status">' +
                '<p class="guest-bank-transfer__amount">' +
                escapeHtml(amountText) +
                "</p>" +
                '<dl class="guest-bank-transfer__meta">' +
                '<div><dt>' +
                escapeHtml(t("checkout.transferMemo")) +
                '</dt><dd class="guest-bank-transfer__memo">' +
                escapeHtml(qr.memo || "") +
                "</dd></div>" +
                bankMeta +
                "</dl>" +
                '<div class="guest-bank-transfer__actions">' +
                '<button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-memo="' +
                escapeHtml(qr.memo || "") +
                '">' +
                escapeHtml(t("checkout.copyMemo")) +
                "</button>" +
                '<button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-amount="' +
                escapeHtml(String(qr.amount || "")) +
                '">' +
                escapeHtml(t("checkout.copyAmount")) +
                "</button>" +
                '<button type="button" class="guest-bank-transfer__copy guest-hit" data-bt-retry="1">' +
                escapeHtml(t("checkout.transferRetry")) +
                "</button>" +
                "</div>" +
                "</div>" +
                '<p class="guest-bank-transfer__feedback" aria-live="polite"></p>' +
                "</div>"
            );
        }
        return (
            '<div class="guest-bank-transfer' + (compact ? " guest-bank-transfer--tray" : "") + '" role="region" aria-label="' + escapeHtml(t("checkout.bankTransfer")) + '">' +
            (compact ? "" : '<p class="guest-bank-transfer__title">' + escapeHtml(t("checkout.bankTransfer")) + "</p>") +
            (img
                ? '<div class="guest-bank-transfer__qr-wrap">' +
                  img +
                  '<p class="guest-bank-transfer__qr-fallback hidden" role="status">' +
                  escapeHtml(t("checkout.transferQrLoadFailed")) +
                  "</p></div>"
                : "") +
            '<p class="guest-bank-transfer__amount">' + escapeHtml(amountText) + "</p>" +
            (compact
                ? '<div class="guest-bank-transfer__keep-open" role="status">' +
                  '<p class="guest-bank-transfer__keep-open-title">' +
                  escapeHtml(
                      t(
                          "checkout.bankTransferKeepOpen",
                          "Vui lòng giữ nguyên màn hình chuyển khoản để nhân viên ra kiểm tra và xác nhận thanh toán.",
                          "Please keep the bank transfer screen open so staff can come verify and confirm payment."
                      )
                  ) +
                  "</p>" +
                  '<p class="guest-bank-transfer__keep-open-sub">' +
                  escapeHtml(
                      t(
                          "checkout.bankTransferStaffConfirmNote",
                          "Đơn chỉ chuyển sang Đã thanh toán sau khi nhân viên xác nhận.",
                          "Your order is marked paid only after staff confirm payment."
                      )
                  ) +
                  "</p></div>"
                : "") +
            '<dl class="guest-bank-transfer__meta">' +
            '<div><dt>' + escapeHtml(t("checkout.transferMemo")) + '</dt><dd class="guest-bank-transfer__memo">' + escapeHtml(qr.memo || "") + "</dd></div>" +
            bankMeta +
            "</dl>" +
            (compact
                ? ""
                : '<p class="guest-bank-transfer__note">' +
                  escapeHtml(qr.message || t("checkout.transferExactNote")) +
                  "</p>") +
            '<div class="guest-bank-transfer__actions">' +
            '<button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-memo="' + escapeHtml(qr.memo || "") + '">' +
            escapeHtml(t("checkout.copyMemo")) +
            "</button>" +
            '<button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-amount="' + escapeHtml(String(qr.amount || "")) + '">' +
            escapeHtml(t("checkout.copyAmount")) +
            "</button>" +
            "</div>" +
            '<p class="guest-bank-transfer__feedback" aria-live="polite"></p>' +
            "</div>"
        );
    }

    function bindCopyButtons(root) {
        if (!root) return;
        var feedback = root.querySelector(".guest-bank-transfer__feedback");
        root.querySelectorAll("[data-copy-memo]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                copyText(btn.getAttribute("data-copy-memo"), feedback);
            });
        });
        root.querySelectorAll("[data-copy-amount]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                copyText(btn.getAttribute("data-copy-amount"), feedback);
            });
        });
        root.querySelectorAll("[data-bt-retry]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var host = root.parentElement;
                if (!host) return;
                var card = host.querySelector(".guest-bank-transfer");
                if (card) card.classList.remove("guest-bank-transfer--fallback");
                var img = host.querySelector(".guest-bank-transfer__qr");
                if (img && img.getAttribute("src")) {
                    var src = img.getAttribute("src");
                    img.setAttribute("src", "");
                    img.setAttribute("src", src);
                }
            });
        });
    }

    function mountTransferCard(host, qr, opts) {
        if (!host) return;
        host.innerHTML = renderTransferCard(qr, opts);
        var card = host.querySelector(".guest-bank-transfer");
        bindCopyButtons(card);
        bindQrImageFallback(card);
    }

    window.GuestBankTransfer = {
        fetchTransferQr: fetchTransferQr,
        fetchAvailability: fetchAvailability,
        renderTransferCard: renderTransferCard,
        mountTransferCard: mountTransferCard,
        bindCopyButtons: bindCopyButtons,
        copyText: copyText,
        t: t,
        version: BANK_TRANSFER_FLOW_VERSION
    };
    devLog("loaded");
})();
