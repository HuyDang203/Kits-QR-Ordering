/* Annap — shared order tray dock (Menu + seated Home) */
function annapBootOff(key) {
    var b = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
    return b[key] === true;
}
const GUEST_CTX = (function () {
    const n = document.getElementById("menu-guest-context");
    if (!n) return {};
    try {
        return JSON.parse(n.textContent || "{}");
    } catch (_annap) {
        return {};
    }
})();
let VENUE_TABLE_ID = "";
let menuOrderIdempotencyKey = null;
let menuOrderSubmitInFlight = false;
const TRAY_STATE = {
    EMPTY: "empty",
    EDITING: "editing",
    REVIEW: "review",
    PAYMENT_CHOICE: "paymentChoice",
    SUBMITTING: "submitting",
    SUBMITTED_COUNTER: "submittedCounterPayment",
    SUBMITTED_CASH: "submittedCashPayment",
    SUBMITTED_CARD: "submittedCardPayment",
    SUBMITTED_BANK: "submittedBankTransfer",
    SUBMITTED_PENDING: "submittedPendingPayment",
    PAID: "paid",
    COMPLETED: "completed"
};
/** Presentation state — sole owner of checkout tray UI. */
const CHECKOUT_UI = {
    EDITING: "editing",
    REVIEW: "review",
    PAYMENT_SELECTION: "payment-selection",
    PAYMENT_BANK_LOADING: "payment-bank-loading",
    PAYMENT_BANK_READY: "payment-bank-ready",
    SUBMITTED: "submitted",
    PAID: "paid",
    COMPLETED: "completed",
    FULFILLMENT_MODE: "fulfillment-mode"
};
const FULFILLMENT_MODE = {
    DINE_IN: "DineIn",
    TAKEAWAY: "Takeaway"
};
const PAYMENT_METHOD = {
    CASH: "Cash",
    CARD: "Card",
    BANK: "BankTransfer",
    LEGACY_CASH_CARD: "CashOrCardAtCounter"
};
const TRAY_PAYMENT_FLOW_VERSION = "v7-fulfillment-mode";
let checkoutUi = CHECKOUT_UI.EDITING;
let selectedPaymentMethod = PAYMENT_METHOD.CASH;
let selectedFulfillmentMode = null;
let trayStatusFingerprint = "";
const expandedItemNoteKeys = new Set();
let bankTransferConfigured = null;
let traySubmittedStatus = null;
let trayStatusPollTimer = null;
let trayKnownPendingPayment = null;
const celebratedPaidOrderIds = new Set();
/* True when the user is updating the PaymentMethod on an already-submitted
   order (SUBMITTED → change → PAYMENT_SELECTION). Cleared after submit. */
let pendingPaymentMethodChange = false;
/* Pre-select target when entering PAYMENT_SELECTION via the change-payment CTA. */
let paymentMethodChangePreselect = null;
/* In-flight guard for the dedicated change-payment POST. */
let paymentMethodChangeInflight = false;
/* Latest successful GET /api/track/orders/{id}?token=... payload, retained only for
   presentation (e.g. tableCode label in submitted/payment panel). Cleared when
   payment flow returns the tray to EDITING. Refresh is idempotent. */
let lastGuestTrackData = null;
const TRAY_STATUS_POLL_MS = 3500;
let bankTransferQrInflightKey = "";
let bankTransferQrCache = null;
let bankTransferQrCacheKey = "";
window.__annapBankTransferDebug = window.__annapBankTransferDebug || {};
function __annapTrayDevOn() {
    var b = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
    return window.__ANNAP_DEBUG === true || b.showBootChecklist === true;
}
function __annapTrayLog() {
    if (!__annapTrayDevOn()) return;
    try {
        window.console.log.apply(window.console, arguments);
    } catch (_e) {}
}
function __annapTrayErr() {
    if (!__annapTrayDevOn()) return;
    try {
        window.console.error.apply(window.console, arguments);
    } catch (_e2) {}
}
function __annapTrayPaymentDevLog(label, detail) {
    if (!__annapTrayDevOn()) return;
    try {
        if (detail !== undefined) window.console.log("[annap-tray-payment:" + TRAY_PAYMENT_FLOW_VERSION + "]", label, detail);
        else window.console.log("[annap-tray-payment:" + TRAY_PAYMENT_FLOW_VERSION + "]", label);
    } catch (_e) {}
}

function __annapBankTransferDebugPatch(patch) {
    if (!patch || typeof patch !== "object") return;
    const current = window.__annapBankTransferDebug || {};
    const next = { ...current, ...patch, at: new Date().toISOString() };
    window.__annapBankTransferDebug = next;
    if (__annapTrayDevOn()) __annapTrayPaymentDevLog("bank-transfer-debug", next);
}

function __annapModalDebugOn() {
    try {
        if (window.__ANNAP_DEBUG === true) return true;
        if (typeof sessionStorage !== "undefined" && sessionStorage.getItem("annap_modal_debug") === "1") return true;
    } catch (_) {}
    return false;
}
function __annapModalLog(msg, data) {
    if (!__annapModalDebugOn()) return;
    try {
        var prefix = "[ANNAP MODAL] " + msg;
        if (data !== undefined) window.console.log(prefix, data);
        else window.console.log(prefix);
    } catch (_) {}
}

/** Opt-in: set window.__ANNAP_MENU_BOOT_TRACE = true, or sessionStorage annap_menu_boot_trace=1, then reload. */
function __annapMenuBootTraceOn() {
    try {
        if (typeof window !== "undefined" && window.__ANNAP_MENU_BOOT_TRACE === true) return true;
        if (typeof window !== "undefined" && window.__ANNAP_DEBUG === true) return true;
        var b = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
        if (b.showBootChecklist === true) return true;
        if (typeof sessionStorage !== "undefined" && sessionStorage.getItem("annap_menu_boot_trace") === "1")
            return true;
    } catch (_t) {}
    return false;
}

function __annapMenuBootErr() {
    try {
        window.console.error.apply(window.console, arguments);
    } catch (_e) {}
}

function __annapMenuRuntimeJsonSelfCheck() {
    var problems = [];
    function checkJsonScript(id, label) {
        var node = document.getElementById(id);
        if (!node) {
            problems.push({ id: id, label: label, issue: "missing element" });
            return;
        }
        var raw = node.textContent || "";
        if (!String(raw).trim()) {
            problems.push({ id: id, label: label, issue: "empty body" });
            return;
        }
        try {
            JSON.parse(raw);
        } catch (e) {
            problems.push({ id: id, label: label, issue: "parse failed", message: e && e.message ? e.message : String(e) });
        }
    }
    checkJsonScript("menu-catalog-json", "catalog");
    checkJsonScript("menu-guest-context", "guest context");
    if (!problems.length) return;
    __annapMenuBootErr("[ANNAP MENU] runtime JSON invalid or missing", problems);
}
        function syncCartKey() {
            if (typeof GuestInteractionContract === "undefined") {
                VENUE_TABLE_ID = "";
                const hid0 = document.getElementById("menuVenueTableId");
                if (hid0) hid0.value = "";
                return;
            }
            var serverVtHint = String((GUEST_CTX && GUEST_CTX.venueTableId) || "").trim();
            GuestInteractionContract.bindPageContext({
                serverVt: serverVtHint,
                guestCtx: GUEST_CTX,
                migrateCatalog: {
                    catalogRow,
                    selectionFallback: () => tOrder("cart.selectionFallback")
                }
            });
            VENUE_TABLE_ID = GuestInteractionContract.getVenueTableId();
            const hid = document.getElementById("menuVenueTableId");
            if (hid) hid.value = VENUE_TABLE_ID || "";
        }

        function tOrder(k) {
            return (window.LuxuryI18n && window.LuxuryI18n.t(k)) || "";
        }

        function tfmt(path, vars) {
            if (window.LuxuryI18n && window.LuxuryI18n.tf) {
                const s = window.LuxuryI18n.tf(path, vars);
                if (s) return s;
            }
            let s = tOrder(path);
            if (!s || !vars) return s;
            for (const k of Object.keys(vars)) s = s.split("{" + k + "}").join(String(vars[k]));
            return s;
        }

        function formatTableMetaLine() {
            const lab = String((GUEST_CTX && GUEST_CTX.label) || "").trim();
            if (!VENUE_TABLE_ID && !lab) return "";
            const tableLabel = lab || trayCopy("cart.linkedTable");
            return (
                tfmt("cart.tableMetaShort", { label: tableLabel }) ||
                trayCopy("cart.tableMetaShort")
            );
        }

        function refreshTableIdentityUi() {
            syncCartKey();
            const idBlock = document.getElementById("menu-table-identity");
            const needQr = document.getElementById("menu-table-need-qr");
            const primary = document.getElementById("menu-table-primary");
            const tableMeta = document.getElementById("trayTableMeta");
            const btn = document.getElementById("menuSubmitBtn");
            const has = !!VENUE_TABLE_ID;
            if (idBlock) idBlock.classList.add("hidden");
            if (needQr) needQr.classList.toggle("hidden", has);
            if (tableMeta) {
                const meta = has ? formatTableMetaLine() : "";
                tableMeta.textContent = meta;
                tableMeta.classList.toggle("hidden", !meta);
            }
            if (btn) {
                if (isPostOrderCheckoutUi(checkoutUi) || menuOrderSubmitInFlight) {
                    btn.setAttribute("disabled", "disabled");
                } else if (has) {
                    btn.removeAttribute("disabled");
                } else {
                    btn.setAttribute("disabled", "disabled");
                }
            }
            document.querySelectorAll(".menu-add-btn").forEach((b) => {
                b.disabled = !has;
                b.classList.toggle("opacity-45", !has);
                b.classList.toggle("pointer-events-none", !has);
            });
            if (primary) {
                const lab = String(GUEST_CTX.label || "").trim();
                if (lab) {
                    const seated = tOrder("cart.seatedAt");
                    primary.textContent = seated ? `${seated} ${lab}` : lab;
                } else primary.textContent = tOrder("cart.linkedTable");
            }
            /* CTA labels are owned by renderCheckoutTray/updateCTA — never applyDom here. */
        }

        function formatMoneySafe(n) {
            const v = Number(n);
            if (!Number.isFinite(v)) return "";
            if (typeof GuestInteractionContract !== "undefined" && GuestInteractionContract && GuestInteractionContract.formatMoney) {
                try {
                    return GuestInteractionContract.formatMoney(v);
                } catch (_fmt) {}
            }
            if (window.AnnapMoney && typeof window.AnnapMoney.format === "function") {
                return window.AnnapMoney.format(v);
            }
            return String(v);
        }

        const money = { format: formatMoneySafe };

        const CATALOG = (function () {
            const node = document.getElementById("menu-catalog-json");
            if (!node) return [];
            try {
                return JSON.parse(node.textContent || "[]");
            } catch (_annap) {
                return [];
            }
        })();

        function catalogRow(id) {
            return CATALOG.find((r) => String(r.id) === String(id));
        }

        /* ── Bilingual cart-line name resolution ──────────────────────────────
           Cart lines store a `name` snapshot at the time the item is added.
           That snapshot is frozen in whatever language was active when the
           guest tapped the Add button. Without re-resolution, switching VI/EN
           after items are already in the tray leaves stale language in every
           visible row (cart, review/receipt, chip stack).

           `localizeCatalogName(id, fallback)` reads the live catalog row and
           picks the active locale's name — VI = nameVi; EN = nameEn || nameVi.
           Falls back to `name` (SSR snapshot) then to the caller-provided
           `fallback` (most often `line.name` itself) so the contract is
           "never blank".

           The corresponding C# rule lives in
           `MenuItemNameLocalizer.For(MenuItem, culture)`; keep them in sync.
        ─────────────────────────────────────────────────────────────────── */
        function getCurrentTrayLang() {
            try {
                if (window.LuxuryI18n && typeof window.LuxuryI18n.getLang === "function") {
                    return window.LuxuryI18n.getLang() === "vi" ? "vi" : "en";
                }
            } catch (_lt) {}
            return "vi";
        }

        function localizeCatalogName(id, fallback) {
            const row = catalogRow(id);
            if (!row) return fallback != null ? String(fallback) : "";
            const lang = getCurrentTrayLang();
            /* nameVi is always present on catalog rows but is unsafe to assume;
               fall back to the SSR `name` (current-locale snapshot) when the
               dedicated VI column is missing. */
            let vi = "";
            if (row.nameVi != null && String(row.nameVi).trim()) vi = String(row.nameVi);
            else if (row.name != null && String(row.name).trim()) vi = String(row.name);
            /* nameEn is optional; treat null / "" / whitespace as missing and
               fall back to Vietnamese per MenuItemNameLocalizer.For. */
            let en = "";
            if (row.nameEn != null) {
                const s = String(row.nameEn);
                if (s && s.trim()) en = s.trim();
            }
            if (lang === "vi") return vi || (fallback != null ? String(fallback) : "");
            return en || vi || (fallback != null ? String(fallback) : "");
        }

        function localizeLineName(line) {
            if (!line) return "";
            const fb = line.name != null ? String(line.name) : "";
            return localizeCatalogName(line.id, fb);
        }

        /* Detail poster URL lookup — server-resolved via MenuMediaResolver.TryResolveDetailPosterUrl.
           Used only by the pointerdown preload below; never used to mutate the modal or API flow. */
        const DETAIL_IMAGE_BY_DRINK_ID = (function () {
            const map = new Map();
            if (!Array.isArray(CATALOG)) return map;
            for (const row of CATALOG) {
                if (!row || row.id == null) continue;
                const url = typeof row.detailImage === "string" ? row.detailImage : "";
                if (url) map.set(String(row.id), url);
            }
            return map;
        })();

        /* Preload dedup — set of URLs already started in this session.
           Image objects are NOT retained; the browser HTTP cache serves repeat requests. */
        const _preloadedDetailImages = new Set();

        function preloadDrinkDetailImage(drinkId) {
            if (!drinkId) return;
            const url = DETAIL_IMAGE_BY_DRINK_ID.get(String(drinkId));
            if (!url) return;
            if (_preloadedDetailImages.has(url)) return;
            _preloadedDetailImages.add(url);
            try {
                const img = new Image();
                img.src = url;
                /* No event listeners — let the browser decide cache/revalidation. */
            } catch (_pe) {
                /* ignore */
            }
        }

        function cartLineKey(l) {
            const gl = (l.guestLabel && String(l.guestLabel).trim()) || "";
            const menuId =
                l.menuItemId != null && l.menuItemId !== ""
                    ? l.menuItemId
                    : l.id != null && l.id !== ""
                      ? l.id
                      : "";
            const temp = (l.temperature && String(l.temperature).trim()) || "";
            return String(menuId) + "\u0001" + gl + "\u0001" + temp;
        }

        const cartItems = new Map(); /* line key -> { id, guestLabel, temperature, name, unitPrice, qty } */

        const orderTrayRoot = document.getElementById("order-tray-root");
        const orderTrayBackdrop = document.getElementById("order-tray-backdrop");
        const orderTraySheet = document.getElementById("order-tray-sheet");
        const orderTrayChip = document.getElementById("order-tray-chip");
        const orderTrayChevron = orderTrayRoot ? orderTrayRoot.querySelector(".order-tray-chevron") : null;
        const orderTrayHeader = orderTrayRoot ? orderTrayRoot.querySelector(".annap-tray-header") : null;
        const orderTrayImpact = document.getElementById("order-tray-chip-impact");
        if (orderTrayChip) orderTrayChip.setAttribute("data-cart-target", "order-tray");
        if (window.AddToOrderAnimationProvider) {
            window.AddToOrderAnimationProvider.registerCartTarget(orderTrayImpact || orderTrayChip);
        }
        let trayOpen = false;
        let trayDrag = null;
        function linesToCartMap(lines) {
            cartItems.clear();
            for (const l of lines) {
                const gl = (l.guestLabel && String(l.guestLabel).trim()) || "";
                const temp = (l.temperature && String(l.temperature).trim()) || "";
                cartItems.set(cartLineKey(l), {
                    id: l.menuItemId,
                    guestLabel: gl,
                    temperature: temp,
                    name: l.name,
                    unitPrice: l.unitPrice,
                    qty: l.qty,
                    imageSrc: l.imageSrc || "",
                    customerNote: l.customerNote && String(l.customerNote).trim() ? String(l.customerNote).trim() : ""
                });
            }
            /* The persisted `name` is a language snapshot from the time the
               line was added. After a client-side language switch we
               re-resolve each line's name from the active-locale catalog
               (nameVi / nameEn) so every visual surface (cart, receipt, chip)
               reads in the current language. The persisted value remains the
               safe fallback when the catalog row cannot be found. The
               override is presentation-only — we deliberately do NOT write
               the result back to localStorage because setCartLines triggers
               annap:guest-interaction, which would re-enter this function
               and infinite-loop. The persisted snapshot is refreshed in
               place only for items where the catalog gives us a different
               answer than the snapshot, so the next reload already starts in
               the latest locale chosen at submit time / page exit. */
            cartItems.forEach(function (line, key) {
                const fb = line.name != null ? String(line.name) : "";
                const resolved = localizeCatalogName(line.id, fb);
                if (resolved) line.name = resolved;
            });
        }

        function persistLocalizedCartNames() {
            try {
                if (typeof GuestInteractionContract === "undefined" || !GuestInteractionContract) return;
                if (typeof GuestInteractionContract.setCartLines !== "function") return;
                var all = Array.from(cartItems.values()).map(function (v) {
                    return {
                        menuItemId: v.id,
                        name: v.name,
                        unitPrice: v.unitPrice,
                        qty: v.qty,
                        guestLabel: v.guestLabel || "",
                        imageSrc: v.imageSrc || "",
                        customerNote: v.customerNote || "",
                        temperature: v.temperature || ""
                    };
                });
                GuestInteractionContract.setCartLines(all);
            } catch (_persist) {
                /* ignore */
            }
        }

        function loadCart() {
            if (typeof GuestInteractionContract === "undefined") {
                cartItems.clear();
                return;
            }
            syncCartKey();
            linesToCartMap(GuestInteractionContract.getCartLines());
        }

        function imageFromSource(el) {
            if (!el) return "";
            const ds = el.dataset || {};
            if (ds.itemImage) return String(ds.itemImage).trim();
            const root = el.closest ? el.closest("[data-drink-card], .ge-result-card, .ge-disc-rec, .dd-pairing, .dd-product-card, .dd-passport, article, section") : null;
            const img = root && root.querySelector ? root.querySelector("img") : null;
            if (img) return String(img.currentSrc || img.getAttribute("src") || "").trim();
            const detailImg = document.querySelector("#drink-detail-mount .dd-product__img");
            return detailImg ? String(detailImg.currentSrc || detailImg.getAttribute("src") || "").trim() : "";
        }

        function lineFromSource(menuItemId, el) {
            const ds = el && el.dataset;
            const name = ds && ds.itemName != null ? String(ds.itemName).trim() : "";
            const priceRaw = ds ? ds.itemPrice : void 0;
            const price = priceRaw != null && priceRaw !== "" ? Number(priceRaw) : NaN;
            const imageSrc = imageFromSource(el);
            if (name && Number.isFinite(price)) {
                return { id: menuItemId, name: name, unitPrice: price, qty: 1, imageSrc };
            }
            const row = catalogRow(menuItemId);
            /* Resolve the active-locale name from the catalog (nameVi / nameEn).
               The button's data-item-name is the server-rendered name for the
               request culture; after a client-side language switch it can be
               stale. Falling back to `localizeCatalogName` keeps brand-new
               items consistent with the language the guest is currently using.
               Unit price is locale-independent so catalogRow.price is fine. */
            const localized = row ? localizeCatalogName(menuItemId, row && row.name != null ? String(row.name) : "") : "";
            return {
                id: menuItemId,
                name: localized || (tOrder("cart.selectionFallback")),
                unitPrice: Number(row && row.price != null ? row.price : 0),
                qty: 1,
                imageSrc
            };
        }

        function setTrayOpenImmediate(open) {
            const next = !!open;
            const useBodyLock =
                typeof window.matchMedia === "function" && window.matchMedia("(min-width: 640px)").matches;

            if (next && !trayOpen && useBodyLock) {
                window.AnnapScrollLock && window.AnnapScrollLock.lock();
            }
            if (!next && trayOpen) {
                window.AnnapScrollLock && window.AnnapScrollLock.unlock();
            }

            trayOpen = next;
            if (orderTrayRoot) orderTrayRoot.classList.toggle("is-open", trayOpen);
            if (orderTraySheet) {
                orderTraySheet.classList.toggle("order-tray-sheet--closed", !trayOpen);
                orderTraySheet.classList.toggle("pointer-events-none", !trayOpen);
                orderTraySheet.classList.toggle("pointer-events-auto", trayOpen);
            }
            if (orderTrayChip) orderTrayChip.setAttribute("aria-expanded", trayOpen ? "true" : "false");
            if (orderTrayRoot) orderTrayRoot.classList.toggle("order-tray-root--sheet-open", trayOpen);
            if (orderTrayBackdrop) {
                if (trayOpen) {
                    orderTrayBackdrop.classList.remove("hidden", "pointer-events-none", "opacity-0");
                    orderTrayBackdrop.classList.add("pointer-events-auto", "opacity-100", "is-visible");
                    orderTrayBackdrop.setAttribute("aria-hidden", "false");
                } else {
                    orderTrayBackdrop.classList.add("hidden", "pointer-events-none", "opacity-0");
                    orderTrayBackdrop.classList.remove("pointer-events-auto", "opacity-100", "is-visible");
                    orderTrayBackdrop.setAttribute("aria-hidden", "true");
                }
            }
        }

        function setTrayOpen(open, opts) {
            opts = opts || {};
            if (open && !opts.userIntent) {
                return;
            }
            setTrayOpenImmediate(!!open);
        }

        function toggleTray() {
            setTrayOpen(!trayOpen, { userIntent: true });
        }

        if (orderTrayChip) orderTrayChip.addEventListener("click", function () {
            toggleTray();
        });
        if (orderTrayBackdrop) orderTrayBackdrop.addEventListener("click", function () {
            setTrayOpen(false, { userIntent: true });
        });

        const trayCloseBtn = document.getElementById("annap-tray-close");
        if (trayCloseBtn && !trayCloseBtn.dataset.annapBound) {
            trayCloseBtn.dataset.annapBound = "1";
            trayCloseBtn.addEventListener("click", function (ev) {
                ev.preventDefault();
                ev.stopPropagation();
                setTrayOpen(false, { userIntent: true });
            });
        }

        function trayIsMobileSheet() {
            try {
                return typeof window.matchMedia === "function" && window.matchMedia("(max-width: 640px)").matches;
            } catch (_tm) {
                return false;
            }
        }

        function resetTrayDragStyles() {
            if (!orderTraySheet) return;
            orderTraySheet.classList.remove("order-tray-sheet--dragging");
            orderTraySheet.style.removeProperty("--order-tray-drag-y");
        }

        function bindTraySwipeToClose() {
            if (!orderTrayHeader || !orderTraySheet) return;
            if (orderTrayHeader.dataset.annapSwipeCloseBound === "1") return;
            orderTrayHeader.dataset.annapSwipeCloseBound = "1";

            orderTrayHeader.addEventListener("pointerdown", function (ev) {
                if (!trayOpen || !trayIsMobileSheet()) return;
                if (ev.button !== 0 || ev.isPrimary === false) return;
                const target = ev.target;
                if (target && target.closest && target.closest("button,a,input,select,textarea")) return;

                trayDrag = {
                    id: ev.pointerId,
                    x: ev.clientX,
                    y: ev.clientY,
                    lastY: ev.clientY,
                    startedAt: Date.now()
                };
                orderTraySheet.classList.add("order-tray-sheet--dragging");
                try {
                    orderTrayHeader.setPointerCapture(ev.pointerId);
                } catch (_pc) {
                    /* ignore */
                }
            }, { passive: true });

            orderTrayHeader.addEventListener("pointermove", function (ev) {
                if (!trayDrag || ev.pointerId !== trayDrag.id || !orderTraySheet) return;
                const dy = Math.max(0, ev.clientY - trayDrag.y);
                const dx = Math.abs(ev.clientX - trayDrag.x);
                if (dx > 56 && dx > dy * 1.2) return;
                trayDrag.lastY = ev.clientY;
                orderTraySheet.style.setProperty("--order-tray-drag-y", Math.min(96, dy).toFixed(0) + "px");
                if (dy > 6) {
                    try { ev.preventDefault(); } catch (_pd) { /* ignore */ }
                }
            }, { passive: false });

            function finish(ev) {
                if (!trayDrag || ev.pointerId !== trayDrag.id) return;
                const dy = ev.clientY - trayDrag.y;
                const elapsed = Math.max(1, Date.now() - trayDrag.startedAt);
                const velocity = dy / elapsed;
                const shouldClose = dy > 58 || (dy > 34 && velocity > 0.45);
                trayDrag = null;
                resetTrayDragStyles();
                if (shouldClose) setTrayOpen(false, { userIntent: true });
            }

            orderTrayHeader.addEventListener("pointerup", finish, { passive: true });
            orderTrayHeader.addEventListener("pointercancel", function (ev) {
                if (!trayDrag || ev.pointerId !== trayDrag.id) return;
                trayDrag = null;
                resetTrayDragStyles();
            }, { passive: true });
        }

        bindTraySwipeToClose();
        /* Closed tray: backdrop is full-viewport; setTrayOpen(false) is a no-op when trayOpen is
           already false — must call setTrayOpenImmediate so backdrop releases hit-testing immediately. */
        try {
            setTrayOpenImmediate(false);
        } catch (_trayInit) {}
        try {
            syncCartKey();
            refreshTableIdentityUi();
        } catch (_earlyTray) {
            /* GuestInteractionContract or tray DOM not ready yet — bootMenuTray will retry. */
        }

        let _annapMenuActiveCatId = null;

        function setActiveCategory(catId) {
            if (catId != null && String(catId) === String(_annapMenuActiveCatId)) return;
            _annapMenuActiveCatId = catId != null ? String(catId) : null;
            document.querySelectorAll("[data-cat-nav]").forEach((btn) => {
                const on = String(btn.getAttribute("data-cat-nav") || "") === String(catId != null ? catId : "");
                btn.classList.toggle("is-active", on);
                btn.classList.toggle("text-[rgb(var(--fg))]", on);
                btn.classList.toggle("ring-[rgb(var(--accent))]/35", on);
                btn.classList.toggle("bg-white/[0.06]", on);
                btn.classList.toggle("shadow-[0_0_0_1px_rgba(199,168,122,0.2)]", on);
            });
        }

        /* ── Editorial scroll engine ────────────────────────────────────────────────
           Category navigation must feel like drifting into the next section of a
           tasting journal — not snapping to a filter tab. Native scrollIntoView uses
           browser-controlled timing that reads as mechanical and app-like.

           Easing: cubic-bezier(0.16, 1, 0.3, 1) — doctrine --ease-arrive curve.
           The section arrives quickly (~88% of distance in first 30% of time), then
           the view breathes and settles gently for the remaining duration.

           Duration: 880ms for vertical section scroll (Ceremonial tier — scene arrival).
           Duration: 360ms for horizontal chip rail centering (Brief/Standard).
        ─────────────────────────────────────────────────────────────────────────── */
        var _annapEaseArrive = (function () {
            var p1x = 0.16, p1y = 1.0, p2x = 0.3, p2y = 1.0;
            var c1x = 3 * p1x;
            var c2x = 3 * (p2x - p1x) - c1x;
            var c3x = 1 - c1x - c2x;
            var c1y = 3 * p1y;
            var c2y = 3 * (p2y - p1y) - c1y;
            var c3y = 1 - c1y - c2y;
            function sx(t) { return ((c3x * t + c2x) * t + c1x) * t; }
            function sy(t) { return ((c3y * t + c2y) * t + c1y) * t; }
            function dsx(t) { return (3 * c3x * t + 2 * c2x) * t + c1x; }
            return function (x) {
                if (x <= 0) return 0;
                if (x >= 1) return 1;
                var t = x;
                for (var i = 0; i < 8; i++) {
                    var slope = dsx(t);
                    if (Math.abs(slope) < 0.000001) break;
                    t -= (sx(t) - x) / slope;
                }
                return sy(t);
            };
        }());

        var _annapSectionRaf = 0;

        function annapEditorialScrollTo(targetY, durationMs) {
            if (_annapSectionRaf) { window.cancelAnimationFrame(_annapSectionRaf); _annapSectionRaf = 0; }
            var startY = window.scrollY !== undefined ? window.scrollY : (document.documentElement.scrollTop || 0);
            var dist = targetY - startY;
            if (Math.abs(dist) < 2) return;
            var t0 = 0;
            function step(ts) {
                if (!t0) t0 = ts;
                var p = Math.min((ts - t0) / durationMs, 1);
                window.scrollTo(0, startY + dist * _annapEaseArrive(p));
                if (p < 1) {
                    _annapSectionRaf = window.requestAnimationFrame(step);
                } else {
                    _annapSectionRaf = 0;
                }
            }
            _annapSectionRaf = window.requestAnimationFrame(step);
        }

        var _annapRailRaf = 0;

        // Exposed for the chip rail horizontal scroll in Menu/Index.cshtml inline script.
        window.annapEditorialScrollH = function (el, targetLeft, durationMs) {
            if (_annapRailRaf) { window.cancelAnimationFrame(_annapRailRaf); _annapRailRaf = 0; }
            var startLeft = el.scrollLeft;
            var dist = targetLeft - startLeft;
            if (Math.abs(dist) < 2) return;
            var t0 = 0;
            function step(ts) {
                if (!t0) t0 = ts;
                var p = Math.min((ts - t0) / durationMs, 1);
                el.scrollLeft = startLeft + dist * _annapEaseArrive(p);
                if (p < 1) {
                    _annapRailRaf = window.requestAnimationFrame(step);
                } else {
                    _annapRailRaf = 0;
                }
            }
            _annapRailRaf = window.requestAnimationFrame(step);
        };

        window.addEventListener(
            "pagehide",
            function () {
                if (_annapSectionRaf) {
                    window.cancelAnimationFrame(_annapSectionRaf);
                    _annapSectionRaf = 0;
                }
                if (_annapRailRaf) {
                    window.cancelAnimationFrame(_annapRailRaf);
                    _annapRailRaf = 0;
                }
            },
            { passive: true }
        );

        /**
         * Jump to a menu category using editorial RAF scroll with the doctrine
         * --ease-arrive curve. Reads scroll-margin-top from computed styles so
         * the sticky category rail is always respected.
         */
        function scrollToCategory(catId) {
            const raw = catId != null ? String(catId) : "";
            if (!raw) return;
            const esc = typeof CSS !== "undefined" && CSS.escape ? CSS.escape(raw) : raw.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
            const el = document.querySelector(`[data-cat-section="${esc}"]`);
            if (!el) return;
            const reduceMotion =
                typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
            const coarsePointer =
                typeof window.matchMedia === "function" &&
                (window.matchMedia("(hover: none)").matches || window.matchMedia("(pointer: coarse)").matches);
            if (reduceMotion || coarsePointer) {
                try { el.scrollIntoView({ block: "start", inline: "nearest", behavior: "auto" }); } catch (_e) { try { el.scrollIntoView(true); } catch (_e2) { /* ignore */ } }
                return;
            }
            const rect = el.getBoundingClientRect();
            const scrollY = window.scrollY !== undefined ? window.scrollY : (document.documentElement.scrollTop || 0);
            const absoluteTop = rect.top + scrollY;
            let scrollMarginTop = 0;
            try { scrollMarginTop = parseFloat(window.getComputedStyle(el).scrollMarginTop) || 0; } catch (_m) {}
            annapEditorialScrollTo(absoluteTop - scrollMarginTop, 880);
        }

        document.querySelectorAll("[data-cat-nav]").forEach((btn) => {
            btn.addEventListener("click", function (ev) {
                ev.preventDefault();
                const id = btn.getAttribute("data-cat-nav");
                if (id) {
                    setActiveCategory(id);
                    scrollToCategory(id);
                }
            });
        });

        const sections = Array.from(document.querySelectorAll("[data-cat-section]"));
        if (sections.length) {
            if (!annapBootOff("disableGuestObservers") && typeof IntersectionObserver !== "undefined") {
                const coarseIo =
                    typeof window.matchMedia === "function" &&
                    (window.matchMedia("(hover: none)").matches || window.matchMedia("(pointer: coarse)").matches);
                let catObsRaf = 0;
                let catObsPendingId = null;
                const io = new IntersectionObserver(
                    (entries) => {
                        const visible = entries
                            .filter((e) => e.isIntersecting)
                            .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
                        if (!visible || !visible.target) return;
                        const id = visible.target.getAttribute("data-cat-section");
                        if (!id) return;
                        catObsPendingId = id;
                        if (catObsRaf) return;
                        catObsRaf = window.requestAnimationFrame(function () {
                            catObsRaf = 0;
                            const pid = catObsPendingId;
                            catObsPendingId = null;
                            if (pid) setActiveCategory(pid);
                        });
                    },
                    coarseIo
                        ? { root: null, rootMargin: "-14% 0px -22% 0px", threshold: [0.03, 0.08, 0.14, 0.22] }
                        : { root: null, rootMargin: "-38% 0px -45% 0px", threshold: [0.08, 0.15, 0.25, 0.4] }
                );
                sections.forEach((s) => io.observe(s));
                if (window.AnnapGuestBootHarness) AnnapGuestBootHarness.row("observers", "ok");
            } else {
                if (window.AnnapGuestBootHarness) AnnapGuestBootHarness.markSkipped("observers");
            }
            const first = sections[0].getAttribute("data-cat-section");
            if (first) setActiveCategory(first);
        }

        function totalQty() {
            let n = 0;
            for (const l of cartItems.values()) n += l.qty;
            return n;
        }

        function subtotal() {
            let s = 0;
            for (const l of cartItems.values()) s += l.qty * l.unitPrice;
            return s;
        }

        function isSommelierFlowActive() {
            return !!(document.body && document.body.classList.contains("ge-sommelier-flow"));
        }

        function reducedMotionTray() {
            try {
                return (
                    document.documentElement.classList.contains("annap-guest-no-motion") ||
                    (typeof window.matchMedia === "function" &&
                        window.matchMedia("(prefers-reduced-motion: reduce)").matches)
                );
            } catch (_rm) {
                return false;
            }
        }

        function trayCopy(key) {
            return tOrder(key) || "";
        }

        function readSubmittedSession() {
            if (typeof GuestInteractionContract === "undefined") return null;
            return GuestInteractionContract.readGuestOrderSession();
        }

        function buildTrackHref(sess) {
            if (!sess) return "";
            if (typeof GuestInteractionContract.buildGuestTrackUrl === "function") {
                return GuestInteractionContract.buildGuestTrackUrl(sess.orderId, sess.token);
            }
            return `/track/${encodeURIComponent(sess.orderId)}?token=${encodeURIComponent(sess.token)}`;
        }

        function buildGuestStatusApiPath(sess) {
            if (!sess) return "";
            if (typeof GuestInteractionContract.buildGuestTrackApiUrl === "function") {
                return GuestInteractionContract.buildGuestTrackApiUrl(sess.orderId, sess.token);
            }
            return `/api/track/orders/${encodeURIComponent(sess.orderId)}?token=${encodeURIComponent(sess.token)}`;
        }

        function fetchGuestOrderStatus(sess) {
            const path = buildGuestStatusApiPath(sess);
            if (!path) return Promise.reject(new Error("missing session"));
            const url = typeof window.__annapApiUrl === "function" ? window.__annapApiUrl(path) : path;
            return fetch(url, {
                headers: { Accept: "application/json" },
                cache: "no-store"
            });
        }

        function resolveSubmittedCounterState(pm) {
            const method = String(pm || "").trim();
            if (method === PAYMENT_METHOD.CARD) return TRAY_STATE.SUBMITTED_CARD;
            if (method === PAYMENT_METHOD.CASH || method === PAYMENT_METHOD.LEGACY_CASH_CARD) return TRAY_STATE.SUBMITTED_CASH;
            if (method === PAYMENT_METHOD.BANK) return TRAY_STATE.SUBMITTED_BANK;
            return TRAY_STATE.SUBMITTED_COUNTER;
        }

        function isPostOrderCheckoutUi(ui) {
            return (
                ui === CHECKOUT_UI.PAYMENT_BANK_LOADING ||
                ui === CHECKOUT_UI.PAYMENT_BANK_READY ||
                ui === CHECKOUT_UI.SUBMITTED ||
                ui === CHECKOUT_UI.PAID ||
                ui === CHECKOUT_UI.COMPLETED
            );
        }

        function isBankCheckoutUi(ui) {
            return ui === CHECKOUT_UI.PAYMENT_BANK_LOADING || ui === CHECKOUT_UI.PAYMENT_BANK_READY;
        }

        function domainStateFromCheckoutUi(ui) {
            if (ui === CHECKOUT_UI.REVIEW) return TRAY_STATE.REVIEW;
            if (ui === CHECKOUT_UI.FULFILLMENT_MODE) return TRAY_STATE.REVIEW;
            if (ui === CHECKOUT_UI.PAYMENT_SELECTION) return TRAY_STATE.PAYMENT_CHOICE;
            if (isBankCheckoutUi(ui)) return TRAY_STATE.SUBMITTED_BANK;
            if (ui === CHECKOUT_UI.SUBMITTED) {
                const sess = readSubmittedSession();
                return resolveSubmittedCounterState(sess && sess.paymentMethod);
            }
            if (ui === CHECKOUT_UI.PAID) return TRAY_STATE.PAID;
            if (ui === CHECKOUT_UI.COMPLETED) return TRAY_STATE.COMPLETED;
            if (menuOrderSubmitInFlight) return TRAY_STATE.SUBMITTING;
            const n = totalQty();
            if (n > 0) return TRAY_STATE.EDITING;
            return TRAY_STATE.EMPTY;
        }

        function resolveTrayState() {
            return domainStateFromCheckoutUi(checkoutUi);
        }

        function checkoutUiFromSession() {
            const sess = readSubmittedSession();
            if (!sess || !sess.orderId || !sess.token) {
                return CHECKOUT_UI.EDITING;
            }
            const st = traySubmittedStatus || sess.status || TRAY_STATE.SUBMITTED_PENDING;
            if (st === TRAY_STATE.COMPLETED || st === "completed") return CHECKOUT_UI.COMPLETED;
            if (st === TRAY_STATE.PAID || st === "paid") return CHECKOUT_UI.PAID;
            const pm = String(sess.paymentMethod || "").trim();
            if (pm === PAYMENT_METHOD.BANK || st === TRAY_STATE.SUBMITTED_BANK) {
                if (bankTransferQrCache && bankTransferQrCacheKey === `${sess.orderId || ""}|${sess.token || ""}`) {
                    return CHECKOUT_UI.PAYMENT_BANK_READY;
                }
                return CHECKOUT_UI.PAYMENT_BANK_LOADING;
            }
            return CHECKOUT_UI.SUBMITTED;
        }

        function statusFingerprint(sess, status, pending) {
            return [
                sess && sess.orderId ? String(sess.orderId) : "",
                status || "",
                pending === true ? "1" : pending === false ? "0" : "x",
                sess && sess.paymentMethod ? String(sess.paymentMethod) : ""
            ].join("|");
        }

        function setCheckoutUi(next, opts) {
            opts = opts || {};
            const allowed = Object.values(CHECKOUT_UI);
            if (allowed.indexOf(next) < 0) next = CHECKOUT_UI.EDITING;
            const prev = checkoutUi;
            checkoutUi = next;
            if (next === CHECKOUT_UI.REVIEW) selectedPaymentMethod = PAYMENT_METHOD.CASH;
            if (next === CHECKOUT_UI.REVIEW) selectedFulfillmentMode = null;
            if (next === CHECKOUT_UI.PAYMENT_SELECTION && typeof refreshBankTransferAvailability === "function") {
                refreshBankTransferAvailability();
            }
            renderCheckoutTray(opts);
            if ((next === CHECKOUT_UI.PAYMENT_SELECTION || next === CHECKOUT_UI.FULFILLMENT_MODE) && prev !== next) {
                window.requestAnimationFrame(function () {
                    scrollCheckoutCtaIntoView();
                });
            }
        }

        function isSubmittedTrayState(state) {
            return (
                state === TRAY_STATE.SUBMITTED_PENDING ||
                state === TRAY_STATE.SUBMITTED_COUNTER ||
                state === TRAY_STATE.SUBMITTED_CASH ||
                state === TRAY_STATE.SUBMITTED_CARD ||
                state === TRAY_STATE.SUBMITTED_BANK ||
                state === TRAY_STATE.PAID ||
                state === TRAY_STATE.COMPLETED
            );
        }

        function restoreSubmitButtonState(btn, prepLabel) {
            if (!btn) return;
            btn.classList.remove("order-tray-submit--preparing", "order-tray-submit--success", "order-tray-submit--pulse");
            btn.removeAttribute("disabled");
            if (prepLabel) btn.textContent = prepLabel;
        }

        function renderInlineTransferFallback(host, qr, options) {
            options = options || {};
            const q = qr || {};
            const amount = q.amountFormatted || q.amount || "—";
            const memo = q.memo || "—";
            const bankName = q.bankName || "—";
            const account = q.accountNumber || "—";
            const holder = q.accountName || "—";
            const qrImage = q.qrImageUrl ? String(q.qrImageUrl) : "";
            const warningLine = options.showWarning
                ? `<p class="order-tray-submitted-card__note">${escapeHtml(
                      trayCopy("checkout.transferQrLoadFailed")
                  )}</p>`
                : "";
            host.innerHTML = `<div class="guest-bank-transfer guest-bank-transfer--tray" role="status">
                ${
                    qrImage
                        ? `<div class="guest-bank-transfer__qr-wrap">
                    <img class="guest-bank-transfer__qr" src="${escapeHtml(qrImage)}" alt="${escapeHtml(
                              trayCopy("checkout.transferQrAlt")
                          )}" loading="eager" decoding="async" referrerpolicy="no-referrer" />
                    <p class="guest-bank-transfer__qr-fallback hidden" role="status">${escapeHtml(
                        trayCopy("checkout.transferQrLoadFailed")
                    )}</p>
                </div>`
                        : ""
                }
                <p class="guest-bank-transfer__amount">${escapeHtml(String(amount))}</p>
                <div class="guest-bank-transfer__keep-open" role="status">
                    <p class="guest-bank-transfer__keep-open-title">${escapeHtml(
                        trayCopy("checkout.bankTransferKeepOpen")
                    )}</p>
                    <p class="guest-bank-transfer__keep-open-sub">${escapeHtml(
                        trayCopy("checkout.bankTransferStaffConfirmNote")
                    )}</p>
                </div>
                <dl class="guest-bank-transfer__meta">
                    <div><dt>${escapeHtml(trayCopy("checkout.transferMemo"))}</dt><dd class="guest-bank-transfer__memo">${escapeHtml(
                        String(memo)
                    )}</dd></div>
                    <div><dt>${escapeHtml(trayCopy("checkout.transferBank"))}</dt><dd>${escapeHtml(
                        String(bankName)
                    )}</dd></div>
                    <div><dt>${escapeHtml(trayCopy("checkout.transferAccountNumber"))}</dt><dd>${escapeHtml(
                        String(account)
                    )}</dd></div>
                    <div><dt>${escapeHtml(trayCopy("checkout.transferAccountHolder"))}</dt><dd>${escapeHtml(
                        String(holder)
                    )}</dd></div>
                </dl>
                <div class="guest-bank-transfer__actions">
                    <button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-memo="${escapeHtml(String(memo))}">${escapeHtml(
                        trayCopy("checkout.copyMemo")
                    )}</button>
                    <button type="button" class="guest-bank-transfer__copy guest-hit" data-copy-amount="${escapeHtml(
                        String(q.amount || "")
                    )}">${escapeHtml(trayCopy("checkout.copyAmount"))}</button>
                </div>
                ${warningLine}
            </div>`;
            const img = host.querySelector(".guest-bank-transfer__qr");
            const fallback = host.querySelector(".guest-bank-transfer__qr-fallback");
            if (img && fallback) {
                img.addEventListener("load", function () {
                    __annapBankTransferDebugPatch({
                        qrImageLoadState: "loaded",
                        qrImageNaturalWidth: img.naturalWidth || 0,
                        qrImageNaturalHeight: img.naturalHeight || 0
                    });
                });
                img.addEventListener("error", function () {
                    img.classList.add("hidden");
                    fallback.classList.remove("hidden");
                    __annapBankTransferDebugPatch({
                        qrImageLoadState: "error",
                        qrImageNaturalWidth: img.naturalWidth || 0,
                        qrImageNaturalHeight: img.naturalHeight || 0
                    });
                });
            }
            host.querySelectorAll("[data-copy-memo]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    if (window.GuestBankTransfer && typeof window.GuestBankTransfer.copyText === "function")
                        void window.GuestBankTransfer.copyText(btn.getAttribute("data-copy-memo"), null);
                });
            });
            host.querySelectorAll("[data-copy-amount]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    if (window.GuestBankTransfer && typeof window.GuestBankTransfer.copyText === "function")
                        void window.GuestBankTransfer.copyText(btn.getAttribute("data-copy-amount"), null);
                });
            });
        }

        function renderTransferFatalFallback(host, onRetry) {
            host.innerHTML = `<div class="guest-bank-transfer guest-bank-transfer--tray" role="alert">
                <p class="guest-bank-transfer__message">${escapeHtml(
                    trayCopy("checkout.transferUnavailableInTray")
                )}</p>
                <div class="guest-bank-transfer__actions">
                    <button type="button" class="guest-bank-transfer__copy guest-hit" data-bt-retry="1">${escapeHtml(
                        trayCopy("checkout.transferRetry")
                    )}</button>
                </div>
            </div>`;
            const retryBtn = host.querySelector("[data-bt-retry]");
            if (retryBtn && typeof onRetry === "function") {
                retryBtn.addEventListener("click", function (ev) {
                    ev.preventDefault();
                    onRetry();
                });
            }
        }

        function transferHostEl() {
            return document.getElementById("order-tray-transfer-host");
        }

        function paintQrIntoHost(host, qr, opts) {
            if (!host) return false;
            if (qr && window.GuestBankTransfer && typeof window.GuestBankTransfer.mountTransferCard === "function") {
                window.GuestBankTransfer.mountTransferCard(host, qr, opts || { compact: true });
            } else if (qr) {
                renderInlineTransferFallback(host, qr, opts);
            } else {
                return false;
            }
            return !!host.querySelector(".guest-bank-transfer");
        }

        function ensureBankTransferQrMounted(sess) {
            if (!sess || String(sess.paymentMethod || "").trim() !== PAYMENT_METHOD.BANK) return Promise.resolve(false);
            if (!isBankCheckoutUi(checkoutUi)) return Promise.resolve(false);
            const cacheKey = `${sess.orderId || ""}|${sess.token || ""}`;
            const host0 = transferHostEl();
            __annapBankTransferDebugPatch({
                appVersion: TRAY_PAYMENT_FLOW_VERSION,
                checkoutUi: checkoutUi,
                submittedStatus: traySubmittedStatus || sess.status || "",
                paymentMethod: sess.paymentMethod || "",
                orderId: sess.orderId || "",
                hasGuestToken: !!sess.token,
                qrMountFound: !!host0
            });
            if (!host0 || !sess.orderId || !sess.token) return Promise.resolve(false);

            function mountCurrent(qr) {
                const host = transferHostEl();
                if (!host) return false;
                if (qr) {
                    const ok = paintQrIntoHost(host, qr, { compact: true });
                    if (ok && checkoutUi === CHECKOUT_UI.PAYMENT_BANK_LOADING) {
                        checkoutUi = CHECKOUT_UI.PAYMENT_BANK_READY;
                        updateCTA();
                        updatePanelVisibility();
                    }
                    __annapBankTransferDebugPatch({
                        qrRenderCalled: true,
                        qrCardInserted: ok,
                        qrFetchStatus: "ok"
                    });
                    return ok;
                }
                renderTransferFatalFallback(host, function () {
                    bankTransferQrInflightKey = "";
                    void ensureBankTransferQrMounted(sess);
                });
                return false;
            }

            if (bankTransferQrCache && bankTransferQrCacheKey === cacheKey) {
                mountCurrent(bankTransferQrCache);
                return Promise.resolve(true);
            }
            if (bankTransferQrInflightKey === cacheKey) return Promise.resolve(true);

            host0.innerHTML =
                '<p class="order-tray-submitted-card__note">' +
                escapeHtml(trayCopy("checkout.transferQrLoading")) +
                "</p>";
            bankTransferQrInflightKey = cacheKey;
            if (checkoutUi !== CHECKOUT_UI.PAYMENT_BANK_READY) checkoutUi = CHECKOUT_UI.PAYMENT_BANK_LOADING;
            __annapBankTransferDebugPatch({ qrFetchStarted: true });

            const fetchQr =
                window.GuestBankTransfer && typeof window.GuestBankTransfer.fetchTransferQr === "function"
                    ? window.GuestBankTransfer.fetchTransferQr(sess.orderId, sess.token)
                    : Promise.resolve(null);

            return fetchQr
                .then(function (qr) {
                    __annapBankTransferDebugPatch({
                        qrFetchStatus: "200",
                        qrFetchBodyHasQrImageUrl: !!(qr && qr.qrImageUrl)
                    });
                    if (qr) {
                        bankTransferQrCache = qr;
                        bankTransferQrCacheKey = cacheKey;
                    }
                    return mountCurrent(qr);
                })
                .catch(function (err) {
                    __annapBankTransferDebugPatch({
                        lastError: String(err && err.message ? err.message : err),
                        qrFetchStatus: "error"
                    });
                    const fallbackPath = `/api/track/orders/${encodeURIComponent(sess.orderId)}?token=${encodeURIComponent(sess.token)}`;
                    const fallbackUrl =
                        typeof window.__annapApiUrl === "function" ? window.__annapApiUrl(fallbackPath) : fallbackPath;
                    return fetch(fallbackUrl, { headers: { Accept: "application/json" }, cache: "no-store" })
                        .then(function (res) {
                            if (!res.ok) throw new Error("fallback track fetch failed");
                            return res.json();
                        })
                        .then(function (track) {
                            const qr = track && track.transferQr ? track.transferQr : null;
                            if (qr) {
                                bankTransferQrCache = qr;
                                bankTransferQrCacheKey = cacheKey;
                            }
                            return mountCurrent(qr);
                        })
                        .catch(function () {
                            mountCurrent(null);
                            return false;
                        });
                })
                .finally(function () {
                    bankTransferQrInflightKey = "";
                });
        }

        function applySubmitSuccessUi(payload, paymentMethod, btn, prepLabel) {
            const submittedStatus =
                paymentMethod === PAYMENT_METHOD.BANK
                    ? TRAY_STATE.SUBMITTED_BANK
                    : paymentMethod === PAYMENT_METHOD.CARD
                      ? TRAY_STATE.SUBMITTED_CARD
                      : TRAY_STATE.SUBMITTED_CASH;

            GuestInteractionContract.writeGuestOrderSession({
                orderId: payload.id,
                token: payload.guestSessionToken,
                venueTableId: VENUE_TABLE_ID,
                submittedAt: new Date().toISOString(),
                status: submittedStatus,
                paymentMethod: paymentMethod
            });
            expandedItemNoteKeys.clear();
            traySubmittedStatus = submittedStatus;
            trayKnownPendingPayment = true;
            menuOrderSubmitInFlight = false;
            selectedPaymentMethod = PAYMENT_METHOD.CASH;

            // Transition checkout UI before clearCart so cartUpdated cannot re-enter payment-selection.
            const nextUi =
                paymentMethod === PAYMENT_METHOD.BANK ? CHECKOUT_UI.PAYMENT_BANK_LOADING : CHECKOUT_UI.SUBMITTED;
            trayStatusFingerprint = statusFingerprint(readSubmittedSession(), submittedStatus, true);
            setCheckoutUi(nextUi, { skipCart: true });

            GuestInteractionContract.clearCart();
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();

            setTrayOpen(true, { userIntent: true });
            startTrayStatusPolling();

            if (btn) {
                btn.classList.remove("order-tray-submit--preparing");
                btn.setAttribute("disabled", "disabled");
            }

            __annapTrayPaymentDevLog("submit success ui", {
                paymentMethod: paymentMethod,
                renderState: nextUi,
                orderId: payload.id ? "present" : "missing",
                token: payload.guestSessionToken ? "present" : "missing",
                trackUrl: payload.trackUrl ? "present" : "missing"
            });
            if (window.InteractionFeedback) {
                window.InteractionFeedback.trigger("submit", { element: btn || null });
            }

            document.body.classList.add("annap-order-confirming");
            window.setTimeout(function () {
                document.body.classList.remove("annap-order-confirming");
            }, 1200);
        }

        async function refreshBankTransferAvailability() {
            if (!window.GuestBankTransfer || typeof window.GuestBankTransfer.fetchAvailability !== "function") {
                bankTransferConfigured = null;
                applyBankTransferAvailabilityUi();
                return;
            }
            try {
                const data = await window.GuestBankTransfer.fetchAvailability();
                bankTransferConfigured = !!data.enabled;
            } catch (_e) {
                bankTransferConfigured = false;
            }
            applyBankTransferAvailabilityUi();
        }

        function applyBankTransferAvailabilityUi() {
            const bankBtn = document.querySelector('.order-tray-payment-option[data-payment-method="BankTransfer"]');
            if (!bankBtn) return;
            const unavailable = bankTransferConfigured === false;
            bankBtn.disabled = unavailable;
            bankBtn.classList.toggle("order-tray-payment-option--disabled", unavailable);
            bankBtn.setAttribute("aria-disabled", unavailable ? "true" : "false");
            let hint = bankBtn.querySelector(".order-tray-payment-option__hint");
            if (unavailable) {
                if (!hint) {
                    hint = document.createElement("span");
                    hint.className = "order-tray-payment-option__hint";
                    bankBtn.appendChild(hint);
                }
                hint.textContent = trayCopy("checkout.bankTransferUnavailable");
            } else if (hint) {
                hint.remove();
            }
            if (unavailable && selectedPaymentMethod === PAYMENT_METHOD.BANK) {
                selectedPaymentMethod = PAYMENT_METHOD.CASH;
                if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) renderPayment();
            }
        }

        function renderReceipt() {
            const host = document.getElementById("order-tray-checkout-lines");
            const totalEl = document.getElementById("order-tray-checkout-total");
            if (!host) return;
            const lines = Array.from(cartItems.values());
            /* Re-resolve per-line names through the active-locale catalog so the
               REVIEW and PAYMENT_SELECTION panels read in the current language
               even though the line snapshot was stored when an item was added. */
            lines.forEach(function (l) {
                const resolved = localizeLineName(l);
                if (resolved) l.name = resolved;
            });
            host.innerHTML = lines
                .map(function (line) {
                    const lineTotal = line.qty * line.unitPrice;
                    return `<li class="order-tray-checkout__line">
                        <span class="order-tray-checkout__line-name">${escapeHtml(line.name)}</span>
                        <span class="order-tray-checkout__line-qty">×${line.qty}</span>
                        <span class="order-tray-checkout__line-total">${escapeHtml(money.format(lineTotal))}</span>
                    </li>`;
                })
                .join("");
            if (totalEl) totalEl.textContent = money.format(subtotal());
        }

        function getPaymentSubmitLabel(method) {
            if (method === PAYMENT_METHOD.BANK) {
                return tOrder("checkout.submitForQr") || trayCopy("checkout.submitForQr");
            }
            if (method === PAYMENT_METHOD.CARD) {
                return tOrder("checkout.submitCard") || trayCopy("checkout.submitCard");
            }
            return tOrder("checkout.submitCash") || trayCopy("checkout.submitCash");
        }

        function renderPayment() {
            document.querySelectorAll(".order-tray-payment-option").forEach(function (btn) {
                const method = btn.getAttribute("data-payment-method");
                const on = method === selectedPaymentMethod;
                btn.classList.toggle("order-tray-payment-option--selected", on);
                btn.setAttribute("aria-pressed", on ? "true" : "false");
            });
            const preview = document.getElementById("order-tray-payment-preview");
            const titleEl = document.getElementById("order-tray-payment-preview-title");
            const bodyEl = document.getElementById("order-tray-payment-preview-body");
            const noteEl = document.getElementById("order-tray-payment-preview-note");
            const keepOpenEl = document.getElementById("order-tray-payment-preview-keepopen");
            if (!preview || checkoutUi !== CHECKOUT_UI.PAYMENT_SELECTION || !selectedPaymentMethod) {
                if (preview) {
                    preview.classList.add("hidden");
                    preview.hidden = true;
                }
                if (keepOpenEl) {
                    keepOpenEl.classList.add("hidden");
                    keepOpenEl.hidden = true;
                }
                return;
            }
            let title;
            let body;
            let note;
            if (selectedPaymentMethod === PAYMENT_METHOD.BANK) {
                title = trayCopy("checkout.bankPreviewTitle");
                body = trayCopy("checkout.bankPreviewBody");
                note = trayCopy("checkout.bankPreviewNote");
                if (keepOpenEl) {
                    keepOpenEl.textContent = trayCopy("checkout.bankPreviewKeepOpen");
                    keepOpenEl.classList.remove("hidden");
                    keepOpenEl.hidden = false;
                }
            } else if (selectedPaymentMethod === PAYMENT_METHOD.CARD) {
                title = trayCopy("checkout.cardPreviewTitle");
                body = trayCopy("checkout.cardPreviewBody");
                note = trayCopy("checkout.counterPreviewNote");
                if (keepOpenEl) {
                    keepOpenEl.textContent = "";
                    keepOpenEl.classList.add("hidden");
                    keepOpenEl.hidden = true;
                }
            } else {
                title = trayCopy("checkout.cashPreviewTitle");
                body = trayCopy("checkout.cashPreviewBody");
                note = trayCopy("checkout.counterPreviewNote");
                if (keepOpenEl) {
                    keepOpenEl.textContent = "";
                    keepOpenEl.classList.add("hidden");
                    keepOpenEl.hidden = true;
                }
            }
            if (titleEl) titleEl.textContent = title;
            if (bodyEl) bodyEl.textContent = body;
            if (noteEl) noteEl.textContent = note;
            preview.classList.remove("hidden");
            preview.hidden = false;
        }

        function scrollCheckoutCtaIntoView() {
            const sticky = document.getElementById("order-tray-checkout-sticky");
            const btn = document.getElementById("menuSubmitBtn");
            const target = sticky || btn;
            if (!target || typeof target.scrollIntoView !== "function") return;
            try {
                target.scrollIntoView({ block: "nearest", behavior: reducedMotionTray() ? "auto" : "smooth" });
            } catch (_e) {
                target.scrollIntoView(false);
            }
        }

        function updateCTA() {
            const btn = document.getElementById("menuSubmitBtn");
            if (!btn) return;
            if (menuOrderSubmitInFlight) return;
            if (isPostOrderCheckoutUi(checkoutUi)) {
                btn.setAttribute("disabled", "disabled");
                return;
            }
            if (checkoutUi === CHECKOUT_UI.REVIEW) {
                btn.textContent = tOrder("checkout.continueToFulfillment") || trayCopy("checkout.continueToFulfillment") || (tOrder("checkout.continueToPayment") || trayCopy("checkout.continueToPayment"));
                if (VENUE_TABLE_ID) btn.removeAttribute("disabled");
                else btn.setAttribute("disabled", "disabled");
                return;
            }
            if (checkoutUi === CHECKOUT_UI.FULFILLMENT_MODE) {
                btn.textContent = tOrder("checkout.continueToPayment") || trayCopy("checkout.continueToPayment");
                if (selectedFulfillmentMode) btn.removeAttribute("disabled");
                else btn.setAttribute("disabled", "disabled");
                return;
            }
            if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) {
                btn.textContent = getPaymentSubmitLabel(selectedPaymentMethod);
                if (VENUE_TABLE_ID) btn.removeAttribute("disabled");
                else btn.setAttribute("disabled", "disabled");
                return;
            }
            btn.textContent = tOrder("checkout.reviewOrder") || trayCopy("checkout.reviewOrder");
            if (totalQty() > 0 && VENUE_TABLE_ID) btn.removeAttribute("disabled");
            else btn.setAttribute("disabled", "disabled");
        }

        function updateTotals() {
            const subEl = document.getElementById("order-tray-subtotal");
            const chipTotal = document.getElementById("order-tray-chip-total");
            const st = subtotal();
            const n = totalQty();
            if (subEl) {
                if (n === 0) {
                    subEl.textContent = "\u2014";
                    subEl.classList.add("order-tray-subtotal--empty");
                } else {
                    subEl.textContent = money.format(st);
                    subEl.classList.remove("order-tray-subtotal--empty");
                }
            }
            if (chipTotal) {
                if (n === 0) {
                    chipTotal.textContent = "\u2014";
                    chipTotal.classList.add("order-tray-chip__total--empty");
                    chipTotal.setAttribute("aria-hidden", "true");
                } else {
                    chipTotal.textContent = money.format(st);
                    chipTotal.classList.remove("order-tray-chip__total--empty");
                    chipTotal.removeAttribute("aria-hidden");
                }
            }
        }

        function updatePanelVisibility() {
            const review = document.getElementById("order-tray-checkout-review");
            const fulfillment = document.getElementById("order-tray-checkout-fulfillment");
            const payment = document.getElementById("order-tray-checkout-payment");
            const cartFooter = document.getElementById("order-tray-cart-footer");
            const backRow = document.getElementById("order-tray-checkout-actions");
            const stickyBar = document.getElementById("order-tray-checkout-sticky");
            const clearBtn = document.getElementById("order-tray-clear");
            const cartEl = document.getElementById("cart");
            const submittedPanel = document.getElementById("order-tray-submitted-panel");
            const footer = document.getElementById("order-tray-sheet-footer");
            const linesWrap = document.getElementById("order-tray-lines-wrap");

            const inReview = checkoutUi === CHECKOUT_UI.REVIEW;
            const inFulfillment = checkoutUi === CHECKOUT_UI.FULFILLMENT_MODE;
            const inPayment = checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION;
            const inCheckout = inReview || inFulfillment || inPayment;
            const postOrder = isPostOrderCheckoutUi(checkoutUi);

            if (review) {
                review.classList.toggle("hidden", !inReview);
                review.hidden = !inReview;
                review.classList.toggle("order-tray-checkout--compact", inPayment || inFulfillment);
            }
            if (fulfillment) {
                fulfillment.classList.toggle("hidden", !inFulfillment);
                fulfillment.hidden = !inFulfillment;
            }
            if (payment) {
                payment.classList.toggle("hidden", !inPayment);
                payment.hidden = !inPayment;
            }
            if (cartFooter) {
                cartFooter.classList.toggle("order-tray-cart-footer--checkout", inCheckout);
                const subtotalRow = cartFooter.querySelector(".order-tray-subtotal-row");
                if (subtotalRow) subtotalRow.classList.toggle("hidden", inCheckout || postOrder);
            }
            if (backRow) {
                backRow.classList.toggle("hidden", !inCheckout);
                backRow.hidden = !inCheckout;
            }
            if (stickyBar) stickyBar.classList.toggle("order-tray-checkout-sticky--pinned", inPayment || inFulfillment);
            if (clearBtn) clearBtn.classList.toggle("hidden", inCheckout || postOrder);

            if (cartEl) {
                cartEl.classList.toggle("hidden", postOrder || inCheckout);
                cartEl.hidden = postOrder || inCheckout;
            }
            if (submittedPanel) {
                submittedPanel.classList.toggle("hidden", !postOrder);
                submittedPanel.hidden = !postOrder;
            }
            if (footer) footer.classList.toggle("order-tray-sheet-footer--hidden", postOrder);
            if (linesWrap) {
                const hideLines = inCheckout && !postOrder;
                linesWrap.classList.toggle("hidden", hideLines);
                linesWrap.hidden = hideLines;
            }
            if (orderTrayRoot) {
                orderTrayRoot.classList.toggle("order-tray-root--checkout", inCheckout);
                orderTrayRoot.classList.toggle("order-tray-root--checkout-payment", inPayment);
                orderTrayRoot.classList.toggle("order-tray-root--checkout-fulfillment", inFulfillment);
            }
        }

        /* Table identity shown inside the submitted/payment panel. Source priority:
             1. track payload tableCode (authoritative; survives stale GUEST_CTX)
             2. GUEST_CTX.label as synchronous fallback
           Returns "" when no usable table identity exists (e.g. takeaway). Never
           parses arbitrary URL/storage strings. */
        function resolveSubmittedTableBadgeText() {
            const fromTrack = lastGuestTrackData && lastGuestTrackData.tableCode != null
                ? String(lastGuestTrackData.tableCode).trim()
                : "";
            let raw = "";
            if (fromTrack && fromTrack.toLowerCase() !== "takeaway") raw = fromTrack;
            else {
                const lab = String((GUEST_CTX && GUEST_CTX.label) || "").trim();
                if (lab && lab.toLowerCase() !== "takeaway") raw = lab;
            }
            if (!raw) return "";
            const formatted =
                tfmt("cart.tableMetaShort", { label: raw }) ||
                trayCopy("cart.tableMetaShort");
            return formatted || raw;
        }

        function renderSubmitted() {
            const sess = readSubmittedSession();
            const panel = document.getElementById("order-tray-submitted-panel");
            const card = document.getElementById("order-tray-submitted-card");
            const titleEl = document.getElementById("order-tray-submitted-title");
            const statusEl = document.getElementById("order-tray-submitted-status");
            const bodyEl = document.getElementById("order-tray-submitted-body");
            const noteEl = document.getElementById("order-tray-submitted-note");
            const trackEl = document.getElementById("order-tray-submitted-track");
            const tableEl = document.getElementById("order-tray-submitted-table");
            const changePaymentBtn = document.getElementById("order-tray-submitted-change-payment");
            if (!panel || !card || !sess) return;

            let title;
            let body;
            let statusLabel = "";
            const bankUi = isBankCheckoutUi(checkoutUi);
            const awaitingPayment = isSubmittedPendingTrayState(
                traySubmittedStatus || sess.status || TRAY_STATE.SUBMITTED_PENDING
            );
            if (checkoutUi === CHECKOUT_UI.COMPLETED) {
                title = trayCopy("menuTray.chipCompleteTitle");
                body = trayCopy("menuTray.chipCompleteBody");
            } else if (checkoutUi === CHECKOUT_UI.PAID) {
                title = trayCopy("menuTray.chipPaidTitle");
                body = trayCopy("menuTray.paymentSuccessCelebrationBody");
            } else if (bankUi) {
                title = trayCopy("checkout.waitingBankTransfer");
                body = trayCopy("checkout.bankTransferPendingBodyShort");
            } else {
                const pm = String(sess.paymentMethod || "").trim();
                title = trayCopy("menuTray.chipSubmittedTitle");
                if (pm === PAYMENT_METHOD.CARD) {
                    body = trayCopy("checkout.cardSubmittedBodyShort");
                    statusLabel = trayCopy("checkout.waitingCardPayment");
                } else {
                    body = trayCopy("checkout.cashSubmittedBodyShort");
                    statusLabel = trayCopy("checkout.waitingCashPayment");
                }
            }
            const keepOpen = trayCopy("menuTray.chipSubmittedNote");
            if (titleEl) titleEl.textContent = title;
            if (bodyEl) bodyEl.textContent = body;
            if (statusEl) {
                if (statusLabel) {
                    statusEl.textContent = statusLabel;
                    statusEl.classList.remove("hidden");
                    statusEl.hidden = false;
                } else {
                    statusEl.textContent = "";
                    statusEl.classList.add("hidden");
                    statusEl.hidden = true;
                }
            }
            if (noteEl) noteEl.textContent = keepOpen;
            if (tableEl) {
                const tableText = resolveSubmittedTableBadgeText();
                if (tableText) {
                    tableEl.textContent = tableText;
                    tableEl.classList.remove("hidden");
                    tableEl.hidden = false;
                } else {
                    tableEl.textContent = "";
                    tableEl.classList.add("hidden");
                    tableEl.hidden = true;
                }
            }
            if (trackEl) {
                trackEl.href = buildTrackHref(sess);
                trackEl.textContent = trayCopy("menuTray.chipTrackCta");
            }
            if (changePaymentBtn) {
                const canChange =
                    awaitingPayment
                    && checkoutUi !== CHECKOUT_UI.PAID
                    && checkoutUi !== CHECKOUT_UI.COMPLETED
                    && !paymentMethodChangeInflight
                    && !pendingPaymentMethodChange;
                changePaymentBtn.classList.toggle("hidden", !canChange);
                changePaymentBtn.hidden = !canChange;
                changePaymentBtn.setAttribute("aria-busy", paymentMethodChangeInflight ? "true" : "false");
                if (paymentMethodChangeInflight) {
                    changePaymentBtn.setAttribute("disabled", "disabled");
                } else {
                    changePaymentBtn.removeAttribute("disabled");
                }
            }
            card.className = bankUi
                ? "order-tray-submitted-card order-tray-submitted-card--bank"
                : "order-tray-submitted-card order-tray-submitted-card--compact";

            if (bankUi) void ensureBankTransferQrMounted(sess);
            else {
                const host = transferHostEl();
                if (host) host.innerHTML = "";
            }
        }

        /** Sole checkout presentation owner. */
        function renderCheckoutTray(opts) {
            opts = opts || {};
            updatePanelVisibility();
            updateTotals();
            refreshTableIdentityUi();
            updateCTA();
            if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) renderPayment();
            if (checkoutUi === CHECKOUT_UI.FULFILLMENT_MODE) renderFulfillment();

            if (checkoutUi === CHECKOUT_UI.REVIEW || checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) {
                renderReceipt();
            }
            if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) {
                renderPayment();
            }
            if (isPostOrderCheckoutUi(checkoutUi)) {
                renderSubmitted();
            }
            updateTrayChrome(opts);
        }

        function handlePrimaryTrayAction() {
            if (menuOrderSubmitInFlight) return;
            if (!VENUE_TABLE_ID) {
                const orderResult = document.getElementById("orderResult");
                if (orderResult)
                    orderResult.textContent = tOrder("order.needTableScan") || tOrder("order.needTable");
                return;
            }
            // ── Change-payment shortcut: skip all cart/fulfillment checks.
            // pendingPaymentMethodChange + PAYMENT_SELECTION means the user is
            // updating the PaymentMethod on an already-submitted order (same
            // OrderId, same items, no new order creation). Dispatch directly.
            if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION && pendingPaymentMethodChange) {
                void submitOrder();
                return;
            }
            if (cartItems.size === 0) {
                const orderResult = document.getElementById("orderResult");
                if (orderResult) orderResult.textContent = tOrder("order.minOne");
                return;
            }
            if (checkoutUi === CHECKOUT_UI.EDITING) {
                setTrayOpen(true, { userIntent: true });
                setCheckoutUi(CHECKOUT_UI.REVIEW);
                return;
            }
            if (checkoutUi === CHECKOUT_UI.REVIEW) {
                setCheckoutUi(CHECKOUT_UI.FULFILLMENT_MODE);
                return;
            }
            if (checkoutUi === CHECKOUT_UI.FULFILLMENT_MODE) {
                if (!selectedFulfillmentMode) {
                    // Defensive: button should be disabled, but guard against race.
                    return;
                }
                setCheckoutUi(CHECKOUT_UI.PAYMENT_SELECTION);
                return;
            }
            if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) {
                void submitOrder();
            }
        }

        function isPendingTrackPayload(data) {
            if (!data || data.isCancelled || data.isComplete) return false;
            if (data.pendingPayment === true) return true;
            const phase = data.phaseKey != null ? String(data.phaseKey) : "";
            return phase === "awaiting_payment" || Number(data.step) === 1;
        }

        function isPaidTrackPayload(data) {
            return isOrderPaidForGuest(data);
        }

        function isOrderPaidForGuest(data) {
            if (!data || data.isCancelled || data.isComplete) return false;
            if (data.pendingPayment === true) return false;
            if (data.paidAtUtc) return true;
            const phase = data.phaseKey != null ? String(data.phaseKey) : "";
            if (phase === "paid_preparing" || phase.startsWith("paid") || phase === "completed") return true;
            if (data.showBill === true && data.pendingPayment === false) return true;
            const statusRaw = data.status != null ? String(data.status) : "";
            if (/^(Paid|InProgress|FinishingTouches|Ready)$/i.test(statusRaw)) return true;
            return false;
        }

        function isSubmittedPendingTrayState(state) {
            return (
                state === TRAY_STATE.SUBMITTED_PENDING ||
                state === TRAY_STATE.SUBMITTED_COUNTER ||
                state === TRAY_STATE.SUBMITTED_CASH ||
                state === TRAY_STATE.SUBMITTED_CARD ||
                state === TRAY_STATE.SUBMITTED_BANK
            );
        }

        function shouldPollSubmittedTrayStatus() {
            if (!readSubmittedSession()) return false;
            return isSubmittedPendingTrayState(resolveTrayState());
        }

        function stopTrayStatusPolling() {
            if (trayStatusPollTimer) {
                window.clearInterval(trayStatusPollTimer);
                trayStatusPollTimer = null;
            }
        }

        function startTrayStatusPolling() {
            if (!shouldPollSubmittedTrayStatus()) return;
            function pollTick() {
                if (!shouldPollSubmittedTrayStatus()) {
                    stopTrayStatusPolling();
                    return;
                }
                refreshSubmittedTrayStatus()
                    .then(function (result) {
                        if (!result || !result.fingerprintChanged) return;
                        renderCheckoutTray();
                    })
                    .catch(function () {
                        /* ignore */
                    });
            }
            pollTick();
            if (trayStatusPollTimer) return;
            trayStatusPollTimer = window.setInterval(pollTick, TRAY_STATUS_POLL_MS);
        }

        function dismissPaymentSuccessCelebration() {
            const host = document.getElementById("order-tray-payment-celebration");
            if (!host) return;
            host.classList.remove("is-visible");
            window.setTimeout(function () {
                if (host.parentNode) host.parentNode.removeChild(host);
            }, 280);
        }

        function runTrayConfettiBurst() {
            if (reducedMotionTray()) return;
            const wrap = document.createElement("div");
            wrap.className = "order-tray-confetti";
            wrap.setAttribute("aria-hidden", "true");
            const colors = ["#f5c842", "#e8a838", "#6fcf97", "#56ccf2", "#bb6bd9", "#f2994a"];
            for (let i = 0; i < 52; i++) {
                const piece = document.createElement("span");
                piece.className = "order-tray-confetti__piece";
                piece.style.setProperty("--x", String(Math.random()));
                piece.style.setProperty("--delay", String(Math.random() * 0.35) + "s");
                piece.style.setProperty("--rot", String(Math.random() * 360) + "deg");
                piece.style.setProperty("--drift", String((Math.random() - 0.5) * 80) + "px");
                piece.style.background = colors[i % colors.length];
                wrap.appendChild(piece);
            }
            document.body.appendChild(wrap);
            window.setTimeout(function () {
                if (wrap.parentNode) wrap.parentNode.removeChild(wrap);
            }, 2000);
        }

        function showPaymentSuccessCelebration(sess, opts) {
            opts = opts || {};
            const onDismiss = typeof opts.onDismiss === "function" ? opts.onDismiss : null;
            dismissPaymentSuccessCelebration();
            const host = document.createElement("div");
            host.id = "order-tray-payment-celebration";
            host.className = "order-tray-payment-celebration";
            host.setAttribute("role", "dialog");
            host.setAttribute("aria-modal", "true");
            host.setAttribute("aria-live", "polite");
            const title = trayCopy("menuTray.chipPaidTitle");
            const body = trayCopy("menuTray.paymentSuccessCelebrationBody");
            const trackLabel = trayCopy("menuTray.chipTrackCta");
            const closeLabel = trayCopy("menuTray.paymentSuccessClose");
            const trackHref = String(buildTrackHref(sess)).replace(/"/g, "&quot;");
            host.innerHTML =
                '<div class="order-tray-payment-celebration__backdrop" data-celebration-act="close"></div>' +
                '<div class="order-tray-payment-celebration__card">' +
                '<p class="order-tray-payment-celebration__title">' +
                escapeHtml(title) +
                "</p>" +
                '<p class="order-tray-payment-celebration__body">' +
                escapeHtml(body) +
                "</p>" +
                '<div class="order-tray-payment-celebration__actions">' +
                '<a href="' +
                trackHref +
                '" class="order-tray-payment-celebration__track guest-hit">' +
                escapeHtml(trackLabel) +
                "</a>" +
                '<button type="button" class="order-tray-payment-celebration__close guest-hit" data-celebration-act="close">' +
                escapeHtml(closeLabel) +
                "</button>" +
                "</div></div>";
            host.addEventListener("click", function (ev) {
                const act = ev.target && ev.target.closest ? ev.target.closest("[data-celebration-act]") : null;
                if (!act) return;
                ev.preventDefault();
                dismissPaymentSuccessCelebration();
                if (onDismiss) {
                    try {
                        onDismiss();
                    } catch (_cbErr) {
                        /* ignore */
                    }
                }
            });
            document.body.appendChild(host);
            window.requestAnimationFrame(function () {
                host.classList.add("is-visible");
            });
            runTrayConfettiBurst();
        }

        /* After payment is confirmed by staff:
           - keep the celebration visible for the current paid order
           - keep celebratedPaidOrderIds populated so the same paid order cannot
             replay its celebration on the next poll/reload
           - register a single-fire cleanup that, when the user dismisses the
             celebration, removes the old Guest order session, resets transient
             state tied to the submitted order, and transitions the tray back
             to EDITING so a fresh order can be placed. */
        function handlePaymentConfirmedCelebration(sess) {
            setTrayOpen(false);
            checkoutUi = CHECKOUT_UI.PAID;
            renderCheckoutTray();
            let cleanupFired = false;
            const cleanupAfterPaidCelebration = function () {
                if (cleanupFired) return;
                cleanupFired = true;
                /* Use the canonical reset so every transient state variable
                   bound to the submitted order is cleared in one place. The
                   'user_dismissed_paid' reason signals the celebration path. */
                resetGuestOrderSessionUi("user_dismissed_paid");
                try {
                    if (
                        isPostOrderCheckoutUi(checkoutUi) ||
                        checkoutUi === CHECKOUT_UI.PAID ||
                        checkoutUi === CHECKOUT_UI.COMPLETED
                    ) {
                        setCheckoutUi(CHECKOUT_UI.EDITING);
                    } else {
                        renderCheckoutTray();
                    }
                } catch (_p3) {
                    checkoutUi = CHECKOUT_UI.EDITING;
                    renderCheckoutTray();
                }
            };
            showPaymentSuccessCelebration(sess, { onDismiss: cleanupAfterPaidCelebration });
            if (window.InteractionFeedback) {
                window.InteractionFeedback.trigger("success", { silentVisual: true });
            }
            __annapTrayPaymentDevLog("payment confirmed celebration", { orderId: sess && sess.orderId });
        }

        /* TERMINAL ORDER ≠ ACTIVE GUEST ORDER SESSION.
           Server is authoritative. When server confirms a terminal state
           (404, isCancelled, isComplete), remove the local Guest Order Session
           and reset every transient UI variable bound to that order so the
           Guest can immediately start a new order. */
        function resetGuestOrderSessionUi(reason) {
            try {
                stopTrayStatusPolling();
            } catch (_p1) {
                /* ignore */
            }
            try {
                GuestInteractionContract.removeGuestOrderSession();
            } catch (_p2) {
                /* ignore */
            }
            traySubmittedStatus = null;
            trayKnownPendingPayment = null;
            trayStatusFingerprint = "";
            bankTransferQrInflightKey = "";
            bankTransferQrCache = null;
            bankTransferQrCacheKey = "";
            lastGuestTrackData = null;
            pendingPaymentMethodChange = false;
            paymentMethodChangePreselect = null;
            paymentMethodChangeInflight = false;
            /* Drop any in-memory cart lines that survived a hard reload. The
               local cart key is per-table; only wipe when this is the active
               table. cartItems may legitimately contain the Guest's new draft
               after a terminal cleanup if they had started a new order — never
               touch cartItems automatically here. */
            try {
                linesToCartMap(GuestInteractionContract.getCartLines());
            } catch (_cartReload) {
                cartItems.clear();
            }
            __annapTrayPaymentDevLog("guest session reset", { reason: reason || "terminal" });
        }

        async function refreshSubmittedTrayStatus() {
            const sess = readSubmittedSession();
            if (!sess) {
                traySubmittedStatus = null;
                trayKnownPendingPayment = null;
                stopTrayStatusPolling();
                return { fingerprintChanged: false };
            }
            try {
                const res = await fetchGuestOrderStatus(sess);
                if (res.status === 404) {
                    resetGuestOrderSessionUi("server_404");
                    if (isPostOrderCheckoutUi(checkoutUi)) setCheckoutUi(CHECKOUT_UI.EDITING);
                    return { fingerprintChanged: true };
                }
                if (!res.ok) return { fingerprintChanged: false };
                let data;
                try {
                    data = await res.json();
                } catch (_json) {
                    __annapTrayPaymentDevLog("guest status poll: non-json response", { status: res.status });
                    return { fingerprintChanged: false };
                }
                /* Cache the latest successful track payload for presentation helpers
                   (e.g. submitted panel table badge). Cleared when the tray returns
                   to EDITING after payment confirmation. */
                if (data && typeof data === "object") lastGuestTrackData = data;
                if (data.isCancelled) {
                    resetGuestOrderSessionUi("server_cancelled");
                    if (isPostOrderCheckoutUi(checkoutUi)) setCheckoutUi(CHECKOUT_UI.EDITING);
                    return { fingerprintChanged: true };
                }
                const wasPending =
                    trayKnownPendingPayment === true ||
                    (trayKnownPendingPayment !== false &&
                        isSubmittedPendingTrayState(traySubmittedStatus || sess.status || TRAY_STATE.SUBMITTED_PENDING));
                const isPending = isPendingTrackPayload(data);
                const isPaid = isPaidTrackPayload(data);
                if (data.isComplete) {
                    /* Server says the order is finished. The Guest Order Session
                       must NOT keep blocking the tray. Remove it and return the
                       tray to EDITING regardless of any local status. */
                    const orderKey = sess.orderId ? String(sess.orderId) : "";
                    const wasCelebrated = orderKey && celebratedPaidOrderIds.has(orderKey);
                    /* Preserve an in-flight celebration modal: if PAID→COMPLETED
                       transitions happen while the paid celebration is still
                       visible, leave the modal/PAID chrome intact and let the
                       user dismiss it to fire cleanup. Otherwise the Guest would
                       never see the paid confirmation. */
                    if (wasCelebrated && checkoutUi === CHECKOUT_UI.PAID) {
                        traySubmittedStatus = TRAY_STATE.COMPLETED;
                        GuestInteractionContract.updateGuestOrderSessionStatus(TRAY_STATE.COMPLETED);
                        if (!window.__annapIfCompletePlayed && window.InteractionFeedback) {
                            window.__annapIfCompletePlayed = true;
                            window.InteractionFeedback.trigger("complete", { silentVisual: true });
                        }
                        trayKnownPendingPayment = false;
                        stopTrayStatusPolling();
                    } else {
                        resetGuestOrderSessionUi("server_completed");
                        celebratedPaidOrderIds.delete(orderKey);
                        window.__annapIfCompletePlayed = false;
                        if (!window.__annapIfCompletePlayed && window.InteractionFeedback) {
                            window.__annapIfCompletePlayed = true;
                            window.InteractionFeedback.trigger("complete", { silentVisual: true });
                        }
                        if (isPostOrderCheckoutUi(checkoutUi) || checkoutUi === CHECKOUT_UI.PAID || checkoutUi === CHECKOUT_UI.COMPLETED) {
                            setCheckoutUi(CHECKOUT_UI.EDITING);
                        } else {
                            renderCheckoutTray();
                        }
                    }
                    return { fingerprintChanged: true, terminalCleared: true };
                } else if (isPaid) {
                    traySubmittedStatus = TRAY_STATE.PAID;
                    GuestInteractionContract.updateGuestOrderSessionStatus(TRAY_STATE.PAID);
                    trayKnownPendingPayment = false;
                    stopTrayStatusPolling();
                } else {
                    const pendingState = resolveSubmittedCounterState(sess.paymentMethod);
                    traySubmittedStatus = pendingState;
                    GuestInteractionContract.updateGuestOrderSessionStatus(pendingState);
                    if (isPending) trayKnownPendingPayment = true;
                }
                let celebrated = false;
                const orderKey = sess.orderId ? String(sess.orderId) : "";
                if (wasPending && isPaid && orderKey && !celebratedPaidOrderIds.has(orderKey)) {
                    celebratedPaidOrderIds.add(orderKey);
                    handlePaymentConfirmedCelebration(sess);
                    celebrated = true;
                }
                if (isPending) startTrayStatusPolling();
                const fp = statusFingerprint(sess, traySubmittedStatus, trayKnownPendingPayment);
                const fingerprintChanged = fp !== trayStatusFingerprint;
                if (fingerprintChanged) {
                    trayStatusFingerprint = fp;
                    const nextUi = checkoutUiFromSession();
                    if (nextUi !== checkoutUi) checkoutUi = nextUi;
                }
                return { fingerprintChanged: fingerprintChanged, celebrated: celebrated };
            } catch (_st) {
                return { fingerprintChanged: false };
            }
        }

        function updateTrayChrome(opts) {
            opts = opts || {};
            const n = totalQty();
            const state = resolveTrayState();
            const submitted = isPostOrderCheckoutUi(checkoutUi);
            const isEmpty = n === 0 && !submitted;
            const line = document.getElementById("trayCountLine");
            const sheetTitle = document.getElementById("traySheetTitle");
            const chipTitle = document.getElementById("order-tray-chip-title");
            const chipSub = document.getElementById("order-tray-chip-sub");
            const chipStack = document.getElementById("order-tray-chip-stack");
            const st = subtotal();
            if (orderTrayRoot) {
                orderTrayRoot.classList.toggle("order-tray-root--empty", isEmpty);
                orderTrayRoot.classList.toggle(
                    "order-tray-root--submitted",
                    submitted && checkoutUi !== CHECKOUT_UI.PAID && checkoutUi !== CHECKOUT_UI.COMPLETED
                );
                orderTrayRoot.classList.toggle("order-tray-root--submitted-paid", checkoutUi === CHECKOUT_UI.PAID);
                orderTrayRoot.classList.toggle("order-tray-root--submitted-complete", checkoutUi === CHECKOUT_UI.COMPLETED);
            }
            if (orderTrayChip) orderTrayChip.classList.toggle("order-tray-chip--has-items", n > 0 && !submitted);

            if (sheetTitle) {
                if (checkoutUi === CHECKOUT_UI.COMPLETED) {
                    sheetTitle.textContent = trayCopy("menuTray.chipCompleteTitle");
                } else if (checkoutUi === CHECKOUT_UI.PAID) {
                    sheetTitle.textContent = trayCopy("menuTray.chipPaidTitle");
                } else if (isBankCheckoutUi(checkoutUi)) {
                    sheetTitle.textContent = trayCopy("checkout.waitingBankTransfer");
                } else if (checkoutUi === CHECKOUT_UI.SUBMITTED) {
                    sheetTitle.textContent = trayCopy("menuTray.chipSubmittedTitle");
                } else if (checkoutUi === CHECKOUT_UI.REVIEW || checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) {
                    sheetTitle.textContent =
                        n === 1
                            ? trayCopy("menuTray.sheetTitleOne")
                            : n > 1
                              ? tfmt("menuTray.sheetTitleMany", { n }) || trayCopy("menuTray.sheetTitleMany")
                              : trayCopy("menuTray.sheetTitle");
                } else {
                    sheetTitle.textContent = trayCopy("menuTray.sheetTitle");
                }
            }

            if (line) {
                if (isBankCheckoutUi(checkoutUi)) {
                    line.textContent = trayCopy("checkout.waitingBankTransfer");
                } else if (checkoutUi === CHECKOUT_UI.SUBMITTED) {
                    const sess = readSubmittedSession();
                    const pm = sess ? String(sess.paymentMethod || "").trim() : "";
                    if (pm === PAYMENT_METHOD.CARD) line.textContent = trayCopy("checkout.waitingCardPayment");
                    else line.textContent = trayCopy("checkout.waitingCashPayment");
                } else if (submitted) {
                    line.textContent = trayCopy("menuTray.chipSubmittedNote");
                } else if (n === 0) line.textContent = trayCopy("menuTray.countNone");
                else if (n === 1) line.textContent = trayCopy("menuTray.countOne");
                else line.textContent = tfmt("menuTray.countMany", { n }) || trayCopy("menuTray.countMany");
            }

            if (chipTitle) {
                chipTitle.classList.remove("order-tray-chip__count--settling");
                if (checkoutUi === CHECKOUT_UI.COMPLETED) {
                    chipTitle.textContent = trayCopy("menuTray.chipCompleteTitle");
                } else if (checkoutUi === CHECKOUT_UI.PAID) {
                    chipTitle.textContent = trayCopy("menuTray.chipPaidTitle");
                } else if (submitted) {
                    chipTitle.textContent = trayCopy("menuTray.chipSubmittedTitle");
                } else if (n === 0) {
                    chipTitle.textContent = isSommelierFlowActive()
                        ? trayCopy("menuTray.chipEmptySomm")
                        : trayCopy("menuTray.chipEmpty");
                } else if (n === 1) {
                    chipTitle.textContent = trayCopy("menuTray.chipOne");
                } else {
                    chipTitle.textContent = isSommelierFlowActive()
                        ? tfmt("menuTray.chipManySomm", { n }) || tfmt("menuTray.chipMany", { n }) || trayCopy("menuTray.chipMany")
                        : tfmt("menuTray.chipMany", { n }) || trayCopy("menuTray.chipMany");
                }
                if (opts.animateChip && n > 0 && !reducedMotionTray()) {
                    void chipTitle.offsetWidth;
                    chipTitle.classList.add("order-tray-chip__count--settling");
                }
            }
            if (chipSub) {
                if (checkoutUi === CHECKOUT_UI.COMPLETED) {
                    chipSub.textContent = trayCopy("menuTray.chipCompleteBody");
                } else if (checkoutUi === CHECKOUT_UI.PAID) {
                    chipSub.textContent = trayCopy("menuTray.chipPreparingShort");
                } else if (isBankCheckoutUi(checkoutUi)) {
                    chipSub.textContent = trayCopy("checkout.waitingBankTransfer");
                } else if (checkoutUi === CHECKOUT_UI.SUBMITTED) {
                    const sess = readSubmittedSession();
                    const pm = sess ? String(sess.paymentMethod || "").trim() : "";
                    if (pm === PAYMENT_METHOD.CARD) chipSub.textContent = trayCopy("checkout.waitingCardPayment");
                    else chipSub.textContent = trayCopy("checkout.waitingCashPayment");
                } else {
                    chipSub.textContent =
                        n === 0
                            ? isSommelierFlowActive()
                                ? trayCopy("menuTray.chipSubSomm")
                                : trayCopy("menuTray.chipSubEmpty")
                            : trayCopy("menuTray.chipSubFilledShort");
                }
            }
            updateTotals();
            if (chipStack) {
                if (submitted) {
                    chipStack.innerHTML = '<span class="order-tray-chip__seal" aria-hidden="true">A</span>';
                    chipStack.classList.remove("annap-tray-slot-receive");
                } else {
                    const recent = Array.from(cartItems.values()).slice(-1);
                    if (!recent.length) {
                        chipStack.innerHTML = '<span class="order-tray-chip__empty-line"></span>';
                        chipStack.classList.remove("annap-tray-slot-receive");
                    } else {
                        chipStack.innerHTML = recent
                            .map((l, i) => {
                                /* Initial letter comes from the active-locale
                                   cart name so the chip glyph stays in sync
                                   when the guest switches VI/EN after items
                                   are already on the tray. */
                                const displayName = localizeLineName(l) || l.name || "A";
                                const initial = escapeHtml(String(displayName).trim().charAt(0).toUpperCase() || "A");
                                const img = l.imageSrc
                                    ? `<img src="${escapeHtml(l.imageSrc)}" alt="" decoding="async" loading="lazy" />`
                                    : initial;
                                return `<span class="order-tray-chip__card" style="--tray-card-i:${i}">${img}</span>`;
                            })
                            .join("");
                        if (opts.animateChip && !reducedMotionTray()) {
                            chipStack.classList.remove("annap-tray-slot-receive");
                            void chipStack.offsetWidth;
                            chipStack.classList.add("annap-tray-slot-receive");
                        }
                    }
                }
            }
            const chipGuest = document.getElementById("order-tray-chip-guest");
            if (chipGuest) {
                const gb =
                    typeof window.__annapGroupBrowseActive !== "undefined" &&
                    window.__annapGroupBrowseActive &&
                    typeof window.__annapGroupGuestCount !== "undefined" &&
                    parseInt(window.__annapGroupGuestCount, 10) >= 1;
                const activeLab =
                    typeof window.__annapActiveGroupGuestLabel === "string"
                        ? String(window.__annapActiveGroupGuestLabel).trim()
                        : "";
                if (gb && activeLab) {
                    const pour = tOrder("menuTray.chipGuestPouring");
                    chipGuest.textContent = `${pour} · ${activeLab}`;
                    chipGuest.classList.remove("hidden");
                } else {
                    chipGuest.textContent = "";
                    chipGuest.classList.add("hidden");
                }
            }
        }

        function updateTraySummary(opts) {
            opts = opts || {};
            if (
                isPostOrderCheckoutUi(checkoutUi) ||
                checkoutUi === CHECKOUT_UI.REVIEW ||
                checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION
            ) {
                renderCheckoutTray(opts);
                return;
            }
            updateTrayChrome(opts);
            updateCTA();
            updatePanelVisibility();
        }

        function addToCart(menuItemId, btn) {
            syncCartKey();
            if (!VENUE_TABLE_ID) return;
            if (typeof GuestInteractionContract === "undefined" || !GuestInteractionContract) {
                __annapMenuBootErr("[ANNAP MENU] addToCart: GuestInteractionContract missing");
                return;
            }
            const incoming = lineFromSource(menuItemId, btn);
            const gl =
                typeof window.__annapActiveGroupGuestLabel === "string"
                    ? window.__annapActiveGroupGuestLabel.trim()
                    : "";
            const row = catalogRow(menuItemId);
            const temperature = (btn && btn.dataset && btn.dataset.temperature) ||
                (row && row.supportsHotIced ? "Hot" : "");
            GuestInteractionContract.addItem({
                menuItemId,
                name: incoming.name,
                unitPrice: incoming.unitPrice,
                imageSrc: incoming.imageSrc,
                selectionFallback: tOrder("cart.selectionFallback"),
                sourceElement: btn || null,
                guestLabel: gl,
                temperature: temperature
            });
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            updateTraySummary({ animateChip: !!btn });
        }

        window.annapAddToCart = (id, el) => addToCart(id, el);
        window.__detailAdd = window.annapAddToCart;

        function _detailModal() {
            return typeof window.DrinkDetailModal !== "undefined" ? window.DrinkDetailModal : null;
        }

        const _menuBrowseTapMeta = new Map();
        let _menuBrowseTouchHandledAt = 0; /* timestamp of last touch-pointerup action; guards click dedup */

        function annapClearMenuCardPress() {
            document.querySelectorAll(".menu-browse [data-drink-card].menu-card-reveal--pressed").forEach(function (c) {
                c.classList.remove("menu-card-reveal--pressed");
            });
        }

        function tryActivateMenuBrowseFromTarget(t, ev) {
            if (!t || !t.closest) return false;
            const browse = t.closest(".menu-browse");
            if (!browse) return false;

            const detailBtn = t.closest("[data-menu-detail]");
            if (detailBtn && browse.contains(detailBtn)) {
                if (ev) ev.preventDefault();
                const cid = detailBtn.getAttribute("data-menu-detail");
                if (cid) void openDrinkDetail(cid);
                return true;
            }

            const addBtn = t.closest(".menu-add-btn,[data-add]");
            if (addBtn && browse.contains(addBtn)) {
                if (addBtn.disabled) return false;
                if (ev) ev.preventDefault();
                const aid = addBtn.getAttribute("data-add");
                if (aid && typeof addToCart === "function") addToCart(aid, addBtn);
                return true;
            }

            const card = t.closest("[data-drink-card]");
            if (card && browse.contains(card) && !t.closest("button,a")) {
                if (ev) ev.preventDefault();
                const cid = card.getAttribute("data-drink-card");
                if (cid) void openDrinkDetail(cid);
                return true;
            }
            return false;
        }

        function annapOnMenuBrowsePointerUp(ev) {
            annapClearMenuCardPress();
            if (ev.pointerType !== "touch" && ev.pointerType !== "pen") return;
            if (!ev.isPrimary || ev.button !== 0) return;
            const meta = _menuBrowseTapMeta.get(ev.pointerId);
            _menuBrowseTapMeta.delete(ev.pointerId);
            if (!meta) return;
            const dx = ev.clientX - meta.x;
            const dy = ev.clientY - meta.y;
            if (dx * dx + dy * dy > 900) return;
            const t = ev.target;
            if (!t || !t.closest) return;
            const browse = t.closest(".menu-browse");
            if (!browse || browse !== meta.browse) return;
            if (tryActivateMenuBrowseFromTarget(t, ev)) {
                _menuBrowseTouchHandledAt = Date.now();
                try {
                    ev.preventDefault();
                } catch (_pe) {
                    /* ignore */
                }
            }
        }

        function annapOnMenuBrowsePointerCancel(ev) {
            _menuBrowseTapMeta.delete(ev.pointerId);
            annapClearMenuCardPress();
        }

        function annapBindMenuCatalogInteractions() {
            if (document.documentElement.dataset.annapMenuCatalogUi === "1") return;
            document.documentElement.dataset.annapMenuCatalogUi = "1";

            document.addEventListener(
                "click",
                function (ev) {
                    /* Skip if a touch pointerup already handled this exact tap (< 600 ms ago).
                       Safari iOS fires pointerup then a synthetic click on the same gesture. */
                    if (Date.now() - _menuBrowseTouchHandledAt < 600) return;
                    const t = ev.target;
                    if (!t || !t.closest) return;
                    const browse = t.closest(".menu-browse");
                    if (!browse) return;
                    tryActivateMenuBrowseFromTarget(t, ev);
                },
                false
            );

            document.addEventListener(
                "keydown",
                function (ev) {
                    if (ev.key !== "Enter" && ev.key !== " ") return;
                    const el = ev.target;
                    if (!el || !el.closest) return;
                    const browse = el.closest(".menu-browse");
                    if (!browse) return;
                    if (el.closest && el.closest("button,a")) return;
                    const card = el.closest("[data-drink-card]");
                    if (!card || !browse.contains(card)) return;
                    ev.preventDefault();
                    const cid = card.getAttribute("data-drink-card");
                    if (cid) void openDrinkDetail(cid);
                },
                false
            );

            document.addEventListener(
                "pointerdown",
                function (ev) {
                    const t = ev.target;
                    if (!t || !t.closest) return;
                    const browse = t.closest(".menu-browse");
                    if (!browse) return;
                    if ((ev.pointerType === "touch" || ev.pointerType === "pen") && ev.isPrimary && ev.button === 0) {
                        _menuBrowseTapMeta.set(ev.pointerId, {
                            x: ev.clientX,
                            y: ev.clientY,
                            browse: browse
                        });
                    }
                    const card = t.closest("[data-drink-card]");
                    if (!card || !browse.contains(card)) return;
                    if (t.closest("button,a")) return;
                    if (ev.pointerType === "mouse" && ev.button !== 0) return;
                    /* Concurrent image preload — starts the detail poster request at
                       pointerdown so the Cloudinary fetch is in-flight before the API
                       request begins on pointerup/click. No blocking, no preventDefault. */
                    preloadDrinkDetailImage(card.getAttribute("data-drink-card"));
                    card.classList.add("menu-card-reveal--pressed");
                },
                true
            );
            document.addEventListener("pointerup", annapOnMenuBrowsePointerUp, true);
            document.addEventListener("pointercancel", annapOnMenuBrowsePointerCancel, true);
        }

        function annapBindDrinkDetailModal() {
            if (window._annapDrinkDetailModalBound) return;
            window._annapDrinkDetailModalBound = true;
            document.documentElement.dataset.annapDrinkDetailModal = "1";
            var dm = _detailModal();
            if (dm && typeof dm.init === "function") dm.init();
        }

        async function openDrinkDetail(id, options) {
            if (!document.getElementById("drink-detail-modal")) return;
            var dm = _detailModal();
            if (!dm || typeof dm.open !== "function") {
                __annapMenuBootErr("[ANNAP MENU] openDrinkDetail: DrinkDetailModal missing");
                return;
            }
            setTrayOpen(false);
            __annapModalLog("open", { id: id });
            return dm.open(id, options);
        }

        function closeDrinkDetail() {
            var dm = _detailModal();
            if (!dm || typeof dm.close !== "function") return;
            __annapModalLog("close");
            dm.close();
        }

        if (!window._annapTrayEscapeBound) {
            window._annapTrayEscapeBound = true;
            document.addEventListener("keydown", function (e) {
                if (e.key !== "Escape") return;
                var dm = _detailModal();
                if (dm && typeof dm.isOpen === "function" && dm.isOpen()) return;
                if (trayOpen) setTrayOpen(false);
            });
        }

        var _trayClear = document.getElementById("order-tray-clear");
        if (_trayClear) _trayClear.addEventListener("click", function () {
            clearCart();
        });

        function clearCart() {
            GuestInteractionContract.clearCart();
            GuestInteractionContract.removeGuestOrderSession();
            traySubmittedStatus = null;
            setCheckoutUi(CHECKOUT_UI.EDITING);
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            const orderResult = document.getElementById("orderResult");
            if (orderResult) orderResult.textContent = "";
            const btn = document.getElementById("menuSubmitBtn");
            if (btn) {
                btn.classList.remove("order-tray-submit--success", "order-tray-submit--pulse");
            }
            updateTraySummary();
            refreshTableIdentityUi();
        }

        function setQty(id, delta, guestLabel, temperature) {
            const gl = guestLabel !== undefined && guestLabel !== null ? String(guestLabel).trim() : "";
            const temp = temperature !== undefined && temperature !== null ? String(temperature).trim() : "";
            GuestInteractionContract.adjustItemQuantity(id, delta, gl, temp);
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            updateTraySummary();
        }

        function removeLine(id, guestLabel, temperature) {
            const gl = guestLabel !== undefined && guestLabel !== null ? String(guestLabel).trim() : "";
            const temp = temperature !== undefined && temperature !== null ? String(temperature).trim() : "";
            GuestInteractionContract.removeItem(id, gl, temp);
            linesToCartMap(GuestInteractionContract.getCartLines());
            renderCart();
            updateTraySummary();
        }

        function itemNoteKey(line) {
            return cartLineKey(line);
        }

        function noteDraftValue(raw) {
            const s = raw != null ? String(raw) : "";
            return s.length > 200 ? s.slice(0, 200) : s;
        }

        function persistLineCustomerNoteDraft(line, rawNote) {
            const key = itemNoteKey(line);
            const draft = noteDraftValue(rawNote);
            const stored = cartItems.get(key);
            if (stored) stored.customerNote = draft;
            if (typeof GuestInteractionContract.setLineCustomerNote === "function") {
                GuestInteractionContract.setLineCustomerNote(
                    line.id,
                    draft,
                    line.guestLabel,
                    line.temperature || "",
                    { silent: true }
                );
            }
            return draft;
        }

        function commitLineCustomerNote(line, options) {
            options = options || {};
            const key = itemNoteKey(line);
            const stored = cartItems.get(key);
            const raw = stored && stored.customerNote != null ? String(stored.customerNote) : "";
            const trimmed = raw.trim() ? raw.trim().slice(0, 200) : "";
            if (stored) stored.customerNote = trimmed;
            if (typeof GuestInteractionContract.setLineCustomerNote === "function") {
                GuestInteractionContract.setLineCustomerNote(
                    line.id,
                    trimmed || null,
                    line.guestLabel,
                    line.temperature || "",
                    {
                        silent: options.silent === true
                    }
                );
            }
            if (!trimmed && !expandedItemNoteKeys.has(key)) {
                refreshNotePillForKey(key);
            } else if (!expandedItemNoteKeys.has(key)) {
                refreshNotePillForKey(key);
            }
            return trimmed;
        }

        function isNoteEditorFocused() {
            const active = document.activeElement;
            return !!(active && active.classList && active.classList.contains("tray-line-note-input"));
        }

        function syncOpenNoteTextareasFromDom() {
            document.querySelectorAll(".tray-line-note-input").forEach(function (ta) {
                const card = ta.closest ? ta.closest("[data-tray-line-key]") : null;
                if (!card) return;
                const noteKey = card.getAttribute("data-tray-line-key");
                if (!noteKey) return;
                const line = cartItems.get(noteKey);
                if (!line) return;
                persistLineCustomerNoteDraft(line, ta.value);
            });
        }

        function flushAllNoteDraftsForSubmit() {
            syncOpenNoteTextareasFromDom();
            cartItems.forEach(function (line) {
                commitLineCustomerNote(line, { silent: true });
            });
            if (typeof GuestInteractionContract !== "undefined" && typeof GuestInteractionContract.getCartLines === "function") {
                linesToCartMap(GuestInteractionContract.getCartLines());
            }
        }

        function refreshNotePillForKey(noteKey) {
            const card = document.querySelector('[data-tray-line-key="' + String(noteKey).replace(/"/g, '\\"') + '"]');
            if (!card) return;
            const line = cartItems.get(noteKey);
            if (!line) return;
            const letter = card.querySelector(".tray-correspondence-card__letter");
            if (!letter) return;
            const noteExpanded = expandedItemNoteKeys.has(noteKey);
            let pill = letter.querySelector(".tray-line-customer-note--pill");
            const noteText =
                line.customerNote && String(line.customerNote).trim() ? String(line.customerNote).trim() : "";
            if (noteText && !noteExpanded) {
                if (!pill) {
                    pill = document.createElement("p");
                    pill.className = "tray-line-customer-note tray-line-customer-note--pill";
                    const toggle = letter.querySelector(".tray-line-note-toggle");
                    if (toggle && toggle.nextSibling) letter.insertBefore(pill, toggle);
                    else letter.appendChild(pill);
                }
                pill.textContent =
                    (tOrder("menuTray.itemNotePreview")) +
                    " " +
                    noteText;
            } else if (pill) {
                pill.remove();
            }
        }

        function updateNoteCharCount(wrap, length) {
            if (!wrap) return;
            let counter = wrap.querySelector(".tray-line-note-count");
            if (length <= 0) {
                if (counter) counter.remove();
                return;
            }
            if (!counter) {
                counter = document.createElement("span");
                counter.className = "tray-line-note-count";
                counter.setAttribute("aria-live", "polite");
                wrap.appendChild(counter);
            }
            counter.textContent = String(length) + "/200";
        }

        function stopNoteEditorEvent(ev) {
            if (ev) ev.stopPropagation();
        }

        function renderCart() {
            if (isNoteEditorFocused()) return;
            const el = document.getElementById("cart");
            if (!el) return;
            const allLines = Array.from(cartItems.values());
            /* Refresh the per-line `name` from the active-locale catalog so
               existing cart rows always read in the language currently chosen
               by the guest. The cartItem.name was a language snapshot from
               add-time and would otherwise stay frozen when the user toggles
               VI/EN after items are already in the tray. */
            allLines.forEach(function (l) {
                const resolved = localizeLineName(l);
                if (resolved) l.name = resolved;
            });
            const guestCount =
                typeof window.__annapGroupGuestCount !== "undefined" && window.__annapGroupGuestCount != null
                    ? parseInt(window.__annapGroupGuestCount, 10)
                    : 0;
            const hasGic = typeof GuestInteractionContract !== "undefined" && GuestInteractionContract;
            const useGrouped =
                hasGic &&
                typeof GuestInteractionContract.buildGroupOrderTraySections === "function" &&
                typeof window.__annapGroupBrowseActive !== "undefined" &&
                window.__annapGroupBrowseActive &&
                guestCount >= 1;

            if (cartItems.size === 0) {
                el.className = "order-tray-lines order-tray-lines--correspondence pb-1 text-sm";
                if (isPostOrderCheckoutUi(checkoutUi)) {
                    el.innerHTML = "";
                    return;
                }
                const emptyMsg = escapeHtml(tOrder("menuTray.emptyBody"));
                el.innerHTML = `<p class="order-tray-empty">${emptyMsg || trayCopy("menuTray.emptyBody")}</p>`;
                return;
            }

            function descriptorForLine(line) {
                return (line.guestLabel && String(line.guestLabel).trim()) || "";
            }

            function appendMenuLineRow(line, rowIndex) {
                const card = document.createElement("article");
                card.className = "tray-correspondence-card";
                const noteKey = itemNoteKey(line);
                card.setAttribute("data-tray-line-key", noteKey);
                card.style.setProperty("--tray-line-i", String(rowIndex || 0));

                const shadow = document.createElement("div");
                shadow.className = "tray-correspondence-card__shadow";
                shadow.setAttribute("aria-hidden", "true");

                const layer = document.createElement("div");
                layer.className = "tray-correspondence-card__layer";
                layer.setAttribute("aria-hidden", "true");

                const body = document.createElement("div");
                body.className = "tray-correspondence-card__body";

                const visual = document.createElement("div");
                visual.className = "tray-correspondence-card__visual";
                if (line.imageSrc) {
                    const img = document.createElement("img");
                    img.alt = "";
                    img.decoding = "async";
                    img.loading = "lazy";
                    img.src = line.imageSrc;
                    visual.appendChild(img);
                } else {
                    const initialSource = localizeLineName(line) || line.name || "A";
                    visual.textContent = String(initialSource).trim().charAt(0).toUpperCase() || "A";
                }

                const letter = document.createElement("div");
                letter.className = "tray-correspondence-card__letter";
                const noteText = line.customerNote && String(line.customerNote).trim() ? String(line.customerNote).trim() : "";
                const noteExpanded = expandedItemNoteKeys.has(noteKey);
                const notePreviewLabel =
                    tOrder("menuTray.itemNotePreview");
                let noteBlock = "";
                if (noteText && !noteExpanded) {
                    noteBlock = `<p class="tray-line-customer-note tray-line-customer-note--pill">${escapeHtml(
                        notePreviewLabel + " " + noteText
                    )}</p>`;
                }
                const descriptor = descriptorForLine(line);
                const descriptorHtml = descriptor
                    ? `<p class="tray-correspondence-card__note">${escapeHtml(descriptor)}</p>`
                    : "";
                letter.innerHTML = `<p class="tray-correspondence-card__name">${escapeHtml(localizeLineName(line))}</p>
                    ${descriptorHtml}
                    ${noteBlock}`;

                const catalog = catalogRow(line.id);
                const showTemp =
                    !!(catalog && catalog.supportsHotIced) ||
                    !!(line.temperature && String(line.temperature).trim());
                if (showTemp) {
                    const currentTemp = (line.temperature && String(line.temperature).trim()) || "Hot";
                    const tempGroup = document.createElement("div");
                    tempGroup.className = "tray-line-temp";
                    tempGroup.setAttribute("role", "radiogroup");
                    tempGroup.setAttribute(
                        "aria-label",
                        tOrder("menuTray.tempHot") + " / " + tOrder("menuTray.tempIced")
                    );
                    const radioName = "temp-" + noteKey.replace(/[^\w-]/g, "_");
                    ["Hot", "Iced"].forEach(function (val) {
                        const label = document.createElement("label");
                        label.className = "tray-line-temp__opt";
                        const input = document.createElement("input");
                        input.type = "radio";
                        input.name = radioName;
                        input.value = val;
                        input.checked = currentTemp === val;
                        input.addEventListener("change", function () {
                            if (!input.checked) return;
                            if (typeof GuestInteractionContract.setLineTemperature === "function") {
                                GuestInteractionContract.setLineTemperature(
                                    line.id,
                                    val,
                                    line.guestLabel || "",
                                    { fromTemperature: line.temperature || "" }
                                );
                            }
                            linesToCartMap(GuestInteractionContract.getCartLines());
                            renderCart();
                            updateTraySummary();
                        });
                        label.appendChild(input);
                        label.appendChild(
                            document.createTextNode(
                                " " +
                                    (val === "Hot"
                                        ? tOrder("menuTray.tempHot") || "Hot"
                                        : tOrder("menuTray.tempIced") || "Iced")
                            )
                        );
                        tempGroup.appendChild(label);
                    });
                    const nameEl = letter.querySelector(".tray-correspondence-card__name");
                    if (nameEl && nameEl.nextSibling) {
                        letter.insertBefore(tempGroup, nameEl.nextSibling);
                    } else if (nameEl) {
                        nameEl.after(tempGroup);
                    } else {
                        letter.insertBefore(tempGroup, letter.firstChild);
                    }
                }

                const noteToggle = document.createElement("button");
                noteToggle.type = "button";
                noteToggle.className = "tray-line-note-toggle guest-hit";
                noteToggle.textContent =
                    tOrder("menuTray.itemNoteAction") ||
                    trayCopy("menuTray.itemNoteAction");
                noteToggle.addEventListener("click", function (ev) {
                    stopNoteEditorEvent(ev);
                    if (noteExpanded) {
                        commitLineCustomerNote(line, { silent: true });
                        expandedItemNoteKeys.delete(noteKey);
                    } else {
                        expandedItemNoteKeys.add(noteKey);
                    }
                    renderCart();
                });

                const noteWrap = document.createElement("div");
                noteWrap.className = "tray-line-note-wrap";
                if (noteExpanded) {
                    const ta = document.createElement("textarea");
                    ta.className = "tray-line-note-input";
                    ta.rows = 2;
                    ta.maxLength = 200;
                    ta.setAttribute("inputmode", "text");
                    ta.setAttribute("autocomplete", "off");
                    ta.setAttribute("autocorrect", "on");
                    ta.placeholder =
                        tOrder("menuTray.itemNotePlaceholder") ||
                        trayCopy("menuTray.itemNotePlaceholder");
                    ta.value = line.customerNote != null ? String(line.customerNote) : noteText;
                    ta.addEventListener("input", function (ev) {
                        stopNoteEditorEvent(ev);
                        const draft = persistLineCustomerNoteDraft(line, ta.value);
                        if (ta.value !== draft) ta.value = draft;
                        updateNoteCharCount(noteWrap, draft.length);
                    });
                    ta.addEventListener("blur", function () {
                        commitLineCustomerNote(line, { silent: true });
                        refreshNotePillForKey(noteKey);
                    });
                    ["focus", "click", "touchstart", "pointerdown", "mousedown"].forEach(function (evtName) {
                        ta.addEventListener(evtName, stopNoteEditorEvent);
                    });
                    noteWrap.appendChild(ta);
                    if (ta.value.length > 0) updateNoteCharCount(noteWrap, ta.value.length);
                }
                letter.appendChild(noteToggle);
                if (noteExpanded) letter.appendChild(noteWrap);

                const actions = document.createElement("div");
                actions.className = "tray-correspondence-card__actions";

                const qtyRing = document.createElement("div");
                qtyRing.className = "tray-correspondence-card__qty-ring";

                const minus = document.createElement("button");
                minus.type = "button";
                minus.className = "order-tray-qty guest-hit";
                minus.textContent = "\u2212";
                minus.addEventListener("click", () =>
                    setQty(line.id, -1, line.guestLabel, line.temperature)
                );

                const qtyEl = document.createElement("span");
                qtyEl.className = "tray-correspondence-card__qty";
                qtyEl.textContent = String(line.qty);

                const plus = document.createElement("button");
                plus.type = "button";
                plus.className = "order-tray-qty guest-hit";
                plus.textContent = "+";
                plus.addEventListener("click", () =>
                    setQty(line.id, 1, line.guestLabel, line.temperature)
                );

                qtyRing.appendChild(minus);
                qtyRing.appendChild(qtyEl);
                qtyRing.appendChild(plus);

                const lineTotal = document.createElement("span");
                lineTotal.className = "tray-correspondence-card__price";
                lineTotal.textContent = money.format(line.qty * line.unitPrice);

                const removeBtn = document.createElement("button");
                removeBtn.type = "button";
                removeBtn.className = "tray-correspondence-card__release order-tray-remove";
                removeBtn.setAttribute("aria-label", (tOrder("menuTray.remove")) + " " + localizeLineName(line));
                removeBtn.textContent = "×";
                removeBtn.addEventListener("click", () =>
                    removeLine(line.id, line.guestLabel, line.temperature)
                );

                actions.appendChild(qtyRing);
                actions.appendChild(lineTotal);
                actions.appendChild(removeBtn);

                body.appendChild(visual);
                body.appendChild(letter);
                body.appendChild(actions);
                card.appendChild(shadow);
                card.appendChild(layer);
                card.appendChild(body);
                el.appendChild(card);
            }

            el.className = "order-tray-lines order-tray-lines--correspondence pb-1 text-sm";
            el.innerHTML = "";
            let rowIndex = 0;

            if (useGrouped) {
                const sections = GuestInteractionContract.buildGroupOrderTraySections(allLines, guestCount);
                for (let si = 0; si < sections.length; si++) {
                    const sec = sections[si];
                    const head = document.createElement("p");
                    head.className =
                        "mb-1 mt-4 first:mt-0 text-[10px] font-medium tracking-[0.22em] uppercase text-[rgb(var(--accent))]/90";
                    head.textContent = sec.label;
                    el.appendChild(head);
                    if (!sec.lines || sec.lines.length === 0) {
                        const emptyP = document.createElement("p");
                        emptyP.className =
                            "rounded-2xl bg-[rgba(253,249,244,0.55)] px-4 py-3 text-sm text-[rgb(var(--muted))] ring-1 ring-[rgba(212,184,150,0.4)] mb-2";
                        emptyP.textContent =
                            tOrder("menuTray.emptyGuestSection");
                        el.appendChild(emptyP);
                        continue;
                    }
                    for (const line of sec.lines) appendMenuLineRow(line, rowIndex++);
                }
                return;
            }

            const visible =
                hasGic && typeof GuestInteractionContract.filterCartLinesForActiveGuest === "function"
                    ? GuestInteractionContract.filterCartLinesForActiveGuest(allLines)
                    : allLines.slice();

            if (visible.length === 0 && allLines.length > 0) {
                const anyGuestLabel = allLines.some((line) => line.guestLabel && String(line.guestLabel).trim());
                if (!anyGuestLabel) {
                    for (const line of allLines) appendMenuLineRow(line, rowIndex++);
                    return;
                }
                const msg =
                    tOrder("menuTray.emptyGuest");
                el.innerHTML = `<p class="rounded-2xl bg-white/[0.03] px-4 py-8 text-center text-sm text-[rgb(var(--muted))] ring-1 ring-white/[0.06]">${escapeHtml(msg)}</p>`;
                return;
            }

            for (const line of visible) appendMenuLineRow(line, rowIndex++);
        }

        function escapeHtml(s) {
            return String(s)
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;");
        }

        async function submitOrder() {
            syncCartKey();
            const orderResult = document.getElementById("orderResult");
            const btn = document.getElementById("menuSubmitBtn");
            if (orderResult) orderResult.textContent = "";

            if (!VENUE_TABLE_ID) {
                if (orderResult)
                    orderResult.textContent =
                        tOrder("order.needTableScan") || tOrder("order.needTable");
                return;
            }
            if (checkoutUi !== CHECKOUT_UI.PAYMENT_SELECTION) {
                handlePrimaryTrayAction();
                return;
            }

            // ── Branch B: change payment method on an already-submitted order ──
            // Do not call /api/orders. Do not clear the cart. Do not create a new
            // idempotency key. Do not create a new order. Do not touch fulfillment.
            if (pendingPaymentMethodChange) {
                if (paymentMethodChangeInflight) return;
                await submitPaymentMethodChange();
                return;
            }

            // ── Branch A: new order submit ──
            if (cartItems.size === 0) {
                if (orderResult) orderResult.textContent = tOrder("order.minOne");
                return;
            }
            if (!selectedPaymentMethod) {
                if (orderResult) orderResult.textContent = tOrder("checkout.choosePayment");
                return;
            }
            if (!selectedFulfillmentMode) {
                if (orderResult) orderResult.textContent = tOrder("fulfillment.chooseRequired") || tOrder("checkout.choosePayment");
                return;
            }
            if (menuOrderSubmitInFlight) return;
            menuOrderSubmitInFlight = true;
            const prepLabel = getPaymentSubmitLabel(selectedPaymentMethod);
            const submitPaymentMethod = selectedPaymentMethod;
            const submitFulfillmentMode = selectedFulfillmentMode;
            const submitVenueTableId =
                submitFulfillmentMode === FULFILLMENT_MODE.TAKEAWAY ? null : VENUE_TABLE_ID;
            __annapTrayPaymentDevLog("submit start", { paymentMethod: submitPaymentMethod, fulfillmentMode: submitFulfillmentMode });
            try {
                if (!menuOrderIdempotencyKey && window.crypto && typeof window.crypto.randomUUID === "function")
                    menuOrderIdempotencyKey = window.crypto.randomUUID();
                else if (!menuOrderIdempotencyKey) menuOrderIdempotencyKey = String(Date.now());
                if (!menuOrderIdempotencyKey) {
                    if (orderResult)
                        orderResult.textContent =
                            tOrder("order.submitFailed");
                    return;
                }

                if (btn) btn.setAttribute("disabled", "disabled");
                if (btn) {
                    btn.textContent = tOrder("order.submitting");
                    btn.classList.remove("order-tray-submit--success", "order-tray-submit--pulse");
                    btn.classList.add("order-tray-submit--preparing");
                }

                flushAllNoteDraftsForSubmit();

                const items = Array.from(cartItems.values()).map((l) => {
                    const g = (l.guestLabel && String(l.guestLabel).trim()) || "";
                    const item = {
                        menuItemId: l.id,
                        quantity: l.qty,
                        notes: g ? `Guest: ${g}` : null
                    };
                    const cn = l.customerNote && String(l.customerNote).trim() ? String(l.customerNote).trim() : "";
                    if (cn) item.customerNote = cn;
                    let temp = l.temperature && String(l.temperature).trim() ? String(l.temperature).trim() : "";
                    if (!temp) {
                        const row = catalogRow(l.id);
                        if (row && row.supportsHotIced) temp = "Hot";
                    }
                    if (temp) item.temperature = temp;
                    return item;
                });

                let res;
                try {
                    res = await fetch(typeof window.__annapApiUrl === "function" ? window.__annapApiUrl("/api/orders") : "/api/orders", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            Accept: "application/json",
                            "Idempotency-Key": menuOrderIdempotencyKey
                        },
                        body: JSON.stringify({
                            venueTableId: submitVenueTableId,
                            items,
                            idempotencyKey: menuOrderIdempotencyKey,
                            paymentMethod: submitPaymentMethod,
                            fulfillmentMode: submitFulfillmentMode
                        })
                    });
                    __annapTrayPaymentDevLog("POST /api/orders status", res.status);
                } catch (_net) {
                    if (window.GuestOrderQueue)
                        window.GuestOrderQueue.enqueue(submitVenueTableId, items, menuOrderIdempotencyKey, submitPaymentMethod, submitFulfillmentMode);
                    if (orderResult)
                        orderResult.textContent =
                            tOrder("order.queuedSoft");
                    restoreSubmitButtonState(btn, prepLabel);
                    refreshTableIdentityUi();
                    return;
                }

                const payload = await res.json().catch(function () {
                    return null;
                });
                if (!res.ok) {
                    const er = String((payload && payload.error) || "").toLowerCase();
                    let msg =
                        tOrder("order.submitFailedRetry") ||
                        trayCopy("order.submitFailedRetry");
                    if (res.status === 400) {
                        menuOrderIdempotencyKey = null;
                        if (er.includes("scan") || er.includes("table qr") || er.includes("seat"))
                            msg = tOrder("order.needTableScan") || msg;
                        else if (er.includes("table") && (er.includes("not available") || er.includes("inactive")))
                            msg = tOrder("order.tableUnavailable") || msg;
                        else if (er.includes("menu") || er.includes("refresh"))
                            msg = tOrder("order.menuChanged") || msg;
                        else if (er.includes("at least one") || er.includes("tray"))
                            msg = tOrder("order.minOne") || msg;
                    } else if (er === "database_migration_required") {
                        msg =
                            tOrder("order.serverMaintenance");
                    } else if (res.status === 429) {
                        if (window.GuestOrderQueue)
                            window.GuestOrderQueue.enqueue(submitVenueTableId, items, menuOrderIdempotencyKey, submitPaymentMethod, submitFulfillmentMode);
                        msg =
                            tOrder("order.queuedSoft");
                    } else if (res.status >= 500) {
                        msg =
                            tOrder("order.submitFailedStaff");
                    }
                    __annapTrayPaymentDevLog("submit failed", { status: res.status, error: er || null });
                    if (window.InteractionFeedback) {
                        window.InteractionFeedback.trigger("error", { silentVisual: true });
                    }
                    if (orderResult) orderResult.textContent = msg;
                    restoreSubmitButtonState(btn, prepLabel);
                    refreshTableIdentityUi();
                    return;
                }

                if (!payload || !payload.id || !payload.guestSessionToken) {
                    __annapTrayPaymentDevLog("submit invalid payload", {
                        hasPayload: !!payload,
                        hasOrderId: !!(payload && payload.id),
                        hasToken: !!(payload && payload.guestSessionToken)
                    });
                    if (orderResult)
                        orderResult.textContent =
                            tOrder("order.submitFailedRetry") ||
                            trayCopy("order.submitFailedRetry");
                    restoreSubmitButtonState(btn, prepLabel);
                    refreshTableIdentityUi();
                    return;
                }

                menuOrderIdempotencyKey = null;
                applySubmitSuccessUi(payload, submitPaymentMethod, btn, prepLabel);
            } finally {
                menuOrderSubmitInFlight = false;
                if (btn && btn.classList.contains("order-tray-submit--preparing")) {
                    restoreSubmitButtonState(btn, prepLabel);
                }
            }
        }

        /* ── Branch B submit: change PaymentMethod on the SAME submitted order ── */
        async function submitPaymentMethodChange() {
            const sess = readSubmittedSession();
            if (!sess || !sess.orderId || !sess.token) {
                pendingPaymentMethodChange = false;
                paymentMethodChangePreselect = null;
                if (isPostOrderCheckoutUi(checkoutUi)) setCheckoutUi(CHECKOUT_UI.SUBMITTED);
                return;
            }
            if (!selectedPaymentMethod) {
                const orderResult = document.getElementById("orderResult");
                if (orderResult) orderResult.textContent = tOrder("checkout.choosePayment");
                return;
            }
            const btn = document.getElementById("menuSubmitBtn");
            const orderResult = document.getElementById("orderResult");
            if (orderResult) orderResult.textContent = "";
            const prepLabel = getPaymentSubmitLabel(selectedPaymentMethod);
            paymentMethodChangeInflight = true;
            renderCheckoutTray();
            __annapTrayPaymentDevLog("change payment method start", {
                orderId: sess.orderId,
                paymentMethod: selectedPaymentMethod
            });
            try {
                if (btn) {
                    btn.setAttribute("disabled", "disabled");
                    btn.textContent = tOrder("order.submitting");
                    btn.classList.add("order-tray-submit--preparing");
                }
                const url =
                    typeof window.__annapApiUrl === "function"
                        ? window.__annapApiUrl(`/api/orders/${encodeURIComponent(sess.orderId)}/payment-method`)
                        : `/api/orders/${encodeURIComponent(sess.orderId)}/payment-method`;
                let res;
                try {
                    res = await fetch(url, {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            Accept: "application/json"
                        },
                        body: JSON.stringify({
                            paymentMethod: selectedPaymentMethod,
                            token: sess.token
                        })
                    });
                } catch (_net) {
                    if (orderResult) orderResult.textContent = tOrder("order.submitFailedRetry") || trayCopy("order.submitFailedRetry");
                    restoreSubmitButtonState(btn, prepLabel);
                    return;
                }
                const payload = await res.json().catch(function () { return null; });
                if (!res.ok) {
                    const er = String((payload && payload.error) || "").toLowerCase();
                    __annapTrayPaymentDevLog("change payment method failed", { status: res.status, error: er });
                    // 409 (serialization conflict) or 400 payment_method_locked:
                    // server is authoritative — refresh status, reconcile, do NOT show fatal.
                    if (res.status === 409 || er === "payment_method_locked") {
                        const reconciled = await refreshSubmittedTrayStatus().catch(function () { return null; });
                        renderCheckoutTray();
                        if (reconciled && reconciled.celebrated) {
                            // Order was already marked paid — celebration handler takes over.
                        } else {
                            // Show server's current payment method on the submitted card.
                            const nextSess = readSubmittedSession();
                            if (nextSess) {
                                const serverPm = String(nextSess.paymentMethod || "").trim();
                                if (serverPm && selectedPaymentMethod !== serverPm) {
                                    selectedPaymentMethod = serverPm;
                                }
                            }
                        }
                        if (orderResult) orderResult.textContent = "";
                        restoreSubmitButtonState(btn, prepLabel);
                        return;
                    }
                    if (er === "database_migration_required") {
                        if (orderResult) orderResult.textContent = tOrder("order.serverMaintenance");
                    } else if (res.status === 404 || er.includes("not found")) {
                        if (orderResult) orderResult.textContent = tOrder("order.submitFailedStaff");
                    } else if (res.status >= 500) {
                        if (orderResult) orderResult.textContent = tOrder("order.submitFailedStaff");
                    } else {
                        if (orderResult) orderResult.textContent = tOrder("order.submitFailedRetry") || trayCopy("order.submitFailedRetry");
                    }
                    restoreSubmitButtonState(btn, prepLabel);
                    return;
                }
                const serverMethod = (payload && payload.paymentMethod) ? String(payload.paymentMethod) : selectedPaymentMethod;
                // Update only paymentMethod in session; preserve orderId, token, venueTableId, status.
                GuestInteractionContract.writeGuestOrderSession({
                    orderId: sess.orderId,
                    token: sess.token,
                    venueTableId: sess.venueTableId || VENUE_TABLE_ID,
                    submittedAt: sess.submittedAt,
                    status: sess.status,
                    paymentMethod: serverMethod
                });
                pendingPaymentMethodChange = false;
                paymentMethodChangePreselect = null;
                // Reset QR cache so a new fetch uses the latest payment method/order state.
                if (String(serverMethod).trim() !== PAYMENT_METHOD.BANK) {
                    bankTransferQrCache = null;
                    bankTransferQrCacheKey = "";
                    bankTransferQrInflightKey = "";
                }
                traySubmittedStatus = resolveSubmittedCounterState(serverMethod);
                trayKnownPendingPayment = true;
                trayStatusFingerprint = statusFingerprint(readSubmittedSession(), traySubmittedStatus, true);
                // Choose next UI based on new payment method.
                const nextUi =
                    String(serverMethod).trim() === PAYMENT_METHOD.BANK
                        ? CHECKOUT_UI.PAYMENT_BANK_LOADING
                        : CHECKOUT_UI.SUBMITTED;
                setCheckoutUi(nextUi);
                if (nextUi === CHECKOUT_UI.PAYMENT_BANK_LOADING) {
                    void ensureBankTransferQrMounted(readSubmittedSession());
                }
                if (window.InteractionFeedback) {
                    window.InteractionFeedback.trigger("submit", { element: btn || null, silentVisual: true });
                }
                __annapTrayPaymentDevLog("change payment method success", {
                    orderId: sess.orderId,
                    paymentMethod: serverMethod,
                    replay: !!(payload && payload.replay),
                    nextUi: nextUi
                });
                return;
            } finally {
                paymentMethodChangeInflight = false;
                restoreSubmitButtonState(btn, prepLabel);
                renderCheckoutTray();
            }
        }

        function annapBindCheckoutControls() {
            if (window._annapCheckoutControlsBound) return;
            window._annapCheckoutControlsBound = true;
            document.querySelectorAll(".order-tray-payment-option").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    const method = btn.getAttribute("data-payment-method");
                    if (method) {
                        if (method === PAYMENT_METHOD.BANK && bankTransferConfigured === false) return;
                        selectedPaymentMethod = method;
                        if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) {
                            renderPayment();
                            updateCTA();
                        }
                        scrollCheckoutCtaIntoView();
                        return;
                    }
                    const mode = btn.getAttribute("data-fulfillment-mode");
                    if (mode) {
                        selectedFulfillmentMode = mode;
                        if (checkoutUi === CHECKOUT_UI.FULFILLMENT_MODE) {
                            renderFulfillment();
                            updateCTA();
                        }
                        scrollCheckoutCtaIntoView();
                    }
                });
            });
            const backBtn = document.getElementById("order-tray-checkout-back");
            if (backBtn) {
                backBtn.addEventListener("click", function () {
                    // Change-payment branch: returning from PAYMENT_SELECTION must NOT
                    // walk into FULFILLMENT_MODE / REVIEW / EDITING. Go back to SUBMITTED.
                    if (pendingPaymentMethodChange && checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) {
                        pendingPaymentMethodChange = false;
                        paymentMethodChangePreselect = null;
                        paymentMethodChangeInflight = false;
                        if (readSubmittedSession()) {
                            setCheckoutUi(CHECKOUT_UI.SUBMITTED);
                        } else {
                            setCheckoutUi(CHECKOUT_UI.EDITING);
                        }
                        return;
                    }
                    if (checkoutUi === CHECKOUT_UI.PAYMENT_SELECTION) setCheckoutUi(CHECKOUT_UI.FULFILLMENT_MODE);
                    else if (checkoutUi === CHECKOUT_UI.FULFILLMENT_MODE) setCheckoutUi(CHECKOUT_UI.REVIEW);
                    else setCheckoutUi(CHECKOUT_UI.EDITING);
                });
            }
            const changePaymentBtn = document.getElementById("order-tray-submitted-change-payment");
            if (changePaymentBtn && !changePaymentBtn.dataset.annapBound) {
                changePaymentBtn.dataset.annapBound = "1";
                changePaymentBtn.addEventListener("click", function () {
                    if (paymentMethodChangeInflight) return;
                    const sess = readSubmittedSession();
                    if (!sess || !sess.orderId || !sess.token) return;
                    const pm = String(sess.paymentMethod || "").trim() || PAYMENT_METHOD.CASH;
                    selectedPaymentMethod = pm;
                    paymentMethodChangePreselect = pm;
                    pendingPaymentMethodChange = true;
                    setCheckoutUi(CHECKOUT_UI.PAYMENT_SELECTION);
                });
            }
        }

        function renderFulfillment() {
            document.querySelectorAll(".order-tray-fulfillment-option").forEach(function (btn) {
                const mode = btn.getAttribute("data-fulfillment-mode");
                const on = mode && mode === selectedFulfillmentMode;
                btn.classList.toggle("order-tray-payment-option--selected", !!on);
                btn.setAttribute("aria-pressed", on ? "true" : "false");
            });
        }

        function bootMenuTray() {
            if (window._annapMenuTrayBooted) return;
            window._annapMenuTrayBooted = true;
            var trace = __annapMenuBootTraceOn();
            if (trace) {
                try {
                    window.console.groupCollapsed("[ANNAP MENU BOOT]");
                    window.console.log("GuestInteractionContract:", typeof GuestInteractionContract);
                    window.console.log("LuxuryI18n:", typeof window.LuxuryI18n);
                    window.console.log("catalog node:", !!document.getElementById("menu-catalog-json"));
                    window.console.log("guest ctx node:", !!document.getElementById("menu-guest-context"));
                } catch (_g) {}
            }
            __annapTrayLog("[annap] boot menu step 2: tray init");
            __annapTrayPaymentDevLog("Kits order tray payment flow loaded", { version: TRAY_PAYMENT_FLOW_VERSION });
            try {
                setTrayOpen(false, { immediate: true });
                if (trace) try { window.console.log("step: setTrayOpen(false) ok"); } catch (_s0) {}
            } catch (e0) {
                __annapMenuBootErr("[ANNAP MENU BOOT] setTrayOpen(false) failed", e0);
                __annapTrayErr("[annap] menu: setTrayOpen failed", e0);
            }
            try {
                GuestInteractionContract.setTrayOpener(function () {
                    setTrayOpen(true, { userIntent: true });
                });
                if (trace) try { window.console.log("step: setTrayOpener ok"); } catch (_s1) {}
                __annapTrayLog("[annap] menu: tray opener ok");
            } catch (e1) {
                __annapMenuBootErr("[ANNAP MENU BOOT] tray opener failed", e1);
                __annapTrayErr("[annap] menu: tray opener failed", e1);
            }
            try {
                GuestInteractionContract.navigation.applyVtNavPatches();
                if (trace) try { window.console.log("step: nav patches ok"); } catch (_s2) {}
                __annapTrayLog("[annap] menu: nav patches ok");
            } catch (e2) {
                __annapMenuBootErr("[ANNAP MENU BOOT] nav patches failed", e2);
                __annapTrayErr("[annap] menu: nav patches failed", e2);
            }
            try {
                syncCartKey();
                if (trace) try { window.console.log("step: syncCartKey ok"); } catch (_s3) {}
                refreshTableIdentityUi();
                loadCart();
                renderCart();

                /* Server-reconciliation-first boot:
                   ──────────────────────────────
                   The Guest Order Session in localStorage may be stale
                   (e.g. a previous test order, a previous visit, or a
                   server-side completion that the Guest never dismissed).
                   The local `status` field is NOT authoritative.

                   Until we validate against GET /api/track/orders/{id}?token=...
                   we MUST NOT let the tray UI render SUBMITTED / PAID /
                   COMPLETED, because those states can lock the tray.

                   Initial render: EDITING (safe default).
                   Then await server reconciliation.
                   Then update checkoutUi from the authoritative server response
                   and re-render. */
                const sessAtBoot = readSubmittedSession();
                const hasActiveSess =
                    !!(sessAtBoot && sessAtBoot.orderId && sessAtBoot.token);

                checkoutUi = CHECKOUT_UI.EDITING;
                trayStatusFingerprint = "";
                renderCheckoutTray();
                if (trace) try { window.console.log("step: initial EDITING render (awaiting reconcile)"); } catch (_sBoot1) {}

                function finalizeBootAfterReconcile() {
                    const afterSess = readSubmittedSession();
                    if (!afterSess) {
                        checkoutUi = CHECKOUT_UI.EDITING;
                    } else if (!isPostOrderCheckoutUi(checkoutUi) && checkoutUi !== CHECKOUT_UI.PAID && checkoutUi !== CHECKOUT_UI.COMPLETED) {
                        checkoutUi = CHECKOUT_UI.EDITING;
                    } else {
                        /* Server kept the session. checkoutUi is already PAID /
                           SUBMITTED / etc. set by refreshSubmittedTrayStatus. */
                    }
                    trayStatusFingerprint = statusFingerprint(
                        readSubmittedSession(),
                        traySubmittedStatus,
                        trayKnownPendingPayment
                    );
                    renderCheckoutTray();
                    if (shouldPollSubmittedTrayStatus()) startTrayStatusPolling();
                }

                if (hasActiveSess) {
                    /* Awaitable boot reconciliation so the stale session can
                       never paint SUBMITTED / COMPLETED before the server
                       responds. If the network fails we fall back to the
                       EDITING default (never trust a local COMPLETED status). */
                    var bootReconcile = Promise.resolve(
                        refreshSubmittedTrayStatus()
                    );
                    bootReconcile
                        .then(finalizeBootAfterReconcile)
                        .catch(function () {
                            __annapTrayPaymentDevLog("boot reconcile failed; staying EDITING");
                            /* Network error: do not trust local COMPLETED.
                               Keep EDITING. If server is genuinely up but slow,
                               the user can retry by adding an item / reloading. */
                            const afterSess = readSubmittedSession();
                            if (afterSess && (afterSess.status === TRAY_STATE.COMPLETED || afterSess.status === "completed")) {
                                /* Stale COMPLETED in localStorage with no server
                                   confirmation: treat as stale. */
                                resetGuestOrderSessionUi("network_fallback_stale_completed");
                                renderCheckoutTray();
                            } else if (afterSess) {
                                /* Active session but server unreachable: render
                                   SUBMITTED so the Guest can still see they
                                   have an order. Polling will reconcile later. */
                                checkoutUi = checkoutUiFromSession();
                                renderCheckoutTray();
                                startTrayStatusPolling();
                            }
                        });
                } else {
                    /* No session — no reconciliation needed. Already EDITING. */
                    if (shouldPollSubmittedTrayStatus()) startTrayStatusPolling();
                }

                document.body.classList.add("annap-has-order-tray-dock");
                if (trace) try { window.console.log("step: cart render + tray summary ok"); } catch (_s4) {}
                __annapTrayLog("[annap] menu: cart/tray ok");
            } catch (e3) {
                __annapMenuBootErr("[ANNAP MENU BOOT] cart/tray init failed (first fatal area for tray UI)", e3);
                __annapTrayErr("[annap] menu: cart/tray failed", e3);
            }
            try {
                annapBindMenuCatalogInteractions();
                annapBindDrinkDetailModal();
                annapBindCheckoutControls();
                refreshBankTransferAvailability();
                if (trace) try { window.console.log("step: catalog interactions + modal chrome bound"); } catch (_s5) {}
                __annapTrayLog("[annap] menu: catalog interactions bound");
            } catch (e4) {
                __annapMenuBootErr("[ANNAP MENU BOOT] catalog bind failed", e4);
                __annapTrayErr("[annap] menu: catalog bind failed", e4);
            }
            if (trace) {
                try {
                    window.console.log("bootMenuTray: done");
                    window.console.groupEnd();
                } catch (_ge) {}
            }
            __annapTrayLog("[annap] boot menu step 3: tray ready");
            if (window.AnnapGuestBootHarness) {
                AnnapGuestBootHarness.row("hydration", "ok");
                if (typeof AnnapGuestBootHarness.markHydrationDone === "function") AnnapGuestBootHarness.markHydrationDone();
            }
        }

        function annapStartMenuGuestBoot() {
            __annapMenuRuntimeJsonSelfCheck();
            if (annapBootOff("disableI18n")) {
                __annapTrayLog("[annap] menu: i18n skipped (diag/safe)");
                if (window.AnnapGuestBootHarness) AnnapGuestBootHarness.markSkipped("i18n");
                bootMenuTray();
                return;
            }
            var H = window.AnnapGuestBootHarness;
            __annapTrayLog("[annap] menu: starting i18n");
            var p = window.LuxuryI18n && window.LuxuryI18n.ready ? window.LuxuryI18n.ready : Promise.resolve();
            var gate =
                H && typeof H.runTimed === "function"
                    ? H.runTimed("i18n", 5000, function () {
                          return p;
                      })
                    : p;
            gate.then(
                function () {
                    __annapTrayLog("[annap] menu: i18n ok");
                    bootMenuTray();
                },
                function (e) {
                    __annapTrayErr("[annap] menu: i18n timeout/fail", e);
                    bootMenuTray();
                }
            );
        }

        document.addEventListener("luxury:i18n-changed", function () {
            try {
                /* Cart lines carry a `name` snapshot from add-time; re-resolve
                   every line from the active-locale catalog before any UI
                   helper reads it. After this guard the cart/receipt/chip
                   surfaces always render in the guest's current language. */
                cartItems.forEach(function (line) {
                    const resolved = localizeLineName(line);
                    if (resolved) line.name = resolved;
                });
                persistLocalizedCartNames();
                refreshTableIdentityUi();
                renderCart();
                renderCheckoutTray();
            } catch (eI18n) {
                __annapMenuBootErr("[ANNAP MENU BOOT] luxury:i18n-changed handler failed", eI18n);
            }
        });

        document.addEventListener("visibilitychange", function () {
            if (document.visibilityState !== "visible" || !readSubmittedSession()) return;
            refreshSubmittedTrayStatus()
                .then(function () {
                    renderCheckoutTray();
                    if (shouldPollSubmittedTrayStatus()) startTrayStatusPolling();
                })
                .catch(function () {
                    /* ignore */
                });
        });

        document.addEventListener("annap:guest-interaction", function (ev) {
            var d = ev && ev.detail;
            if (!d || (d.type !== "cartUpdated" && d.type !== "activeGuestChanged")) return;
            if (isNoteEditorFocused()) return;
            try {
                linesToCartMap(GuestInteractionContract.getCartLines());
                renderCart();
                updateTraySummary();
            } catch (_m) {
                /* ignore */
            }
        });

        document.addEventListener("annap:tray-chip-settle", function () {
            try {
                updateTraySummary({ animateChip: true });
            } catch (_settle) {
                /* ignore */
            }
        });

        window.__annapRefreshTraySummary = function () {
            updateTraySummary();
        };

window.__annapMenuSubmitOrder = handlePrimaryTrayAction;
window.addToCart = addToCart;
window.openDrinkDetail = openDrinkDetail;
window.closeDrinkDetail = closeDrinkDetail;
window.annapStartOrderTrayDock = annapStartMenuGuestBoot;

/* ── Global overlay interceptor ─────────────────────────────────────────────
   Catches any <a href="/menu/drink/{guid}"> click anywhere on the page and
   converts it to an in-page overlay open instead of full navigation.
   Only fires when #drink-detail-modal is present (seated page context).
   This is the universal safety net so every code path uses the overlay. */
(function () {
    var _re = /\/menu\/drink\/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/i;
    document.addEventListener('click', function (ev) {
        if (!ev || ev.defaultPrevented) return;
        var t = ev.target;
        var a = t && t.closest ? t.closest('a[href]') : null;
        if (!a) return;
        if (!document.getElementById('drink-detail-modal')) return;
        var href = a.getAttribute('href') || '';
        var m = href.match(_re);
        if (!m) return;
        /* Always prevent default so correspondence.js page-transition handler
           sees e.defaultPrevented = true and skips its 290ms navigation timer. */
        ev.preventDefault();
        /* If the anchor also carries data-ge-open-detail, guest-experience.js
           will handle the actual openDrinkDetail call — don't double-invoke. */
        if (!a.hasAttribute('data-ge-open-detail') && typeof window.openDrinkDetail === 'function') {
            void window.openDrinkDetail(m[1]);
        }
    }, true);
}());
