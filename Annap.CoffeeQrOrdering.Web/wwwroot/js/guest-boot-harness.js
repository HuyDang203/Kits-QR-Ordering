/**
 * Guest boot isolation: checklist UI, per-subsystem timeouts, 8s hydration watchdog.
 * Expects window.AnnapGuestBoot from #annap-guest-boot-json (set in _Layout).
 */
(function () {
    "use strict";

    function readCfg() {
        try {
            var el = document.getElementById("annap-guest-boot-json");
            if (!el || !el.textContent) return {};
            return JSON.parse(el.textContent);
        } catch (e) {
            if (window.__ANNAP_DEBUG === true) {
                console.warn("[annap] boot config parse failed", e);
            }
            return {};
        }
    }

    window.AnnapGuestBoot = readCfg();

    function rowEl(id) {
        return document.querySelector('[data-annap-boot-row="' + id + '"]');
    }

    function sym(state) {
        if (state === "ok") return "✓";
        if (state === "fail" || state === "timeout") return "✗";
        if (state === "skip") return "—";
        return "…";
    }

    function devBootOn() {
        var c = window.AnnapGuestBoot || {};
        return window.__ANNAP_DEV_MODE === true && (window.__ANNAP_DEBUG === true || c.showBootChecklist === true);
    }

    function bootLog() {
        if (!devBootOn()) return;
        try {
            console.log.apply(console, arguments);
        } catch (_e) {}
    }

    function bootErr() {
        if (!devBootOn()) return;
        try {
            console.error.apply(console, arguments);
        } catch (_e2) {}
    }

    function setRow(id, state) {
        var li = rowEl(id);
        if (!li) return;
        var label = li.getAttribute("data-label") || id;
        li.setAttribute("data-state", state);
        li.textContent = "[" + sym(state) + "] " + label;
        bootLog("[annap] boot checklist", id, state);
    }

    function plainMenuLimited(reason) {
        var mount = document.querySelector(".guest-main");
        if (!mount) return;
        var box = document.getElementById("annap-plain-menu-fallback");
        if (!box) {
            box = document.createElement("div");
            box.id = "annap-plain-menu-fallback";
            box.setAttribute(
                "style",
                "margin:12px;padding:12px;border:1px solid #a67c52;border-radius:8px;background:#1a1814;color:#eee;font:14px system-ui,sans-serif"
            );
            mount.insertBefore(box, mount.firstChild);
        }
        box.innerHTML =
            "<p><strong>Limited guest mode</strong></p><p style=\"font-size:12px;opacity:.9\">" +
            String(reason || "Boot watchdog") +
            "</p><p style=\"font-size:12px\">Loading menu…</p>";
        fetch(typeof window.__annapApiUrl === "function" ? window.__annapApiUrl("/api/menu") : "/api/menu", {
            headers: { Accept: "application/json" },
            cache: "no-store"
        })
            .then(function (r) {
                if (!r.ok) throw new Error("HTTP " + r.status);
                return r.json();
            })
            .then(function (data) {
                var cats = Array.isArray(data) ? data : data.categories || [];
                var html = ["<h2 style=\"font-size:15px;margin-top:10px\">Menu</h2>"];
                for (var i = 0; i < cats.length; i++) {
                    var c = cats[i];
                    html.push("<h3 style=\"margin:8px 0 4px;font-size:13px\">" + String(c.name || "").replace(/</g, "&lt;") + "</h3><ul style=\"margin:0 0 8px 1.1rem\">");
                    var items = c.items || [];
                    for (var j = 0; j < items.length; j++) {
                        var it = items[j];
                        html.push("<li>" + String(it.name || "").replace(/</g, "&lt;") + "</li>");
                    }
                    html.push("</ul>");
                }
                box.innerHTML =
                    "<p><strong>Limited guest mode</strong></p><p style=\"font-size:11px;opacity:.85\">" +
                    String(reason || "") +
                    "</p>" +
                    html.join("");
                bootLog("[annap] limited guest mode: plain menu rendered");
            })
            .catch(function (e) {
                box.innerHTML =
                    "<p><strong>Limited guest mode</strong></p><p style=\"color:#f88\">Could not load menu: " +
                    String(e && e.message ? e.message : e) +
                    "</p>";
            });
    }

    window.AnnapGuestBootHarness = {
        row: setRow,
        markSkipped: function (id) {
            setRow(id, "skip");
        },
        subsystemTimeoutMs: 5000,
        /**
         * @param {string} name checklist row id
         * @param {number} ms
         * @param {function(): any} work returns T or Promise&lt;T&gt;
         */
        runTimed: function (name, ms, work) {
            return new Promise(function (resolve, reject) {
                var done = false;
                var msVal = ms && ms > 0 ? ms : 5000;
                var to = window.setTimeout(function () {
                    if (done) return;
                    done = true;
                    bootErr("[annap] subsystem timeout:", name, msVal + "ms");
                    setRow(name, "timeout");
                    reject(new Error("annap-timeout:" + name));
                }, msVal);
                try {
                    var out = typeof work === "function" ? work() : work;
                    Promise.resolve(out).then(
                        function (v) {
                            if (done) return;
                            done = true;
                            window.clearTimeout(to);
                            setRow(name, "ok");
                            resolve(v);
                        },
                        function (e) {
                            if (done) return;
                            done = true;
                            window.clearTimeout(to);
                            setRow(name, "fail");
                            bootErr("[annap] subsystem failed:", name, e);
                            reject(e);
                        }
                    );
                } catch (e) {
                    if (done) return;
                    done = true;
                    window.clearTimeout(to);
                    setRow(name, "fail");
                    reject(e);
                }
            });
        },
        markHydrationDone: function () {
            window.__annapGuestHydrationComplete = true;
            var sp = document.getElementById("annap-boot-spinner");
            if (sp) sp.style.display = "none";
        }
    };

    function bootOverlayInit() {
        var cfg = window.AnnapGuestBoot || {};
        var panel = document.getElementById("annap-boot-overlay");
        if (!cfg.showBootChecklist || !panel) return;
        panel.classList.remove("hidden");
        var safeEl = document.getElementById("annap-boot-safe-note");
        if (safeEl) {
            safeEl.textContent = cfg.safeQuery ? "safe=" + cfg.safeQuery : "";
        }
        setRow("css", "ok");
        setRow("js", "ok");
        setRow("viewport", "pending");
        window.requestAnimationFrame(function () {
            setRow("viewport", "ok");
        });
        if (typeof window.GuestOrderQueue === "undefined") setRow("queue", "skip");
        else setRow("queue", "ok");
        if (typeof window.signalR === "undefined") setRow("signalr", "skip");
        else setRow("signalr", "pending");
    }

    function startWatchdog() {
        var cfg = window.AnnapGuestBoot || {};
        if (!cfg.showBootChecklist) {
            return;
        }
        window.setTimeout(function () {
            if (window.__annapGuestHydrationComplete) return;
            bootErr("[annap] hydration watchdog (8s): forcing limited guest mode");
            setRow("hydration", "timeout");
            var sp = document.getElementById("annap-boot-spinner");
            if (sp) sp.style.display = "none";
            plainMenuLimited("Hydration not complete within 8 seconds.");
        }, 8000);
    }

    window.annapLimitedGuestMode = plainMenuLimited;

    function run() {
        bootOverlayInit();
        startWatchdog();
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", run);
    else run();
})();
