/**
 * GuestInteractionContract — small UX coordination layer (not a framework).
 *
 * WHAT THIS IS
 * — Persists guest cart lines (localStorage v3) after venue context is bound
 * — Resolves table / ?vt context and exposes URL helpers + DOM patch helpers for vt
 * — Emits minimal document events for UI feedback (toast, tray, etc.)
 * — Stores guest order session (post-submit) keyed by venue
 *
 * WHAT THIS IS NOT
 * — Not a state machine, lifecycle engine, or event orchestration platform
 * — Not business rules (pricing, availability, order validation stay server-side)
 * — Not a router; navigation helpers only stringify paths + query params
 *
 * Events (`annap:guest-interaction`, detail.type) — UI signals only:
 *   itemAdded | itemRemoved | cartUpdated
 */
(function (global) {
    "use strict";

    var LEGACY_V1 = "annap_cart_v1";
    var LEGACY_V2 = "annap_cart_v2";

    function isGuid(s) {
        return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(s || "").trim());
    }

    function cartKey(venueTableId) {
        var id = String(venueTableId || "").trim();
        return id ? "annap_cart_v3_" + id : "annap_cart_v3_unseated";
    }

    function orderSessionKey(venueTableId) {
        var id = String(venueTableId || "").trim();
        return id ? "annap_guest_order_" + id : "annap_guest_order_none";
    }

    function resolveVenueTableIdImpl(opts) {
        opts = opts || {};
        var server = String(opts.serverVt || "").trim();
        if (isGuid(server)) {
            try {
                sessionStorage.setItem("annap_venue_table_id", server);
            } catch (e) {
                /* ignore */
            }
            return server;
        }
        var ctx = opts.guestCtx;
        /* Trust server-rendered table id whenever it is a valid GUID — do not require `seated`,
           so a JSON mismatch cannot drop the table on the Menu page. */
        if (ctx && isGuid(ctx.venueTableId)) {
            var v = String(ctx.venueTableId);
            try {
                sessionStorage.setItem("annap_venue_table_id", v);
            } catch (e2) {
                /* ignore */
            }
            return v;
        }
        try {
            var u = new URL(global.location.href);
            var q = (u.searchParams.get("vt") || "").trim();
            if (isGuid(q)) {
                try {
                    sessionStorage.setItem("annap_venue_table_id", q);
                } catch (e3) {
                    /* ignore */
                }
                return q;
            }
            var ss = (sessionStorage.getItem("annap_venue_table_id") || "").trim();
            return isGuid(ss) ? ss : "";
        } catch (e4) {
            return "";
        }
    }

    function normalizeGuestLabel(gl) {
        if (gl == null) return "";
        return String(gl).trim();
    }

    function normalizeTemperature(v) {
        if (v == null || v === "") return "";
        var s = String(v).trim();
        if (/^hot$/i.test(s)) return "Hot";
        if (/^iced$/i.test(s)) return "Iced";
        return "";
    }

    function lineKeyMatches(line, menuItemId, guestLabel, temperature) {
        return (
            line.menuItemId === menuItemId &&
            normalizeGuestLabel(line.guestLabel) === guestLabel &&
            normalizeTemperature(line.temperature) === normalizeTemperature(temperature)
        );
    }

    /** When group browse is active, tray UI shows only the active guest's lines unless the page uses grouped sections (buildGroupOrderTraySections). */
    function filterCartLinesForActiveGuest(lines) {
        if (!lines || !lines.length) return [];
        try {
            if (!global.__annapGroupBrowseActive) return lines.slice();
            var gl = normalizeGuestLabel(global.__annapActiveGroupGuestLabel);
            if (!gl) return lines.slice();
            var out = [];
            for (var i = 0; i < lines.length; i++) {
                if (normalizeGuestLabel(lines[i] && lines[i].guestLabel) === gl) out.push(lines[i]);
            }
            return out;
        } catch (eF) {
            return lines.slice();
        }
    }

    /** Guest 1..N sections (in order) plus any extra labels (e.g. Table) for group-order tray UI. */
    function buildGroupOrderTraySections(lines, guestCount) {
        var n = parseInt(guestCount, 10) || 0;
        var sections = [];
        if (!lines || !lines.length || n < 1) return sections;
        var map = {};
        for (var i = 0; i < lines.length; i++) {
            var L = lines[i];
            if (!L) continue;
            var g = normalizeGuestLabel(L.guestLabel);
            if (!g) g = "Table";
            if (!map[g]) map[g] = [];
            map[g].push(L);
        }
        for (var gi = 1; gi <= n; gi++) {
            var lab = "Guest " + gi;
            sections.push({ label: lab, lines: map[lab] ? map[lab].slice() : [] });
            delete map[lab];
        }
        var extras = Object.keys(map).sort();
        for (var e = 0; e < extras.length; e++) {
            var k = extras[e];
            if (map[k] && map[k].length) sections.push({ label: k, lines: map[k].slice() });
        }
        return sections;
    }

    function normalizeLine(line) {
        if (!line || typeof line !== "object") return null;
        var menuItemId =
            line.menuItemId != null && line.menuItemId !== ""
                ? String(line.menuItemId)
                : line.id != null && line.id !== ""
                  ? String(line.id)
                  : "";
        var qty = Number(line.qty);
        var name = line.name != null ? String(line.name) : "";
        var up = Number(line.unitPrice);
        var guestLabel = normalizeGuestLabel(line.guestLabel);
        var imageSrc = line.imageSrc != null ? String(line.imageSrc) : "";
        var customerNote =
            line.customerNote != null && String(line.customerNote).trim()
                ? String(line.customerNote).trim().slice(0, 200)
                : "";
        var temperature = normalizeTemperature(line.temperature);
        return {
            menuItemId: menuItemId,
            name: name,
            unitPrice: isFinite(up) ? up : 0,
            qty: isFinite(qty) && qty > 0 ? qty : 0,
            guestLabel: guestLabel,
            imageSrc: imageSrc,
            customerNote: customerNote,
            temperature: temperature
        };
    }

    function readLinesRaw(cartKey) {
        try {
            var raw = global.localStorage.getItem(cartKey);
            if (!raw) return [];
            var data = JSON.parse(raw);
            var arr = Array.isArray(data.lines) ? data.lines : [];
            var out = [];
            for (var i = 0; i < arr.length; i++) {
                var l = normalizeLine(arr[i]);
                if (l && l.menuItemId && l.qty > 0) out.push(l);
            }
            return out;
        } catch (e) {
            return [];
        }
    }

    function writeLinesRaw(cartKey, lines) {
        var clean = [];
        for (var i = 0; i < lines.length; i++) {
            var l = normalizeLine(lines[i]);
            if (l && l.menuItemId && l.qty > 0) clean.push(l);
        }
        try {
            global.localStorage.setItem(cartKey, JSON.stringify({ lines: clean }));
        } catch (e) {
            /* ignore */
        }
    }

    function addIncrementRaw(cartKey, payload) {
        var menuItemId = String(payload.menuItemId);
        var name = payload.name != null ? String(payload.name) : "";
        var unitPrice = Number(payload.unitPrice);
        var selectionFallback =
            payload.selectionFallback != null ? String(payload.selectionFallback) : "Selection";
        var guestLabel = normalizeGuestLabel(payload.guestLabel);
        var imageSrc = payload.imageSrc != null ? String(payload.imageSrc) : "";
        var temperature = normalizeTemperature(payload.temperature);
        var lines = readLinesRaw(cartKey);
        var ix = -1;
        for (var i = 0; i < lines.length; i++) {
            if (lineKeyMatches(lines[i], menuItemId, guestLabel, temperature)) {
                ix = i;
                break;
            }
        }
        if (ix >= 0) {
            lines[ix].qty = (Number(lines[ix].qty) || 0) + 1;
            if (name) lines[ix].name = name;
            if (isFinite(unitPrice)) lines[ix].unitPrice = unitPrice;
            if (imageSrc) lines[ix].imageSrc = imageSrc;
            lines[ix].temperature = temperature;
        } else {
            lines.push({
                menuItemId: menuItemId,
                name: name || selectionFallback,
                unitPrice: isFinite(unitPrice) ? unitPrice : 0,
                qty: 1,
                guestLabel: guestLabel,
                imageSrc: imageSrc,
                temperature: temperature
            });
        }
        writeLinesRaw(cartKey, lines);
        return lines;
    }

    function migrateLegacyIntoKey(cartKey, options) {
        options = options || {};
        if (global.localStorage.getItem(cartKey)) return readLinesRaw(cartKey);

        var catalogRow = typeof options.catalogRow === "function" ? options.catalogRow : function () {
            return null;
        };
        var selectionFallback = "Selection";
        if (typeof options.selectionFallback === "function") {
            selectionFallback = String(options.selectionFallback() || "Selection");
        } else if (options.selectionFallback != null) {
            selectionFallback = String(options.selectionFallback);
        }

        try {
            var raw2 = global.localStorage.getItem(LEGACY_V2);
            if (raw2) {
                var parsed2 = JSON.parse(raw2);
                var lines2 = parsed2 && Array.isArray(parsed2.lines) ? parsed2.lines : null;
                if (lines2 && lines2.length) {
                    writeLinesRaw(cartKey, lines2);
                    try {
                        global.localStorage.removeItem(LEGACY_V2);
                    } catch (e) {
                        /* ignore */
                    }
                    try {
                        global.localStorage.removeItem(LEGACY_V1);
                    } catch (e2) {
                        /* ignore */
                    }
                    return readLinesRaw(cartKey);
                }
            }
        } catch (e3) {
            /* ignore */
        }

        var raw = global.localStorage.getItem(LEGACY_V1);
        if (!raw) return readLinesRaw(cartKey);
        var next = [];
        try {
            var parsed = JSON.parse(raw);
            if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
                for (var k in parsed) {
                    if (!Object.prototype.hasOwnProperty.call(parsed, k)) continue;
                    var v = parsed[k];
                    var qty = typeof v === "number" ? v : Number(v);
                    if (!isFinite(qty) || qty <= 0) continue;
                    var row = catalogRow(k);
                    next.push({
                        menuItemId: String(k),
                        name: (row && row.name) || selectionFallback,
                        unitPrice: Number(row && row.price != null ? row.price : 0) || 0,
                        qty: qty
                    });
                }
            }
        } catch (e4) {
            /* ignore */
        }
        try {
            global.localStorage.removeItem(LEGACY_V1);
        } catch (e5) {
            /* ignore */
        }
        if (next.length) writeLinesRaw(cartKey, next);
        return readLinesRaw(cartKey);
    }


    function formatVnd(amount) {
        if (global.AnnapMoney && typeof global.AnnapMoney.format === "function") {
            return global.AnnapMoney.format(amount);
        }
        var n = Math.round(Number(amount) || 0);
        return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 }).format(n) + " ₫";
    }

    function formatMoney(n) {
        return formatVnd(n);
    }

    var _venueId = "";
    var _migrateCatalog = null;
    var _trayOpener = null;

    function emit(type, payload) {
        var detail = { type: type };
        if (payload && typeof payload === "object") {
            for (var k in payload) {
                if (Object.prototype.hasOwnProperty.call(payload, k)) detail[k] = payload[k];
            }
        }
        try {
            document.dispatchEvent(new CustomEvent("annap:guest-interaction", { detail: detail }));
        } catch (e) {
            /* ignore */
        }
    }

    function emitCartUpdated() {
        try {
            runGroupOrderingAutoAdvanceIfNeeded();
        } catch (eAdv) {
            /* ignore */
        }
        emit("cartUpdated", {});
    }

    /** Seated group flow: after first drink for active guest, advance to next guest without a line (session + globals). */
    function runGroupOrderingAutoAdvanceIfNeeded() {
        var vt = String(_venueId || "").trim();
        if (!vt || !isGuid(vt)) return;
        var sk = "annap_ge_group_" + vt;
        var raw;
        try {
            raw = global.sessionStorage.getItem(sk);
        } catch (e0) {
            return;
        }
        if (!raw) return;
        var o;
        try {
            o = JSON.parse(raw);
        } catch (e1) {
            return;
        }
        if (!o || typeof o !== "object") return;
        var c = parseInt(o.count, 10);
        if (!isFinite(c) || c < 1) return;
        var done = o.done;
        if (!Array.isArray(done) || done.length !== c) {
            done = [];
            for (var d = 0; d < c; d++) done.push(false);
        } else {
            done = done.slice();
        }
        var fp = o.firstPassAuto;
        if (!Array.isArray(fp) || fp.length !== c) {
            fp = [];
            for (var f = 0; f < c; f++) fp.push(false);
        } else {
            fp = fp.slice();
        }
        var a = parseInt(o.active, 10);
        if (!isFinite(a) || a < 0 || a >= c) a = 0;

        function guestLabelForIndex(ix) {
            return "Guest " + (ix + 1);
        }
        function cartHasLineFor(ix) {
            var want = guestLabelForIndex(ix);
            var lines = getCartLines();
            for (var i = 0; i < lines.length; i++) {
                var L = lines[i];
                if (normalizeGuestLabel(L && L.guestLabel) === want && (Number(L.qty) || 0) > 0) return true;
            }
            return false;
        }
        function findNextWithoutLine(from) {
            for (var u = from + 1; u < c; u++) if (!cartHasLineFor(u)) return u;
            for (var v = 0; v <= from; v++) if (!cartHasLineFor(v)) return v;
            return -1;
        }

        if (!cartHasLineFor(a) || fp[a]) return;

        fp[a] = true;
        var nextIx = findNextWithoutLine(a);
        if (nextIx >= 0 && nextIx !== a) a = nextIx;

        var out = { count: c, active: a, done: done, firstPassAuto: fp };
        try {
            global.sessionStorage.setItem(sk, JSON.stringify(out));
        } catch (e2) {
            return;
        }
        try {
            global.__annapActiveGroupGuestLabel = guestLabelForIndex(a);
            global.__annapGroupBrowseActive = true;
            global.__annapGroupGuestCount = c;
        } catch (e3) {
            /* ignore */
        }
        try {
            if (global.document && global.document.dispatchEvent) {
                global.document.dispatchEvent(
                    new CustomEvent("annap:guest-interaction", { detail: { type: "activeGuestChanged" } })
                );
            }
        } catch (e4) {
            /* ignore */
        }
    }

    function restoreGroupOrderingGlobalsFromSession() {
        var vt = String(_venueId || "").trim();
        if (!vt || !isGuid(vt)) {
            try {
                global.__annapGroupGuestCount = 0;
                global.__annapGroupBrowseActive = false;
            } catch (eC) {
                /* ignore */
            }
            return;
        }
        var sk = "annap_ge_group_" + vt;
        var raw;
        try {
            raw = global.sessionStorage.getItem(sk);
        } catch (eR) {
            return;
        }
        if (!raw) {
            try {
                global.__annapGroupGuestCount = 0;
                global.__annapGroupBrowseActive = false;
            } catch (eR2) {
                /* ignore */
            }
            return;
        }
        var o;
        try {
            o = JSON.parse(raw);
        } catch (eJ) {
            return;
        }
        if (!o || typeof o !== "object") return;
        var c = parseInt(o.count, 10);
        if (!isFinite(c) || c < 1) return;
        var a = parseInt(o.active, 10);
        if (!isFinite(a) || a < 0 || a >= c) a = 0;
        try {
            global.__annapGroupGuestCount = c;
            global.__annapGroupBrowseActive = true;
            global.__annapActiveGroupGuestLabel = "Guest " + (a + 1);
        } catch (eG) {
            /* ignore */
        }
    }

    function getCartStorageKey() {
        return cartKey(_venueId);
    }

    function ensureMigrated() {
        var key = getCartStorageKey();
        if (_migrateCatalog) migrateLegacyIntoKey(key, _migrateCatalog);
        return key;
    }

    function bindPageContext(opts) {
        opts = opts || {};
        if (opts.migrateCatalog) _migrateCatalog = opts.migrateCatalog;
        _venueId = resolveVenueTableIdImpl(opts);
        navigationApplyVtNavPatches();
        restoreGroupOrderingGlobalsFromSession();
    }

    function navigationDrinkDetailHref(menuItemId) {
        var base = "/menu/drink/" + encodeURIComponent(String(menuItemId));
        var vt = String(_venueId || "").trim();
        return vt && isGuid(vt) ? base + "?vt=" + encodeURIComponent(vt) : base;
    }

    function navigationMenuIndexHref() {
        var vt = String(_venueId || "").trim();
        return vt && isGuid(vt) ? "/Menu/Index?vt=" + encodeURIComponent(vt) : "/Menu/Index";
    }

    function navigationDrinkDetailDataUrl(menuItemId) {
        var id = encodeURIComponent(String(menuItemId));
        var vt = String(_venueId || "").trim();
        return "/menu/drink/" + id + "?handler=Data" + (vt && isGuid(vt) ? "&vt=" + encodeURIComponent(vt) : "");
    }

    function navigationApplyVtNavPatches() {
        try {
            var vt = String(_venueId || "").trim();
            if (!vt || !isGuid(vt)) vt = (sessionStorage.getItem("annap_venue_table_id") || "").trim();
            if (!vt || !isGuid(vt)) return;
            document.querySelectorAll("a[data-guest-vt-link='1']").forEach(function (a) {
                var href = a.getAttribute("href");
                if (!href) return;
                var u = new URL(href, global.location.origin);
                if (!u.searchParams.has("vt")) u.searchParams.set("vt", vt);
                a.setAttribute("href", u.pathname + u.search + u.hash);
            });
        } catch (e) {
            /* ignore */
        }
    }

    function navigationPatchDrinkAnchors(root) {
        if (!root) return;
        var vt = String(_venueId || "").trim();
        if (!isGuid(vt)) return;
        root.querySelectorAll("a[href]").forEach(function (a) {
            try {
                var hrefAttr = a.getAttribute("href");
                if (!hrefAttr) return;
                var u = new URL(hrefAttr, global.location.origin);
                if (!/^\/menu\/drink\//i.test(u.pathname)) return;
                if (!u.searchParams.has("vt")) u.searchParams.set("vt", vt);
                a.setAttribute("href", u.pathname + u.search + u.hash);
            } catch (e2) {
                /* ignore */
            }
        });
    }

    function getCartLines() {
        var key = ensureMigrated();
        return readLinesRaw(key);
    }

    function setCartLines(lines) {
        writeLinesRaw(getCartStorageKey(), lines);
        emitCartUpdated();
    }

    function imageFromPayloadSource(payload) {
        try {
            var el = payload && payload.sourceElement;
            if (!el || !el.closest) return "";
            if (el.dataset && el.dataset.itemImage) return String(el.dataset.itemImage || "").trim();
            var root = el.closest("[data-drink-card], .ge-result-card, .ge-disc-rec, .dd-pairing, .dd-product-card, .dd-passport, article, section");
            var img = root && root.querySelector ? root.querySelector("img") : null;
            return img ? String(img.currentSrc || img.getAttribute("src") || "").trim() : "";
        } catch (_img) {
            return "";
        }
    }

    function addItem(payload) {
        payload = payload || {};
        if (readGuestOrderSession()) removeGuestOrderSession();
        if (!payload.imageSrc) payload.imageSrc = imageFromPayloadSource(payload);
        var key = ensureMigrated();
        addIncrementRaw(key, payload);
        emitCartUpdated();
        emit("itemAdded", {
            menuItemId: payload.menuItemId,
            sourceElement: payload.sourceElement || null,
            name: payload.name || "",
            unitPrice: payload.unitPrice || 0,
            guestLabel: payload.guestLabel || "",
            imageSrc: payload.imageSrc || ""
        });
    }

    function adjustItemQuantity(menuItemId, delta, guestLabel, temperature) {
        var d = Number(delta) || 0;
        if (!d) return;
        ensureMigrated();
        var key = getCartStorageKey();
        var lines = readLinesRaw(key);
        var id = String(menuItemId);
        var gl = normalizeGuestLabel(guestLabel);
        var temp = normalizeTemperature(temperature);
        var ix = -1;
        for (var i = 0; i < lines.length; i++) {
            if (lineKeyMatches(lines[i], id, gl, temp)) {
                ix = i;
                break;
            }
        }
        if (ix < 0) return;
        lines[ix].qty = (Number(lines[ix].qty) || 0) + d;
        if (lines[ix].qty <= 0) lines.splice(ix, 1);
        writeLinesRaw(key, lines);
        emitCartUpdated();
        if (d < 0) emit("itemRemoved", { menuItemId: id });
    }

    function removeItem(menuItemId, guestLabel, temperature) {
        ensureMigrated();
        var key = getCartStorageKey();
        var id = String(menuItemId);
        var gl = normalizeGuestLabel(guestLabel);
        var temp = normalizeTemperature(temperature);
        var lines = readLinesRaw(key).filter(function (l) {
            return !lineKeyMatches(l, id, gl, temp);
        });
        writeLinesRaw(key, lines);
        emit("itemRemoved", { menuItemId: id });
        emitCartUpdated();
    }

    function setLineCustomerNote(menuItemId, note, guestLabel, temperature, options) {
        // Compat: setLineCustomerNote(id, note, gl, options) when 4th arg is options object.
        if (
            temperature != null &&
            typeof temperature === "object" &&
            !Array.isArray(temperature) &&
            options === undefined
        ) {
            options = temperature;
            temperature = options.temperature;
        }
        options = options || {};
        var silent = options.silent === true;
        ensureMigrated();
        var key = getCartStorageKey();
        var id = String(menuItemId);
        var gl = normalizeGuestLabel(guestLabel);
        var temp = normalizeTemperature(
            temperature !== undefined ? temperature : options.temperature
        );
        var raw = note != null ? String(note) : "";
        var stored = silent
            ? (raw.length > 200 ? raw.slice(0, 200) : raw)
            : (raw.trim() ? raw.trim().slice(0, 200) : "");
        var lines = readLinesRaw(key);
        var changed = false;
        for (var i = 0; i < lines.length; i++) {
            if (lineKeyMatches(lines[i], id, gl, temp)) {
                lines[i].customerNote = stored;
                changed = true;
            }
        }
        if (!changed) return;
        writeLinesRaw(key, lines);
        if (!silent) emitCartUpdated();
    }

    function setLineTemperature(menuItemId, temperature, guestLabel, options) {
        options = options || {};
        var silent = options.silent === true;
        ensureMigrated();
        var key = getCartStorageKey();
        var id = String(menuItemId);
        var gl = normalizeGuestLabel(guestLabel);
        var newTemp = normalizeTemperature(temperature);
        var fromTemp = normalizeTemperature(
            options.fromTemperature !== undefined
                ? options.fromTemperature
                : options.currentTemperature !== undefined
                  ? options.currentTemperature
                  : ""
        );
        var lines = readLinesRaw(key);
        var ix = -1;
        if (options.fromTemperature === undefined && options.currentTemperature === undefined) {
            // Fallback: single line for this menu+guest.
            for (var f = 0; f < lines.length; f++) {
                if (lines[f].menuItemId === id && normalizeGuestLabel(lines[f].guestLabel) === gl) {
                    if (ix >= 0) {
                        ix = -1;
                        break;
                    }
                    ix = f;
                    fromTemp = normalizeTemperature(lines[f].temperature);
                }
            }
        } else {
            for (var i = 0; i < lines.length; i++) {
                if (lineKeyMatches(lines[i], id, gl, fromTemp)) {
                    ix = i;
                    break;
                }
            }
        }
        if (ix < 0) return;
        if (normalizeTemperature(lines[ix].temperature) === newTemp) return;

        var collide = -1;
        for (var j = 0; j < lines.length; j++) {
            if (j === ix) continue;
            if (lineKeyMatches(lines[j], id, gl, newTemp)) {
                collide = j;
                break;
            }
        }
        if (collide >= 0) {
            lines[collide].qty = (Number(lines[collide].qty) || 0) + (Number(lines[ix].qty) || 0);
            if (!lines[collide].customerNote && lines[ix].customerNote) {
                lines[collide].customerNote = lines[ix].customerNote;
            }
            lines.splice(ix, 1);
        } else {
            lines[ix].temperature = newTemp;
        }
        writeLinesRaw(key, lines);
        if (!silent) emitCartUpdated();
    }

    function clearCart() {
        ensureMigrated();
        writeLinesRaw(getCartStorageKey(), []);
        emitCartUpdated();
    }

    /** Removes lines labeled Guest 1..N so a new seated group session does not inherit prior per-guest cups. */
    var guestRosterLabelRe = /^Guest\s+\d+$/i;

    function isGuestRosterLabel(gl) {
        return guestRosterLabelRe.test(normalizeGuestLabel(gl));
    }

    function clearGroupGuestLabeledLines() {
        ensureMigrated();
        var key = getCartStorageKey();
        var lines = readLinesRaw(key);
        var next = [];
        for (var i = 0; i < lines.length; i++) {
            if (isGuestRosterLabel(lines[i] && lines[i].guestLabel)) continue;
            next.push(lines[i]);
        }
        if (next.length === lines.length) return;
        writeLinesRaw(key, next);
        emitCartUpdated();
    }

    function readGuestOrderSession() {
        try {
            var raw = global.localStorage.getItem(orderSessionKey(_venueId));
            if (!raw) return null;
            var o = JSON.parse(raw);
            if (!o || !o.orderId || !o.token) return null;
            var storedVt = String(o.venueTableId || "").trim();
            if (storedVt && _venueId && storedVt !== _venueId) {
                removeGuestOrderSession();
                return null;
            }
            return o;
        } catch (e) {
            return null;
        }
    }

    function writeGuestOrderSession(obj) {
        if (!obj || !obj.orderId || !obj.token) return;
        var vt = String(obj.venueTableId || _venueId || "").trim();
        var payload = {
            orderId: String(obj.orderId),
            token: String(obj.token),
            venueTableId: vt,
            submittedAt: obj.submittedAt || new Date().toISOString(),
            status: obj.status || "submittedPendingPayment"
        };
        if (obj.paymentMethod) payload.paymentMethod = String(obj.paymentMethod);
        try {
            global.localStorage.setItem(orderSessionKey(vt || _venueId), JSON.stringify(payload));
        } catch (e) {
            /* ignore */
        }
    }

    function updateGuestOrderSessionStatus(status) {
        var sess = readGuestOrderSession();
        if (!sess) return;
        sess.status = String(status || sess.status || "submittedPendingPayment");
        writeGuestOrderSession(sess);
    }

    function buildGuestTrackUrl(orderId, token) {
        var id = String(orderId || "").trim();
        var tok = String(token || "").trim();
        if (!id || !tok) return "";
        return "/track/" + encodeURIComponent(id) + "?token=" + encodeURIComponent(tok);
    }

    function buildGuestTrackApiUrl(orderId, token) {
        var id = String(orderId || "").trim();
        var tok = String(token || "").trim();
        if (!id || !tok) return "";
        return "/api/track/orders/" + encodeURIComponent(id) + "?token=" + encodeURIComponent(tok);
    }

    function removeGuestOrderSession() {
        try {
            global.localStorage.removeItem(orderSessionKey(_venueId));
        } catch (e) {
            /* ignore */
        }
    }

    function setTrayOpener(fn) {
        _trayOpener = typeof fn === "function" ? fn : null;
    }

    function openTray() {
        var opener = _trayOpener;
        if (!opener) {
            try {
                if (global.__ANNAP_DEBUG === true && typeof console !== "undefined" && console.warn) {
                    console.warn("[annap-guest] openTray: no tray opener; navigating to menu");
                }
            } catch (_l) {}
            try {
                global.location.href = navigationMenuIndexHref();
            } catch (e0) {
                /* ignore */
            }
            return;
        }
        try {
            opener();
            return;
        } catch (e) {
            try {
                if (global.__ANNAP_DEBUG === true && typeof console !== "undefined" && console.warn) {
                    console.warn("[annap-guest] openTray: opener threw", e);
                }
            } catch (_l2) {}
            try {
                if (global.document && global.document.getElementById("guest-experience-root")) {
                    return;
                }
            } catch (_g) {}
        }
        try {
            global.location.href = navigationMenuIndexHref();
        } catch (e2) {
            /* ignore */
        }
    }

    global.GuestInteractionContract = {
        bindPageContext: bindPageContext,
        getVenueTableId: function () {
            return _venueId;
        },
        isGuid: isGuid,
        formatMoney: formatMoney,
        navigation: {
            drinkDetailHref: navigationDrinkDetailHref,
            menuIndexHref: navigationMenuIndexHref,
            drinkDetailDataUrl: navigationDrinkDetailDataUrl,
            applyVtNavPatches: navigationApplyVtNavPatches,
            patchDrinkAnchors: navigationPatchDrinkAnchors
        },
        getCartLines: getCartLines,
        filterCartLinesForActiveGuest: filterCartLinesForActiveGuest,
        buildGroupOrderTraySections: buildGroupOrderTraySections,
        runGroupOrderingAutoAdvanceIfNeeded: runGroupOrderingAutoAdvanceIfNeeded,
        restoreGroupOrderingGlobalsFromSession: restoreGroupOrderingGlobalsFromSession,
        setCartLines: setCartLines,
        addItem: addItem,
        adjustItemQuantity: adjustItemQuantity,
        removeItem: removeItem,
        setLineCustomerNote: setLineCustomerNote,
        setLineTemperature: setLineTemperature,
        clearCart: clearCart,
        clearGroupGuestLabeledLines: clearGroupGuestLabeledLines,
        readGuestOrderSession: readGuestOrderSession,
        writeGuestOrderSession: writeGuestOrderSession,
        updateGuestOrderSessionStatus: updateGuestOrderSessionStatus,
        buildGuestTrackUrl: buildGuestTrackUrl,
        buildGuestTrackApiUrl: buildGuestTrackApiUrl,
        removeGuestOrderSession: removeGuestOrderSession,
        setTrayOpener: setTrayOpener,
        openTray: openTray
    };
})(typeof window !== "undefined" ? window : globalThis);
