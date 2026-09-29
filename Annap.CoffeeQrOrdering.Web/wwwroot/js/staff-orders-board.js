(function () {
    // ===== TEMP: STAFF PERF FE instrumentation (delete after diagnosis) =====
    // Captures per-action click-to-render timings for prep-done / complete / mark-paid.
    // Does NOT alter fetch, render, or SignalR behavior. Logs one [STAFF PERF FE] line per action.
    let __staffPerfCtx = null;
    function __staffPerfStart(action, orderId, t0) {
        __staffPerfCtx = {
            action,
            orderId,
            t0,
            postStart: 0,
            postHeaders: 0,
            postJson: 0,
            boardStart: 0,
            boardHeaders: 0,
            boardJson: 0,
            render: 0,
            logged: false
        };
    }
    function __staffPerfLogFinal() {
        const ctx = __staffPerfCtx;
        if (!ctx || ctx.logged) return;
        ctx.logged = true;
        __staffPerfCtx = null;
        const r = (n) => Math.max(0, Math.round(n));
        console.warn(
            "[STAFF PERF FE] action=" + ctx.action + " order=" + ctx.orderId +
            " post_headers_ms=" + r(ctx.postHeaders - ctx.postStart) +
            " post_json_ms=" + r(ctx.postJson - ctx.postHeaders) +
            " board_headers_ms=" + r(ctx.boardHeaders - ctx.boardStart) +
            " board_json_ms=" + r(ctx.boardJson - ctx.boardHeaders) +
            " render_ms=" + r(ctx.render - ctx.boardJson) +
            " total_ms=" + r(ctx.render - ctx.t0)
        );
    }
    // ===== /TEMP =====

    const BOARD_COLUMNS = ["submitted", "paid", "completed"];

    const COLUMN_META = {
        submitted: {
            badge: "CHỜ TT",
            empty: "Chưa có đơn chờ thanh toán"
        },
        paid: {
            badge: "ĐÃ TT",
            empty: "Chưa có đơn đang pha chế"
        },
        completed: {
            badge: "XONG",
            empty: "Chưa có đơn hoàn thành gần đây"
        }
    };

    let hubConnection = null;
    let hubRefreshTimer = null;
    let backupPollTimer = null;
    let loadInFlight = false;
    let firstPaint = true;
    let previousIds = new Set();
    let previousColumns = new Map();
    let elapsedTimer = null;
    let lastBoardPulseAt = null;
    let boardPermissions = { canMarkPaid: false, canComplete: false, canPrepareItems: false, canManageBills: false, canGroup: false };
    let activeMobileTab = null;
    let mobileTabInitialized = false;
    let lastBoardData = null;
    let lastBoardGroups = [];
    let primaryForGroup = new Map(); // groupId -> primaryOrderId
    let orderIdToGroupId = new Map(); // orderId -> groupId
    let draggingOrderId = null;
    const expandedItemOrders = new Set();

    const MOBILE_MAX = 767.98;
    const MOBILE_ITEM_PREVIEW = 3;

    const el = (sel) => document.querySelector(sel);

    function isMobileViewport() {
        return window.matchMedia(`(max-width: ${MOBILE_MAX}px)`).matches;
    }

    function tabFromHash() {
        const hash = (window.location.hash || "").replace(/^#/, "").toLowerCase();
        if (hash === "pending") return "submitted";
        if (hash === "preparing") return "paid";
        if (hash === "completed") return "completed";
        return null;
    }

    function hashForTab(tab) {
        if (tab === "submitted") return "#pending";
        if (tab === "paid") return "#preparing";
        if (tab === "completed") return "#completed";
        return "";
    }

    function resolveInitialMobileTab() {
        return tabFromHash() || el(".staff-dash")?.getAttribute("data-staff-default-tab") || "submitted";
    }

    function setMobileTab(tab, opts) {
        if (!BOARD_COLUMNS.includes(tab)) return;
        activeMobileTab = tab;
        mobileTabInitialized = true;
        applyMobileTabVisibility();
        if (!opts?.skipHash && isMobileViewport()) {
            const h = hashForTab(tab);
            if (h && window.history.replaceState) window.history.replaceState(null, "", h);
        }
    }

    function applyMobileTabVisibility() {
        const mobile = isMobileViewport();
        document.body.classList.toggle("staff-board-mobile", mobile);
        for (const col of BOARD_COLUMNS) {
            const section = document.querySelector(`[data-staff-column="${col}"]`);
            if (!section) continue;
            if (mobile) {
                const active = col === activeMobileTab;
                section.classList.toggle("staff-col--active", active);
                section.hidden = !active;
            } else {
                section.classList.add("staff-col--active");
                section.hidden = false;
            }
        }
        document.querySelectorAll("[data-staff-mobile-tab]").forEach((btn) => {
            const on = btn.getAttribute("data-staff-mobile-tab") === activeMobileTab;
            btn.classList.toggle("is-active", on);
            btn.setAttribute("aria-selected", on ? "true" : "false");
        });
    }

    function initMobileTabs() {
        if (!mobileTabInitialized) setMobileTab(resolveInitialMobileTab(), { skipHash: !!tabFromHash() });
        else applyMobileTabVisibility();
    }

    function wireMobileTabs() {
        document.querySelectorAll("[data-staff-mobile-tab]").forEach((btn) => {
            btn.addEventListener("click", () => setMobileTab(btn.getAttribute("data-staff-mobile-tab")));
        });
        window.addEventListener("hashchange", () => {
            const fromHash = tabFromHash();
            if (fromHash && isMobileViewport()) setMobileTab(fromHash, { skipHash: true });
        });
        window.addEventListener("resize", () => {
            if (!mobileTabInitialized) initMobileTabs();
            else applyMobileTabVisibility();
        });
    }

    function showError(msg) {
        const box = el("#staff-error");
        if (!box) return;
        if (!msg) {
            box.classList.add("hidden");
            box.textContent = "";
            return;
        }
        box.textContent = msg;
        box.classList.remove("hidden");
    }

    function fmtClock(iso) {
        if (!iso) return "—";
        const d = new Date(iso);
        return d.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
    }

    function fmtRel(iso) {
        if (!iso) return "—";
        const t = new Date(iso).getTime();
        if (Number.isNaN(t)) return "—";
        const s = Math.max(0, Math.floor((Date.now() - t) / 1000));
        if (s < 60) return `${s}s`;
        if (s < 3600) return `${Math.floor(s / 60)}m`;
        return `${Math.floor(s / 3600)}h`;
    }

    function fmtWaitVi(iso) {
        if (!iso) return "Chờ —";
        const ts = new Date(iso).getTime();
        if (Number.isNaN(ts)) return "Chờ —";
        const mins = Math.max(0, Math.floor((Date.now() - ts) / 60000));
        if (mins < 1) return "Chờ <1 phút";
        if (mins < 60) return `Chờ ${mins} phút`;
        const hours = Math.floor(mins / 60);
        if (hours < 24) return `Chờ ${hours} giờ`;
        const days = Math.floor(hours / 24);
        return `Cũ · ${days} ngày`;
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function colBody(status) {
        return document.querySelector(`[data-staff-drop="${status}"]`);
    }

    function moneyFmt(v) {
        if (window.AnnapMoney && typeof window.AnnapMoney.format === "function") {
            return window.AnnapMoney.format(v);
        }
        return String(v);
    }

    function pacingClass(key) {
        return key === "watch" || key === "brisk" ? key : "steady";
    }

    function apiUrl(path) {
        return typeof window.__annapApiUrl === "function" ? window.__annapApiUrl(path) : path;
    }

    function columnMeta(column) {
        return COLUMN_META[column] || COLUMN_META.submitted;
    }

    function orderHasBill(o, column) {
        if (column === "paid" || column === "completed") return true;
        return !!(o.paidAtUtc || o.billNumber);
    }

    function orderItems(o) {
        return o.items || o.Items || [];
    }

    function itemPrepState(it) {
        const qty = Number(it.quantity ?? it.Quantity) || 0;
        const prepared = Number(it.preparedQuantity ?? it.PreparedQuantity) || 0;
        return { qty, prepared, done: qty > 0 && prepared >= qty };
    }

    function orderPrepProgress(o) {
        const items = orderItems(o);
        let total = 0;
        let done = 0;
        items.forEach((it) => {
            const st = itemPrepState(it);
            total += st.qty;
            done += Math.min(st.prepared, st.qty);
        });
        return { done, total, allDone: total > 0 && done >= total };
    }

    function buildPrepControls(it, orderId, column) {
        if (column !== "paid" || !boardPermissions.canPrepareItems) return "";
        const itemId = it.id || it.Id;
        if (!itemId) return "";
        const st = itemPrepState(it);
        const countLabel = `${st.prepared}/${st.qty}`;
        if (st.done) {
            return `<div class="staff-prep-line__actions">
                <span class="staff-prep-line__count">${countLabel}</span>
                <button type="button" class="staff-prep-btn staff-prep-btn--undo guest-hit" data-staff-act="prep-undo" data-order-id="${orderId}" data-item-id="${itemId}">Chưa xong</button>
            </div>`;
        }
        return `<div class="staff-prep-line__actions">
            <span class="staff-prep-line__count">${countLabel}</span>
            <button type="button" class="staff-prep-btn staff-prep-btn--done guest-hit" data-staff-act="prep-done" data-order-id="${orderId}" data-item-id="${itemId}">Xong</button>
        </div>`;
    }

    function buildPrepSection(o, column) {
        const progress = orderPrepProgress(o);
        if (column === "paid" && boardPermissions.canPrepareItems) {
            const pct = progress.total ? Math.round((progress.done / progress.total) * 100) : 0;
            return `<div class="staff-prep-progress">
                <div class="staff-prep-progress__head">
                    <span class="staff-prep-progress__label">Tiến độ pha chế</span>
                    <span class="staff-prep-progress__count">Đã xong ${progress.done}/${progress.total} món</span>
                </div>
                <div class="staff-prep-progress__bar" style="--prep-pct:${pct}%"><span></span></div>
            </div>`;
        }
        if (column === "paid" && !boardPermissions.canPrepareItems && progress.total > 0) {
            return `<p class="staff-prep-progress__count staff-prep-progress__count--readonly">Đã xong ${progress.done}/${progress.total} món</p>`;
        }
        if (column === "completed" && progress.total > 0) {
            return `<p class="staff-prep-progress__count staff-prep-progress__count--readonly">Đã xong ${progress.done}/${progress.total} món</p>`;
        }
        return "";
    }

    function buildItemCustomerNoteHtml(it) {
        const customerNoteRaw = it.customerNote || it.CustomerNote || "";
        const trimmed = customerNoteRaw && String(customerNoteRaw).trim() ? String(customerNoteRaw).trim() : "";
        if (!trimmed) return "";
        return `<p class="staff-order-note staff-order-note--item"><span class="staff-order-note__label">Ghi chú:</span> ${escapeHtml(trimmed)}</p>`;
    }

    function buildItemLine(it, orderId, column) {
        const note = buildItemCustomerNoteHtml(it);
        const st = itemPrepState(it);
        const prepReadonly =
            ((column === "paid" && !boardPermissions.canPrepareItems) || column === "completed") && st.qty > 0
                ? `<span class="staff-prep-line__count staff-prep-line__count--readonly">${st.prepared}/${st.qty}</span>`
                : "";
        const prepControls = buildPrepControls(it, orderId, column);
        const preparedByLine =
            st.done && (it.preparedBy || it.PreparedBy)
                ? `<p class="staff-order-card__item-prep-by">Xong bởi: ${escapeHtml(it.preparedBy || it.PreparedBy)}</p>`
                : "";
        return `<li class="staff-order-card__item${st.done ? " staff-order-card__item--prepared" : ""}">
                <div class="staff-order-card__item-row">
                    <span class="staff-order-card__item-name">${escapeHtml(it.name || it.Name)}${
                        it.temperatureLabel || it.temperature
                            ? ` <span class="staff-order-card__item-temp">· ${escapeHtml(it.temperatureLabel || it.temperature)}</span>`
                            : ""
                    }</span>
                    <span class="staff-order-card__item-qty">×${it.quantity}</span>
                    ${prepReadonly}
                </div>
                ${note}
                ${prepControls}
                ${preparedByLine}
            </li>`;
    }

    function buildCardActions(o, column) {
        if (column === "completed") {
            return `<div class="staff-order-card__ops staff-order-card__ops--mobile-solo">
                <button type="button" class="staff-order-back staff-order-back--solo guest-hit" data-staff-act="view-bill" data-order-id="${o.id}">Xem bill</button>
            </div>`;
        }

        const bits = [];
        if (column === "submitted" && boardPermissions.canMarkPaid) {
            bits.push(
                `<button type="button" class="staff-order-advance staff-order-advance--pay guest-hit" data-staff-act="mark-paid" data-order-id="${o.id}">Xác nhận thanh toán</button>`
            );
        }
        if (column === "paid" && boardPermissions.canComplete) {
            const progress = orderPrepProgress(o);
            if (!progress.allDone) {
                bits.push(
                    `<button type="button" class="staff-order-advance staff-order-advance--done guest-hit" disabled>Hoàn thành đơn</button>`
                );
                bits.push(`<p class="staff-prep-complete-hint">Tick đủ món để hoàn thành</p>`);
            } else {
                bits.push(
                    `<button type="button" class="staff-order-advance staff-order-advance--done guest-hit" data-staff-act="complete" data-order-id="${o.id}">Hoàn thành đơn</button>`
                );
            }
        }
        if (orderHasBill(o, column) && column !== "completed") {
            bits.push(
                `<button type="button" class="staff-order-back guest-hit" data-staff-act="view-bill" data-order-id="${o.id}">Xem bill</button>`
            );
        }
        if (!bits.length) return "";
        const solo = bits.length === 1 && (column === "submitted" || (column === "paid" && boardPermissions.canComplete));
        return `<div class="staff-order-card__ops"><div class="staff-order-card__ops-row${solo ? " staff-order-card__ops-row--solo" : ""}">${bits.join("")}</div></div>`;
    }

    function buildItemsList(o, column) {
        const items = orderItems(o);
        const usePreview = isMobileViewport();
        const expanded = expandedItemOrders.has(o.id);
        const limit = usePreview && !expanded ? MOBILE_ITEM_PREVIEW : items.length;
        const visible = items.slice(0, limit);
        const lines = visible.map((it) => buildItemLine(it, o.id, column)).join("");
        const hiddenCount = items.length - MOBILE_ITEM_PREVIEW;
        const more =
            usePreview && !expanded && hiddenCount > 0
                ? `<li class="staff-order-card__item staff-order-card__item--more">
                    <button type="button" class="staff-order-card__expand guest-hit" data-staff-act="expand-items" data-order-id="${o.id}">Xem thêm ${hiddenCount} món</button>
                   </li>`
                : "";
        return `<ul class="staff-order-card__items">${lines || '<li class="staff-order-card__item staff-order-card__item--empty">Không có món</li>'}${more}</ul>`;
    }

    function expandOrderItems(orderId) {
        if (!orderId) return;
        expandedItemOrders.add(orderId);
        if (!lastBoardData) return;
        const board = el("[data-staff-board]");
        if (!board) return;
        for (const col of BOARD_COLUMNS) {
            const o = (lastBoardData[col] || []).find((x) => x.id === orderId);
            if (o) {
                mountCard(board, colBody(col), o, col, col === "completed");
                break;
            }
        }
    }

    function buildCardHtml(o, opts) {
        const column = opts?.column || o.boardColumn || "submitted";
        const meta = columnMeta(column);
        const pendingBadge =
            column === "submitted" && (o.pendingPaymentBadgeVi || o.paymentMethod)
                ? escapeHtml(
                      o.pendingPaymentBadgeVi ||
                          (o.paymentMethod === "BankTransfer"
                              ? "Chuyển khoản · chờ xác nhận"
                              : o.paymentMethod === "Card"
                                ? "Thẻ · chờ thanh toán"
                                : o.paymentMethod === "Cash"
                                  ? "Tiền mặt · chờ thanh toán"
                                  : o.paymentMethod === "CashOrCardAtCounter"
                                    ? "Tại quầy · tiền mặt/thẻ"
                                    : "Chờ thanh toán")
                  )
                : "";
        const paymentLine =
            column === "submitted" && pendingBadge
                ? `<p class="staff-order-card__payment-line">${pendingBadge}</p>`
                : "";
        const paymentBadge =
            column === "submitted" && pendingBadge
                ? `<span class="staff-order-card__payment-badge staff-order-card__payment-badge--desktop">${pendingBadge}</span>`
                : "";
        const stepIso = o.statusChangedAtUtc || o.updatedAtUtc || o.createdAtUtc;
        const isTakeaway = o.isTakeaway === true || o.isTakeaway === "true";
        const fulfillmentBadge = o.fulfillmentModeLabelVi
            ? `<span class="staff-order-card__fulfillment-badge${isTakeaway ? " staff-order-card__fulfillment-badge--takeaway" : ""}">${escapeHtml(o.fulfillmentModeLabelVi)}</span>`
            : "";
        const tableCodeDisplay = isTakeaway
            ? `<span class="staff-order-card__table-code staff-order-card__table-code--takeaway">—</span>`
            : escapeHtml(o.tableCode || "—");
        const tableCode = escapeHtml(o.tableCode || "—");
        const waitBadge =
            column === "submitted"
                ? `<span class="staff-order-card__wait-badge" data-field="elapsed-wait">${fmtWaitVi(o.createdAtUtc)}</span>`
                : "";
        const transferMemoLine =
            column === "submitted" && o.paymentMethod === "BankTransfer" && o.transferMemo
                ? `<p class="staff-order-card__transfer-memo">CK: ${escapeHtml(o.transferMemo)}</p>`
                : "";
        const paidStatusBadge =
            column === "paid"
                ? `<span class="staff-order-card__status-pill staff-order-card__status-pill--paid staff-order-card__status-pill--desktop">Đã thanh toán</span>`
                : "";
        const paidTimeLine =
            column === "paid" && o.paidAtUtc
                ? `<p class="staff-order-card__meta-line staff-order-card__meta-line--mobile">TT <span>${fmtClock(o.paidAtUtc)}</span></p>`
                : column === "completed" && o.paidAtUtc
                  ? `<p class="staff-order-card__meta-line staff-order-card__meta-line--desktop">Thanh toán <span>${fmtClock(o.paidAtUtc)}</span></p>`
                  : "";
        function resolvePaymentMethodLabel(order) {
            if (order.paymentMethodLabelVi) return String(order.paymentMethodLabelVi);
            if (order.paymentMethod === "BankTransfer") return "Chuyển khoản";
            if (order.paymentMethod === "Card") return "Thẻ";
            if (order.paymentMethod === "Cash") return "Tiền mặt";
            if (order.paymentMethod === "CashOrCardAtCounter") return "Tiền mặt/thẻ";
            return "Chưa rõ";
        }
        const confirmedPaymentLine =
            column === "paid" || column === "completed"
                ? `<p class="staff-order-card__meta-line staff-order-card__payment-confirmed">Thanh toán: <span>${escapeHtml(
                      resolvePaymentMethodLabel(o)
                  )}</span></p>`
                : "";
        const confirmerLabel = column === "paid" ? "Thu bởi" : "Xác nhận bởi";
        const confirmedByLine =
            column === "paid" || column === "completed"
                ? (() => {
                      if (o.autoPaymentConfirmed) {
                          return `<p class="staff-order-card__meta-line staff-order-card__confirmer">${confirmerLabel}: <span>Thanh toán tự động</span></p>`;
                      }
                      const name = o.paymentConfirmedBy ? String(o.paymentConfirmedBy).trim() : "";
                      if (!name) return "";
                      const timeSuffix = column === "completed" && o.paidAtUtc ? ` · ${fmtClock(o.paidAtUtc)}` : "";
                      return `<p class="staff-order-card__meta-line staff-order-card__confirmer">${confirmerLabel}: <span>${escapeHtml(name)}${timeSuffix ? escapeHtml(timeSuffix) : ""}</span></p>`;
                  })()
                : "";
        const doneLine =
            column === "completed" && o.completedAtUtc
                ? `<p class="staff-order-card__meta-line staff-order-card__meta-line--desktop">Hoàn thành <span>${fmtClock(o.completedAtUtc)}</span></p>`
                : "";
        const completedByLine =
            column === "completed" && (o.completedBy || o.CompletedBy)
                ? `<p class="staff-order-card__meta-line staff-order-card__confirmer">Hoàn thành bởi: <span>${escapeHtml(o.completedBy || o.CompletedBy)}${o.completedAtUtc ? ` · ${fmtClock(o.completedAtUtc)}` : ""}</span></p>`
                : "";
        const totalLine =
            o.totalAmount != null
                ? `<div class="staff-order-card__total"><span class="staff-order-card__total-label">Tổng</span><span class="staff-order-card__total-amount">${moneyFmt(o.totalAmount)}</span></div>`
                : "";
        const columnBadge = `<span class="staff-order-badge staff-order-badge--${column} staff-order-badge--desktop">${escapeHtml(meta.badge)}</span>`;

        return `<article class="staff-order-card staff-order-card--${column} surface-glass"
                data-order-id="${o.id}" data-staff-status="${o.staffStatus || ""}" data-board-column="${column}"
                data-created-at="${o.createdAtUtc || ""}" data-updated-at="${o.updatedAtUtc || ""}" data-status-changed-at="${stepIso || ""}">
                <div class="staff-order-card__top">
                    <div class="staff-order-card__table-row">
                        <span class="staff-order-card__table-prefix">Bàn</span>
                        <span class="staff-order-card__table-code">${tableCodeDisplay}</span>
                        ${waitBadge}
                    </div>
                    <div class="staff-order-card__badges-row">
                        ${fulfillmentBadge}
                        ${paymentBadge}
                        ${paidStatusBadge}
                        ${columnBadge}
                    </div>
                    ${paymentLine}
                    ${transferMemoLine}
                </div>
                <div class="staff-order-card__meta-block">
                    ${paidTimeLine}
                    ${confirmedPaymentLine}
                    ${confirmedByLine}
                    ${doneLine}
                    ${completedByLine}
                </div>
                ${buildPrepSection(o, column)}
                ${buildItemsList(o, column)}
                ${totalLine}
                ${buildCardActions(o, column)}
            </article>`;
    }

    // ===== Staff Column 3 grouping helpers =====
    // OrderId is the identity. TableCode is display metadata only. The grouped
    // card is a presentation layer; underlying Orders and OrderItems remain
    // unchanged. See /api/staff/order-groups.
    function groupOf(orderId) {
        return orderIdToGroupId.get(orderId) || null;
    }

    function findGroupById(groupId) {
        return lastBoardGroups.find((g) => g.id === groupId) || null;
    }

    function findOrderById(orderId) {
        for (const col of BOARD_COLUMNS) {
            const arr = lastBoardData?.[col] || [];
            const hit = arr.find((o) => o.id === orderId);
            if (hit) return hit;
        }
        return null;
    }

    function collectGroupMemberOrders(group) {
        if (!group) return [];
        return (group.orderIds || []).map((id) => findOrderById(id)).filter(Boolean);
    }

    function buildGroupedCardHtml(primary, memberOrders, group, column) {
        const meta = columnMeta(column);
        const isTakeaway = primary.isTakeaway === true || primary.isTakeaway === "true";
        const tableCodes = group.tableCodes || [];
        const tableCodeDisplay = isTakeaway
            ? `<span class="staff-order-card__table-code staff-order-card__table-code--takeaway">—</span>`
            : escapeHtml(tableCodes.join(", ") || "—");
        const waitBadge =
            column === "submitted"
                ? `<span class="staff-order-card__wait-badge" data-field="elapsed-wait">${fmtWaitVi(primary.createdAtUtc)}</span>`
                : "";
        const aggregate = group.aggregateTotal != null
            ? group.aggregateTotal
            : memberOrders.reduce((sum, o) => sum + (Number(o.totalAmount) || 0), 0);
        const memberCount = group.memberCount || memberOrders.length;
        const groupBadge = `<span class="staff-order-badge staff-order-badge--group staff-order-badge--desktop">Gộp ${memberCount} đơn</span>`;
        const columnBadge = `<span class="staff-order-badge staff-order-badge--${column} staff-order-badge--desktop">${escapeHtml(meta.badge)}</span>`;
        const pendingBadge =
            column === "submitted" && primary.paymentMethod
                ? escapeHtml(
                      primary.pendingPaymentBadgeVi ||
                          (primary.paymentMethod === "BankTransfer"
                              ? "Chuyển khoản · chờ xác nhận"
                              : primary.paymentMethod === "Card"
                                ? "Thẻ · chờ thanh toán"
                                : primary.paymentMethod === "Cash"
                                  ? "Tiền mặt · chờ thanh toán"
                                  : primary.paymentMethod === "CashOrCardAtCounter"
                                    ? "Tại quầy · tiền mặt/thẻ"
                                    : "Chờ thanh toán")
                  )
                : "";
        const paymentLine =
            column === "submitted" && pendingBadge
                ? `<p class="staff-order-card__payment-line">${pendingBadge}</p>`
                : "";
        const paymentBadge =
            column === "submitted" && pendingBadge
                ? `<span class="staff-order-card__payment-badge staff-order-card__payment-badge--desktop">${pendingBadge}</span>`
                : "";
        const transferMemoLine =
            column === "submitted" && primary.paymentMethod === "BankTransfer" && primary.transferMemo
                ? `<p class="staff-order-card__transfer-memo">CK: ${escapeHtml(primary.transferMemo)}</p>`
                : "";
        const paidTimeLine =
            column === "paid" && primary.paidAtUtc
                ? `<p class="staff-order-card__meta-line staff-order-card__meta-line--mobile">TT <span>${fmtClock(primary.paidAtUtc)}</span></p>`
                : column === "completed" && primary.paidAtUtc
                  ? `<p class="staff-order-card__meta-line staff-order-card__meta-line--desktop">Thanh toán <span>${fmtClock(primary.paidAtUtc)}</span></p>`
                  : "";
        const stepIso = primary.statusChangedAtUtc || primary.updatedAtUtc || primary.createdAtUtc;

        // Build member header chip showing how many orders are combined.
        const memberHeaders = memberOrders
            .map((o, idx) => `<span class="staff-order-group__chip" title="${escapeHtml(o.tableCode || "—")}">${escapeHtml(o.tableCode || "—")}</span>`)
            .join('<span class="staff-order-group__chip-sep" aria-hidden="true">·</span>');
        const totalLine =
            aggregate != null
                ? `<div class="staff-order-card__total staff-order-card__total--grouped"><span class="staff-order-card__total-label">Tổng ${memberCount} đơn</span><span class="staff-order-card__total-amount">${moneyFmt(aggregate)}</span></div>`
                : "";

        // Per-member item lists, separated visually. Underlying OrderItems are
        // never modified — these lists are a pure presentation pass.
        const memberSections = memberOrders.map((o, idx) => {
            const heading = memberOrders.length > 1
                ? `<p class="staff-order-group__member-head">Đơn ${escapeHtml(o.tableCode || "—")} · <span>${moneyFmt(Number(o.totalAmount) || 0)}</span></p>`
                : "";
            const lines = (o.items || []).map((it) => buildItemLine(it, o.id, column)).join("");
            return `<section class="staff-order-group__member" data-member-id="${o.id}" data-member-index="${idx}">
                ${heading}
                <ul class="staff-order-card__items">${lines || '<li class="staff-order-card__item staff-order-card__item--empty">Không có món</li>'}</ul>
            </section>`;
        }).join("");

        // "Mark paid" / "Complete" / "View bill" actions remain bound to the
        // primary order only. Secondary orders keep their state untouched.
        const primaryActions = buildCardActions(primary, column);

        return `<article class="staff-order-card staff-order-card--${column} staff-order-card--grouped surface-glass"
                data-order-id="${primary.id}" data-group-id="${group.id}" data-staff-status="${primary.staffStatus || ""}" data-board-column="${column}"
                data-created-at="${primary.createdAtUtc || ""}" data-updated-at="${primary.updatedAtUtc || ""}" data-status-changed-at="${stepIso || ""}">
                <div class="staff-order-card__top">
                    <div class="staff-order-card__table-row">
                        <span class="staff-order-card__table-prefix">Đơn</span>
                        <span class="staff-order-card__table-code staff-order-card__table-code--grouped">${tableCodeDisplay}</span>
                        <span class="staff-order-card__group-count" aria-label="Số đơn trong nhóm">${memberCount} đơn</span>
                        ${waitBadge}
                    </div>
                    <div class="staff-order-card__badges-row">
                        ${groupBadge}
                        ${paymentBadge}
                        ${columnBadge}
                    </div>
                    ${paymentLine}
                    ${transferMemoLine}
                </div>
                <div class="staff-order-card__meta-block">
                    ${paidTimeLine}
                </div>
                <div class="staff-order-group__members">${memberHeaders}</div>
                ${memberSections}
                ${totalLine}
                ${primaryActions}
                <div class="staff-order-group__ops">
                    <button type="button" class="staff-order-group__ungroup guest-hit" data-staff-act="ungroup" data-group-id="${group.id}">Tách gộp</button>
                </div>
            </article>`;
    }

    function renderEmptyNode(text) {
        const p = document.createElement("p");
        p.className = "staff-col-empty staff-col-empty--note";
        p.textContent = text;
        return p;
    }

    function updateColumnBodyState(body, orderCount) {
        if (!body) return;
        body.classList.toggle("staff-col-body--has-cards", orderCount > 0);
        body.classList.toggle("staff-col-body--idle", orderCount === 0);
        body.classList.toggle("staff-col-body--scroll", orderCount >= 4);
    }

    function clearEmptyPlaceholders(body) {
        body.querySelectorAll(".staff-col-empty").forEach((n) => n.remove());
    }

    function ensureEmpty(body, text) {
        if (body.querySelector("article.staff-order-card")) return;
        if (body.querySelector(".staff-col-empty")) return;
        body.appendChild(renderEmptyNode(text));
    }

    function mountCard(board, body, o, column, isServed) {
        const existing = board.querySelector(`article.staff-order-card[data-order-id="${o.id}"]`);
        const prevColumn = previousColumns.get(o.id);
        const isNewOrder = previousIds.size > 0 && !previousIds.has(o.id);
        const movedColumn = prevColumn && prevColumn !== column;

        const wrap = document.createElement("div");
        wrap.innerHTML = buildCardHtml(o, { column, servedColumn: isServed }).trim();
        const card = wrap.firstElementChild;

        if (existing) existing.remove();
        body.appendChild(card);

        if (isNewOrder) {
            card.classList.add("staff-order-card--enter");
            window.setTimeout(() => card.classList.remove("staff-order-card--enter"), 700);
        } else if (movedColumn) {
            card.classList.add("staff-order-card--shift");
            window.setTimeout(() => card.classList.remove("staff-order-card--shift"), 450);
        }

        attachDragAndDrop(card);
        return card;
    }

    function mountGroupedCard(board, body, group, column) {
        const primaryId = group.primaryOrderId;
        if (!primaryId) return null;
        const memberOrders = collectGroupMemberOrders(group);
        if (memberOrders.length === 0) return null;
        const primary = memberOrders[0];
        const existing = board.querySelector(`article.staff-order-card[data-order-id="${primary.id}"][data-group-id="${group.id}"]`);
        if (existing) existing.remove();
        const wrap = document.createElement("div");
        wrap.innerHTML = buildGroupedCardHtml(primary, memberOrders, group, column).trim();
        const card = wrap.firstElementChild;
        if (!card) return null;
        body.appendChild(card);
        attachDragAndDrop(card);
        return card;
    }

    function syncColumn(board, status, orders, emptyText, isServed) {
        const body = colBody(status);
        if (!body) return;
        clearEmptyPlaceholders(body);

        // ── Compute desired state for this column ──
        // Every OrderId that appears in any group anchored in this column must
        // be rendered EXACTLY ONCE via its grouped card, never as a standalone
        // card. Identity is OrderId only; TableCode is presentation metadata.
        const groupedOrderIds = new Set();
        const desiredGroupIds = new Set();
        for (const g of lastBoardGroups) {
            if (g.primaryBoardColumn !== status) continue;
            desiredGroupIds.add(g.id);
            for (const oid of g.orderIds || []) {
                if (oid) groupedOrderIds.add(oid);
            }
        }

        const sorted = [...orders].sort((a, b) => String(a.createdAtUtc).localeCompare(String(b.createdAtUtc)));

        // ── Reconciliation: remove stale cards BEFORE mounting the desired set ──
        // This is the critical fix. Without this, transitioning an Order
        // standalone→grouped leaves the OLD standalone card in the DOM
        // alongside the NEW grouped card (the old standalone is skipped by
        // mountCard because its OrderId is in groupedOrderIds; removeOrphanCards
        // does not touch it because its OrderId is still in the order list).
        // The same applies in reverse when a group is dissolved.
        const desiredStandaloneIds = new Set();
        for (const o of sorted) {
            if (!groupedOrderIds.has(o.id)) desiredStandaloneIds.add(o.id);
        }

        const existingCards = Array.from(body.querySelectorAll("article.staff-order-card"));
        for (const c of existingCards) {
            const groupId = c.getAttribute("data-group-id");
            const orderId = c.getAttribute("data-order-id");
            if (groupId != null) {
                // Grouped card — keep only if its group is still desired in this column.
                if (!desiredGroupIds.has(groupId)) c.remove();
            } else {
                // Standalone card — keep only if its OrderId is still a desired standalone.
                if (!desiredStandaloneIds.has(orderId)) c.remove();
            }
        }

        // ── Render grouped cards (one per group anchored in this column) ──
        for (const g of lastBoardGroups) {
            if (g.primaryBoardColumn !== status) continue;
            const primaryId = g.primaryOrderId;
            if (!primaryId) continue;
            const primary = sorted.find((o) => o.id === primaryId);
            if (!primary) continue;
            mountGroupedCard(board, body, g, status);
        }

        // ── Render ungrouped orders ──
        // Skip any Order whose OrderId is already covered by a grouped card.
        // Reconciliation above has already cleared the old standalone card for
        // every grouped OrderId, so we only need to add cards that should
        // exist after this render.
        for (const o of sorted) {
            if (groupedOrderIds.has(o.id)) continue;
            o.boardColumn = status;
            mountCard(board, body, o, status, isServed);
        }

        if (!body.querySelector("article.staff-order-card")) ensureEmpty(body, emptyText);
        updateColumnBodyState(body, body.querySelectorAll("article.staff-order-card").length);
    }

    function updateColumnCounts(data) {
        const counts = {
            submitted: (data.submitted || []).length,
            paid: (data.paid || []).length,
            completed: (data.completed || []).length
        };
        for (const col of BOARD_COLUMNS) {
            const count = String(counts[col] ?? 0);
            const badge = document.querySelector(`[data-staff-col-count="${col}"]`);
            const tabBadge = document.querySelector(`[data-staff-tab-count="${col}"]`);
            if (badge) badge.textContent = count;
            if (tabBadge) tabBadge.textContent = count;
        }
        updateKpiChips(data);
    }

    function updateKpiChips(data) {
        const submitted = data.submitted || [];
        const paid = data.paid || [];
        const completed = data.completed || [];
        const bankPending = submitted.filter((o) => o.paymentMethod === "BankTransfer").length;
        const awaitingTotal = submitted.reduce((sum, o) => sum + (Number(o.totalAmount) || 0), 0);

        const setKpi = (key, value) => {
            const node = document.querySelector(`[data-staff-kpi="${key}"]`);
            if (node) node.textContent = value;
        };

        setKpi("submitted", String(submitted.length));
        setKpi("paid", String(paid.length));
        setKpi("completed", String(completed.length));
        setKpi("bank-pending", String(bankPending));
        const amountNode = document.querySelector('[data-staff-kpi="awaiting-amount"]');
        if (amountNode) amountNode.textContent = moneyFmt(awaitingTotal);

        const completedChip = document.querySelector('[data-staff-kpi-chip="completed"]');
        if (completedChip) completedChip.classList.toggle("staff-floor-kpi__chip--hidden", completed.length === 0);
    }

    function collectAllIds(data) {
        const s = new Set();
        (data.submitted || []).forEach((o) => s.add(o.id));
        (data.paid || []).forEach((o) => s.add(o.id));
        (data.completed || []).forEach((o) => s.add(o.id));
        return s;
    }

    function removeOrphanCards(board, allIds) {
        board.querySelectorAll("article.staff-order-card").forEach((c) => {
            const id = c.getAttribute("data-order-id");
            if (!allIds.has(id)) {
                c.classList.add("staff-order-card--exit");
                window.setTimeout(() => {
                    if (c.parentNode) c.parentNode.removeChild(c);
                }, 380);
            }
        });
    }

    function mergeBoard(data) {
        lastBoardData = data;
        const board = el("[data-staff-board]");
        if (!board) return;

        if (data.permissions) {
            boardPermissions = {
                canMarkPaid: !!data.permissions.canMarkPaid,
                canComplete: !!data.permissions.canComplete,
                canPrepareItems: !!data.permissions.canPrepareItems,
                canManageBills: !!data.permissions.canManageBills,
                canGroup: !!data.permissions.canGroup || !!data.permissions.canMarkPaid || !!data.permissions.canComplete
            };
        }

        // Track groups state. OrderId is the only identity; TableCode is display.
        lastBoardGroups = Array.isArray(data.groups) ? data.groups : [];
        primaryForGroup = new Map();
        orderIdToGroupId = new Map();
        for (const g of lastBoardGroups) {
            if (g.primaryOrderId) primaryForGroup.set(g.id, g.primaryOrderId);
            for (const oid of g.orderIds || []) orderIdToGroupId.set(oid, g.id);
        }

        syncColumn(board, "submitted", data.submitted || [], COLUMN_META.submitted.empty, false);
        syncColumn(board, "paid", data.paid || [], COLUMN_META.paid.empty, false);
        syncColumn(board, "completed", data.completed || [], COLUMN_META.completed.empty, true);

        const all = collectAllIds(data);
        removeOrphanCards(board, all);

        const nextColumns = new Map();
        for (const col of BOARD_COLUMNS) {
            (data[col] || []).forEach((o) => nextColumns.set(o.id, col));
        }
        previousColumns = nextColumns;
        previousIds = all;
        updateColumnCounts(data);
        applyMobileTabVisibility();

        window.setTimeout(() => {
            for (const st of BOARD_COLUMNS) {
                const b = colBody(st);
                if (b && !b.querySelector("article.staff-order-card")) ensureEmpty(b, COLUMN_META[st].empty);
            }
        }, 420);
    }

    function setSkeletons(visible) {
        document.querySelectorAll(".staff-col-skeleton").forEach((sk) => {
            if (visible) sk.classList.add("is-visible");
            else {
                sk.classList.remove("is-visible");
                sk.remove();
            }
        });
    }

    function updateElapsedAll() {
        const board = el("[data-staff-board]");
        if (!board) return;
        board.querySelectorAll("article.staff-order-card").forEach((card) => {
            if (card.dataset.boardColumn !== "submitted") return;
            const iso = card.getAttribute("data-created-at");
            const wait = card.querySelector('[data-field="elapsed-wait"]');
            if (wait && iso) wait.textContent = fmtWaitVi(iso);
        });
    }

    function setHubUi(state) {
        const dot = document.querySelectorAll(".staff-sync-dot");
        const label = el("#staff-last-sync");
        const mobileLabel = el("#staff-mobile-sync-label");
        dot.forEach((d) => {
            d.classList.remove("staff-sync-dot--live", "staff-sync-dot--soft", "staff-sync-dot--reconnect");
            if (state === "connected") d.classList.add("staff-sync-dot--live");
            else if (state === "reconnecting") d.classList.add("staff-sync-dot--reconnect");
            else d.classList.add("staff-sync-dot--soft");
        });
        const text =
            state === "connected"
                ? "Trực tiếp"
                : state === "reconnecting"
                  ? "Đang kết nối lại…"
                  : state === "connecting"
                    ? "Đang kết nối…"
                    : "Yên tĩnh · làm mới nếu cần";
        if (label) label.textContent = text;
        if (mobileLabel) mobileLabel.textContent = text;
    }

    async function loadBoard(opts) {
        const userRefresh = !!(opts && opts.user);
        if (loadInFlight) return;
        loadInFlight = true;
        if (userRefresh) showError("");
        if (__staffPerfCtx) __staffPerfCtx.boardStart = performance.now();
        const migrationMsg =
            "Cơ sở dữ liệu chưa được cập nhật cho quy trình thanh toán. Vui lòng chạy migration rồi thử lại.";
        try {
            const res = await fetch(apiUrl("/api/staff/orders"), {
                headers: { Accept: "application/json" },
                credentials: "same-origin"
            });
            if (__staffPerfCtx) __staffPerfCtx.boardHeaders = performance.now();
            const data = await res.json().catch(() => null);
            if (__staffPerfCtx) __staffPerfCtx.boardJson = performance.now();
            if (!res.ok) {
                if (data?.error === "database_migration_required") throw new Error(migrationMsg);
                throw new Error("Không thể tải đơn hàng.");
            }
            mergeBoard(data);
            if (__staffPerfCtx) {
                __staffPerfCtx.render = performance.now();
                __staffPerfLogFinal();
            }
            showError("");
            const stamp = el("#staff-last-updated");
            const mobileStamp = el("#staff-mobile-sync-time");
            const nowText = fmtClock(new Date().toISOString());
            if (stamp) stamp.textContent = nowText;
            if (mobileStamp) mobileStamp.textContent = nowText;
        } catch (e) {
            closeBillSheet();
            showError(e.message || "Lỗi mạng.");
            if (__staffPerfCtx) {
                __staffPerfCtx.render = performance.now();
                __staffPerfLogFinal();
            }
        } finally {
            if (firstPaint) {
                firstPaint = false;
                setSkeletons(false);
            }
            loadInFlight = false;
        }
    }

    function scheduleHubRefresh() {
        if (hubRefreshTimer) window.clearTimeout(hubRefreshTimer);
        hubRefreshTimer = window.setTimeout(() => {
            hubRefreshTimer = null;
            loadBoard({});
        }, 280);
    }

    function renderBillHtml(bill) {
        if (!bill) return "";
        const items = bill.items || bill.Items || [];
        const lines = items
            .map((it) => {
                const qty = it.quantity ?? 1;
                const unit = it.unitPrice ?? it.UnitPrice;
                const lineTotal = it.lineTotal ?? (unit != null ? unit * qty : null);
                const itemNoteRaw = it.customerNote || it.CustomerNote || "";
                const itemNoteLine =
                    itemNoteRaw && String(itemNoteRaw).trim()
                        ? `<p class="staff-bill-receipt__line-note">Ghi chú: ${escapeHtml(String(itemNoteRaw).trim())}</p>`
                        : "";
                return `<li class="staff-bill-receipt__line">
                    <div class="staff-bill-receipt__line-main">
                        <span class="staff-bill-receipt__line-name">${escapeHtml(it.name || it.Name)}</span>
                        <span class="staff-bill-receipt__line-qty">×${qty}</span>
                    </div>
                    ${itemNoteLine}
                    <div class="staff-bill-receipt__line-meta">
                        ${unit != null ? `<span>${moneyFmt(unit)}</span>` : ""}
                        ${lineTotal != null ? `<span class="staff-bill-receipt__line-total">${moneyFmt(lineTotal)}</span>` : ""}
                    </div>
                </li>`;
            })
            .join("");
        const paidAt = bill.paidAtUtc ? fmtClock(bill.paidAtUtc) : "—";
        const statusLabel = bill.paidAtUtc ? "Đã thanh toán" : bill.paymentStatusLabelVi || "Chờ thanh toán";
        const methodLabel =
            bill.paymentMethodLabelVi ||
            bill.PaymentMethodLabelVi ||
            (bill.paymentMethod === "BankTransfer"
                ? "Chuyển khoản"
                : bill.paymentMethod === "Card"
                  ? "Thẻ"
                  : bill.paymentMethod === "Cash"
                    ? "Tiền mặt"
                    : bill.paymentMethod === "CashOrCardAtCounter"
                      ? "Tiền mặt/thẻ"
                      : bill.paymentMethod || "—");
        const confirmedByRaw = bill.paymentConfirmedBy || bill.PaymentConfirmedBy || "";
        const confirmedByLabel =
            confirmedByRaw && String(confirmedByRaw).startsWith("bank-webhook:")
                ? "Thanh toán tự động"
                : confirmedByRaw || "—";
        const confirmedBy =
            bill.paidAtUtc
                ? `<div><dt>Xác nhận bởi</dt><dd>${escapeHtml(confirmedByLabel)}</dd></div>`
                : "";
        const completedByRaw = bill.completedBy || bill.CompletedBy || "";
        const completedAt = bill.completedAtUtc ? fmtClock(bill.completedAtUtc) : "";
        const completedBy =
            completedByRaw
                ? `<div><dt>Hoàn thành bởi</dt><dd>${escapeHtml(completedByRaw)}${completedAt ? ` · ${completedAt}` : ""}</dd></div>`
                : "";
        const servingLocationLine = bill.servingLocationVi
            ? `<div><dt>Hình thức</dt><dd>${escapeHtml(bill.servingLocationVi)}</dd></div>`
            : "";
        const methodLine =
            bill.paidAtUtc
                ? `<div><dt>Phương thức</dt><dd>${escapeHtml(methodLabel)}</dd></div>`
                : "";
        return `<div class="staff-bill-receipt">
            <div class="staff-bill-receipt__hero">
                <p class="staff-bill-receipt__bill-label">Mã bill</p>
                <p class="staff-bill-receipt__bill-no">#${escapeHtml(bill.billNumber || "—")}</p>
            </div>
            <dl class="staff-bill-receipt__meta">
                ${servingLocationLine}
                <div><dt>Gửi lúc</dt><dd>${fmtClock(bill.submittedAtUtc)}</dd></div>
                <div><dt>Thanh toán lúc</dt><dd>${paidAt}</dd></div>
                <div><dt>Trạng thái</dt><dd>${escapeHtml(statusLabel)}</dd></div>
                ${methodLine}
                ${confirmedBy}
                ${completedBy}
            </dl>
            <ul class="staff-bill-receipt__items">${lines || '<li class="staff-bill-receipt__line staff-bill-receipt__line--empty">Không có món</li>'}</ul>
            <p class="staff-bill-receipt__grand-total">
                <span>Tổng cộng</span>
                <strong>${moneyFmt(bill.totalAmount)}</strong>
            </p>
            <p class="staff-bill-receipt__thanks">Cảm ơn bạn đã ghé Kits.</p>
        </div>`;
    }

    function openBillSheet(html) {
        if (!html || !String(html).trim()) return;
        const sheet = el("#staff-bill-sheet");
        const body = el("#staff-bill-body");
        if (!sheet || !body) return;
        body.innerHTML = html;
        sheet.classList.add("is-open");
        sheet.classList.remove("hidden");
        sheet.hidden = false;
        document.body.classList.add("staff-bill-open");
    }

    function closeBillSheet() {
        const sheet = el("#staff-bill-sheet");
        const body = el("#staff-bill-body");
        if (!sheet) return;
        sheet.classList.remove("is-open");
        sheet.classList.add("hidden");
        sheet.hidden = true;
        document.body.classList.remove("staff-bill-open");
        if (body) body.innerHTML = "";
    }

    async function markPaid(orderId, cardEl) {
        if (!orderId) return;
        if (cardEl) cardEl.classList.add("staff-order-card--syncing");
        __staffPerfStart("mark-paid", orderId, performance.now());
        try {
            __staffPerfCtx.postStart = performance.now();
            const res = await fetch(apiUrl(`/api/staff/orders/${orderId}/mark-paid`), {
                method: "POST",
                headers: { "Content-Type": "application/json", Accept: "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({})
            });
            __staffPerfCtx.postHeaders = performance.now();
            const j = await res.json().catch(() => null);
            __staffPerfCtx.postJson = performance.now();
            if (!res.ok) throw new Error(j?.error || "Không xác nhận được thanh toán.");
            if (j?.bill) openBillSheet(renderBillHtml(j.bill));
            await loadBoard({});
        } catch (e) {
            showError(e.message || "Lỗi thanh toán.");
            await loadBoard({});
        } finally {
            if (cardEl) cardEl.classList.remove("staff-order-card--syncing");
        }
    }

    async function setItemPrepared(orderId, itemId, body, cardEl) {
        if (!orderId || !itemId) return;
        if (cardEl) cardEl.classList.add("staff-order-card--syncing");
        const __isPrepDone = !!(body && body.isPrepared === true);
        if (__isPrepDone) __staffPerfStart("prep-done", orderId, performance.now());
        try {
            if (__isPrepDone) __staffPerfCtx.postStart = performance.now();
            const res = await fetch(apiUrl(`/api/staff/orders/${orderId}/items/${itemId}/prepared`), {
                method: "POST",
                headers: { "Content-Type": "application/json", Accept: "application/json" },
                credentials: "same-origin",
                body: JSON.stringify(body)
            });
            if (__isPrepDone) __staffPerfCtx.postHeaders = performance.now();
            const j = await res.json().catch(() => null);
            if (__isPrepDone) __staffPerfCtx.postJson = performance.now();
            if (!res.ok) throw new Error(j?.message || j?.error || "Không cập nhật được tiến độ pha chế.");
            await loadBoard({});
        } catch (e) {
            showError(e.message || "Lỗi pha chế.");
            await loadBoard({});
        } finally {
            if (cardEl) cardEl.classList.remove("staff-order-card--syncing");
        }
    }

    async function completeOrder(orderId, cardEl) {
        if (!orderId) return;
        if (cardEl) cardEl.classList.add("staff-order-card--syncing");
        __staffPerfStart("complete", orderId, performance.now());
        try {
            __staffPerfCtx.postStart = performance.now();
            const res = await fetch(apiUrl(`/api/staff/orders/${orderId}/complete`), {
                method: "POST",
                headers: { Accept: "application/json" },
                credentials: "same-origin"
            });
            __staffPerfCtx.postHeaders = performance.now();
            const j = await res.json().catch(() => null);
            __staffPerfCtx.postJson = performance.now();
            if (!res.ok) throw new Error(j?.messageVi || j?.message || j?.error || "Không hoàn thành được đơn.");
            await loadBoard({});
        } catch (e) {
            showError(e.message || "Lỗi hoàn thành.");
            await loadBoard({});
        } finally {
            if (cardEl) cardEl.classList.remove("staff-order-card--syncing");
        }
    }

    async function viewBill(orderId) {
        try {
            const res = await fetch(apiUrl(`/api/staff/orders/${orderId}/bill`), {
                headers: { Accept: "application/json" },
                credentials: "same-origin"
            });
            const j = await res.json().catch(() => null);
            if (!res.ok) throw new Error(j?.error || "Không tải được bill.");
            const bill = j && j.pendingPayment && j.summary ? j.summary : j;
            if (!bill || (!bill.billNumber && !bill.items && !bill.Items && !bill.totalAmount)) {
                throw new Error("Không tải được bill.");
            }
            openBillSheet(renderBillHtml(bill));
        } catch (e) {
            closeBillSheet();
            showError(e.message || "Không tải được bill.");
        }
    }

    function startBackupPoll() {
        stopBackupPoll();
        const cb = el("#staff-backup-poll");
        if (cb?.checked) backupPollTimer = window.setInterval(() => loadBoard({}), 60000);
    }

    function stopBackupPoll() {
        if (backupPollTimer) {
            window.clearInterval(backupPollTimer);
            backupPollTimer = null;
        }
    }

    async function startHub() {
        if (!window.signalR) {
            setHubUi("offline");
            return;
        }
        setHubUi("connecting");
        try {
            hubConnection = new signalR.HubConnectionBuilder()
                .withUrl(typeof window.__annapHubUrl === "function" ? window.__annapHubUrl() : "/hubs/orders")
                .withAutomaticReconnect([0, 3000, 8000, 20000, 45000, 60000])
                .build();
            hubConnection.on("boardRefresh", () => {
                showError("");
                scheduleHubRefresh();
            });
            hubConnection.onreconnecting(() => setHubUi("reconnecting"));
            hubConnection.onreconnected(async () => {
                setHubUi("connected");
                lastBoardPulseAt = null;
                try {
                    await hubConnection.invoke("JoinStaffBoard");
                } catch {
                    /* ignore */
                }
                await loadBoard({});
            });
            hubConnection.onclose(() => setHubUi("offline"));
            await hubConnection.start();
            await hubConnection.invoke("JoinStaffBoard");
            setHubUi("connected");
        } catch (e) {
            console.warn("[annap] staff SignalR failed", e);
            hubConnection = null;
            setHubUi("offline");
        }
    }

    // ===== Staff Column 3 drag/drop wiring =====
    // Native HTML5 drag/drop — keeps the implementation small and reuses the
    // existing card markup. Mobile/tablet staff can use the "Tách gộp" button
    // to dissolve a group if drag is not available.
    function attachDragAndDrop(card) {
        if (!card || !boardPermissions.canGroup) return;
        const orderId = card.getAttribute("data-order-id");
        if (!orderId) return;

        card.setAttribute("draggable", "true");

        card.addEventListener("dragstart", (ev) => {
            draggingOrderId = orderId;
            try {
                ev.dataTransfer.setData("text/plain", orderId);
                ev.dataTransfer.effectAllowed = "move";
            } catch (_) {
                /* IE/edge cases */
            }
            card.classList.add("staff-order-card--dragging");
        });

        card.addEventListener("dragend", () => {
            draggingOrderId = null;
            card.classList.remove("staff-order-card--dragging");
            document
                .querySelectorAll(".staff-order-card--drop-target")
                .forEach((el2) => el2.classList.remove("staff-order-card--drop-target"));
        });

        card.addEventListener("dragover", (ev) => {
            if (!draggingOrderId || draggingOrderId === orderId) return;
            ev.preventDefault();
            try {
                ev.dataTransfer.dropEffect = "move";
            } catch (_) {
                /* noop */
            }
            card.classList.add("staff-order-card--drop-target");
        });

        card.addEventListener("dragleave", () => {
            card.classList.remove("staff-order-card--drop-target");
        });

        card.addEventListener("drop", async (ev) => {
            ev.preventDefault();
            card.classList.remove("staff-order-card--drop-target");
            const droppedId = draggingOrderId || (ev.dataTransfer && ev.dataTransfer.getData("text/plain")) || null;
            draggingOrderId = null;
            if (!droppedId || droppedId === orderId) return;
            // Collect the full set to merge: existing group members + the
            // dropped order. Falls back to just the two if no group exists.
            const targetGroupId = orderIdToGroupId.get(orderId) || null;
            const droppedGroupId = orderIdToGroupId.get(droppedId) || null;
            const ids = new Set();
            if (targetGroupId) {
                const g = findGroupById(targetGroupId);
                (g?.orderIds || []).forEach((id) => ids.add(id));
            }
            if (droppedGroupId && droppedGroupId !== targetGroupId) {
                const g = findGroupById(droppedGroupId);
                (g?.orderIds || []).forEach((id) => ids.add(id));
            }
            if (!ids.has(droppedId)) ids.add(droppedId);
            if (!ids.has(orderId)) ids.add(orderId);
            await requestGroup(Array.from(ids));
        });
    }

    async function requestGroup(orderIds) {
        if (!Array.isArray(orderIds) || orderIds.length < 2) return;
        try {
            const res = await fetch(apiUrl("/api/staff/order-groups"), {
                method: "POST",
                headers: { "Content-Type": "application/json", Accept: "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({ orderIds })
            });
            const j = await res.json().catch(() => null);
            if (!res.ok) {
                showError(j?.error || j?.message || "Không thể gộp đơn.");
                return;
            }
            showError("");
            await loadBoard({});
        } catch (e) {
            showError(e?.message || "Không gộp được đơn.");
        }
    }

    async function ungroup(groupId) {
        if (!groupId) return;
        try {
            const res = await fetch(apiUrl(`/api/staff/order-groups/${groupId}`), {
                method: "DELETE",
                headers: { Accept: "application/json" },
                credentials: "same-origin"
            });
            const j = await res.json().catch(() => null);
            if (!res.ok) {
                showError(j?.error || "Không thể tách đơn.");
                return;
            }
            showError("");
            await loadBoard({});
        } catch (e) {
            showError(e?.message || "Không tách được đơn.");
        }
    }

    function wireUi() {
        el("#staff-refresh-btn")?.addEventListener("click", () => loadBoard({ user: true }));
        el("#staff-mobile-refresh-btn")?.addEventListener("click", () => loadBoard({ user: true }));
        wireMobileTabs();
        el("#staff-backup-poll")?.addEventListener("change", () => {
            if (el("#staff-backup-poll")?.checked) startBackupPoll();
            else stopBackupPoll();
        });
        document.addEventListener("visibilitychange", () => {
            if (document.visibilityState === "visible") loadBoard({});
        });
        document.querySelectorAll("[data-staff-bill-close]").forEach((btn) => {
            btn.addEventListener("click", closeBillSheet);
        });
        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") closeBillSheet();
        });

        document.querySelector("[data-staff-board]")?.addEventListener("click", (e) => {
            const actEl = e.target.closest("[data-staff-act]");
            if (!actEl) return;
            const act = actEl.getAttribute("data-staff-act");
            const oid = actEl.getAttribute("data-order-id");
            const itemId = actEl.getAttribute("data-item-id");
            const card = oid
                ? document.querySelector(`article.staff-order-card[data-order-id="${oid}"]`)
                : null;
            if (act === "mark-paid" && oid) void markPaid(oid, card);
            else if (act === "complete" && oid) void completeOrder(oid, card);
            else if (act === "view-bill" && oid) void viewBill(oid);
            else if (act === "prep-done" && oid && itemId) void setItemPrepared(oid, itemId, { isPrepared: true }, card);
            else if (act === "prep-undo" && oid && itemId) void setItemPrepared(oid, itemId, { isPrepared: false }, card);
            else if (act === "expand-items" && oid) expandOrderItems(oid);
            else if (act === "ungroup") {
                const gid = actEl.getAttribute("data-group-id");
                if (gid) void ungroup(gid);
            }
        });
    }

    function boot() {
        if (!document.querySelector("[data-staff-board]")) return;
        closeBillSheet();
        setSkeletons(true);
        wireUi();
        initMobileTabs();
        void loadBoard({});
        void startHub();
        startBackupPoll();
        if (elapsedTimer) window.clearInterval(elapsedTimer);
        elapsedTimer = window.setInterval(updateElapsedAll, 10000);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot);
    else boot();
})();
