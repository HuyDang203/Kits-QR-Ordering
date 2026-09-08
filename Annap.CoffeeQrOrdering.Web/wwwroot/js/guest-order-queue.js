(function () {
    const keyV2 = "annap_pending_orders_v2";
    const keyV1 = "annap_pending_orders_v1";

    function queueDevOn() {
        var adb = typeof window.AnnapGuestBoot !== "undefined" ? window.AnnapGuestBoot : {};
        return window.__ANNAP_DEBUG === true || adb.showBootChecklist === true;
    }
    function queueInfo() {
        if (!queueDevOn()) return;
        try {
            console.info.apply(console, arguments);
        } catch (_e) {}
    }
    function queueWarn() {
        if (!queueDevOn()) return;
        try {
            console.warn.apply(console, arguments);
        } catch (_e2) {}
    }

    function newIdempotencyKey() {
        try {
            if (window.crypto && typeof window.crypto.randomUUID === "function") return window.crypto.randomUUID();
        } catch (_annap) {
            /* ignore */
        }
        return `q-${Date.now()}-${Math.floor(Math.random() * 1e9)}`;
    }

    function readQueueRaw() {
        try {
            return JSON.parse(localStorage.getItem(keyV2) || "[]");
        } catch (_annapRead) {
            return [];
        }
    }

    function writeQueue(items) {
        localStorage.setItem(keyV2, JSON.stringify(items));
    }

    function migrateV1IfNeeded() {
        let q = readQueueRaw();
        if (q.length > 0) return q;
        try {
            const legacy = JSON.parse(localStorage.getItem(keyV1) || "[]");
            if (!Array.isArray(legacy) || legacy.length === 0) return [];
            q = legacy
                .map((job) => ({
                    venueTableId: job.venueTableId,
                    items: job.items,
                    idempotencyKey: job.idempotencyKey && String(job.idempotencyKey).trim()
                        ? String(job.idempotencyKey).trim()
                        : null,
                    at: job.at || Date.now()
                }))
                .filter((job) => job.idempotencyKey);
            writeQueue(q);
            localStorage.removeItem(keyV1);
        } catch (_annap) {
            /* ignore */
        }
        return q;
    }

    function readQueue() {
        return migrateV1IfNeeded();
    }

    function notify(msg) {
        try {
            if (window.AnnapGuestFeedback && typeof window.AnnapGuestFeedback.showToast === "function") {
                window.AnnapGuestFeedback.showToast(msg);
                return;
            }
            const el = document.getElementById("guestToastText");
            const wrap = document.getElementById("guestToast");
            if (el && wrap) {
                el.textContent = msg;
                wrap.classList.remove("hidden");
                window.setTimeout(function () {
                    wrap.classList.add("hidden");
                }, 5200);
            }
        } catch (_annap) {
            /* ignore */
        }
    }

    async function flush() {
        const q = readQueue();
        if (q.length === 0) return;
        const t0 = typeof performance !== "undefined" && performance.now ? performance.now() : Date.now();
        queueInfo("[annap] order queue flush: start", { jobs: q.length });
        const remaining = [];
        for (const job of q) {
            const idem = job.idempotencyKey && String(job.idempotencyKey).trim()
                ? String(job.idempotencyKey).trim()
                : "";
            if (!idem) {
                queueWarn("[annap] order queue flush: skipped job without idempotency key");
                continue;
            }
            const paymentMethod =
                job.paymentMethod && String(job.paymentMethod).trim()
                    ? String(job.paymentMethod).trim()
                    : "Cash";
            try {
                const ac = new AbortController();
                const flushTo = setTimeout(function () {
                    ac.abort();
                }, 25000);
                let res;
                try {
                    res = await fetch(typeof window.__annapApiUrl === "function" ? window.__annapApiUrl("/api/orders") : "/api/orders", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            Accept: "application/json",
                            "Idempotency-Key": idem
                        },
                        body: JSON.stringify({
                            venueTableId: job.venueTableId,
                            items: job.items,
                            idempotencyKey: idem,
                            paymentMethod: paymentMethod
                        }),
                        signal: ac.signal
                    });
                } finally {
                    clearTimeout(flushTo);
                }
                if (res.status === 201 || res.status === 200) {
                    notify("A held tray just found the floor again — your order went through.");
                    continue;
                }
                remaining.push({ ...job, idempotencyKey: idem, paymentMethod: paymentMethod });
            } catch (e) {
                queueWarn("[annap] order queue flush: job failed (will retry)", e);
                remaining.push({ ...job, idempotencyKey: idem, paymentMethod: paymentMethod });
            }
        }
        writeQueue(remaining);
        const t1 = typeof performance !== "undefined" && performance.now ? performance.now() : Date.now();
        queueInfo("[annap] order queue flush: done", { remaining: remaining.length, ms: Math.round(t1 - t0) });
    }

    window.addEventListener("online", function () {
        document.dispatchEvent(new CustomEvent("annap:network-online"));
        void flush();
    });

    window.GuestOrderQueue = {
        /**
         * @param {string} venueTableId
         * @param {object[]} items
         * @param {string} [idempotencyKey] stable key so retries / flush dedupe with server
         * @param {string} [paymentMethod] Cash | Card | BankTransfer
         */
        enqueue(venueTableId, items, idempotencyKey, paymentMethod) {
            const idem = idempotencyKey && String(idempotencyKey).trim() ? String(idempotencyKey).trim() : "";
            if (!idem) {
                queueWarn("[annap] order queue enqueue: idempotency key required — job not queued");
                return;
            }
            const method =
                paymentMethod && String(paymentMethod).trim()
                    ? String(paymentMethod).trim()
                    : "Cash";
            const q = readQueue().filter((j) => j.idempotencyKey !== idem);
            q.push({ venueTableId, items, idempotencyKey: idem, paymentMethod: method, at: Date.now() });
            writeQueue(q);
            notify("The line softened for a moment — we will place this tray when the room is steady again.");
        },
        flush
    };

    if (typeof window !== "undefined" && window.navigator && window.navigator.onLine) void flush();
})();
