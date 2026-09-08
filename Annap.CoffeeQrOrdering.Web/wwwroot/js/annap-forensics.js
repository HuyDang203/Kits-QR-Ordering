/**
 * ANNAP FORENSICS — full app-lifecycle instrumentation.
 * Gate: sessionStorage.annap_forensics = "1"  OR  window.__ANNAP_FORENSICS = true
 * Load this script FIRST in <head> (before any other JS) so History.prototype
 * is patched before any other code can call pushState / replaceState.
 *
 * Usage in DevTools:
 *   sessionStorage.annap_forensics = "1"; location.reload();
 *   // … reproduce the bug …
 *   copy(window.__annapForensicsDump());
 */
(function () {
    'use strict';

    /* ── gate ─────────────────────────────────────────────────────────────── */
    function _enabled() {
        try {
            if (window.__ANNAP_FORENSICS === true) return true;
            if (sessionStorage.getItem('annap_forensics') === '1') return true;
        } catch (_) {}
        return false;
    }
    /* ─────────────────────────────────────────────────────────────────────── */
    /* LIGHTWEIGHT DIAGNOSTICS — always available, no forensics gate required  */
    /* Enable full forensics: sessionStorage.annap_forensics="1"; reload.      */
    /* ─────────────────────────────────────────────────────────────────────── */
    (function () {
        function _lightReport() {
            var r = {};
            try {
                var dm = window.DrinkDetailModal;
                r.overlay = { isOpen: dm ? dm.isOpen() : false };
            } catch (_) { r.overlay = { error: 'DrinkDetailModal unavailable' }; }
            r.scrollLock = {
                guestScrollLock: document.body.classList.contains('guest-scroll-lock'),
                orderTrayLocked: document.body.classList.contains('order-tray-locked'),
                drinkDetailOpen: document.body.classList.contains('drink-detail-open'),
                bodyTop: document.body.style.top || ''
            };
            var mount = document.getElementById('drink-detail-mount');
            r.mountNode = {
                exists: !!mount,
                contentLength: mount ? mount.innerHTML.length : 0,
                childCount: mount ? mount.childNodes.length : 0
            };
            try {
                if (window.performance && window.performance.memory) {
                    var m = window.performance.memory;
                    r.heap = {
                        usedMB: Math.round(m.usedJSHeapSize / 1024 / 1024),
                        totalMB: Math.round(m.totalJSHeapSize / 1024 / 1024),
                        limitMB: Math.round(m.jsHeapSizeLimit / 1024 / 1024)
                    };
                }
            } catch (_) {}
            try {
                var anims = document.getAnimations ? document.getAnimations() : [];
                r.animations = {
                    count: anims.length,
                    running: anims.filter(function (a) { return a.playState === 'running'; }).length
                };
            } catch (_) { r.animations = {}; }
            r.bodyClasses = document.body.className;
            r.capturedAt = new Date().toISOString();
            try { console.log('%c[ANNAP RUNTIME REPORT]', 'color:#8C2318;font-weight:bold', r); } catch (_) {}
            return r;
        }

        function _stressOverlay(n) {
            var cycles = (typeof n === 'number' && n > 0) ? Math.min(n, 500) : 100;
            var dm = window.DrinkDetailModal;
            if (!dm || typeof dm.open !== 'function') {
                try { console.error('[ANNAP STRESS] DrinkDetailModal not available'); } catch (_) {}
                return;
            }
            var testId = null;
            var sels = ['[data-drink-card]', '[data-menu-detail]'];
            for (var si = 0; si < sels.length && !testId; si++) {
                var els = document.querySelectorAll(sels[si]);
                for (var ei = 0; ei < els.length && !testId; ei++) {
                    var cid = els[ei].getAttribute('data-drink-card') || els[ei].getAttribute('data-menu-detail');
                    if (cid && cid.length > 4) testId = cid;
                }
            }
            if (!testId) {
                try { console.error('[ANNAP STRESS] No drink found — navigate to /menu first.'); } catch (_) {}
                return;
            }
            var done = 0, errors = 0;
            var heapBefore = null;
            try { if (window.performance && window.performance.memory) heapBefore = window.performance.memory.usedJSHeapSize; } catch (_) {}
            try { console.log('%c[ANNAP STRESS OVERLAY] ' + cycles + ' cycles on ' + testId, 'color:#8C2318'); } catch (_) {}

            function step() {
                if (done >= cycles) {
                    var heapAfter = null;
                    try { if (window.performance && window.performance.memory) heapAfter = window.performance.memory.usedJSHeapSize; } catch (_) {}
                    var growth = (heapBefore != null && heapAfter != null)
                        ? (Math.round((heapAfter - heapBefore) / 1024) + 'KB') : 'N/A';
                    try {
                        console.log('%c[ANNAP STRESS OVERLAY] DONE', 'color:#8C2318;font-weight:bold',
                            '— cycles:' + cycles + '  errors:' + errors + '  heapGrowth:' + growth);
                        console.log('[ANNAP STRESS] Run window.__ANNAP_RUNTIME_REPORT() for full diagnostics');
                    } catch (_) {}
                    return;
                }
                try {
                    var p = dm.open(testId);
                    Promise.resolve(p).then(function () {
                        window.setTimeout(function () {
                            try { dm.close(); } catch (e) { errors++; }
                            done++;
                            window.setTimeout(step, 60);
                        }, 120);
                    }, function () {
                        errors++;
                        try { dm.close(); } catch (_) {}
                        done++;
                        window.setTimeout(step, 60);
                    });
                } catch (_e) { errors++; done++; window.setTimeout(step, 60); }
            }
            step();
        }

        function _stressReload() {
            var r = {
                menuTrayBooted:        !!window._annapMenuTrayBooted,
                drinkDetailModalBound: !!window._annapDrinkDetailModalBound,
                menuCatalogUiGuard:    !!(document.documentElement.dataset && document.documentElement.dataset.annapMenuCatalogUi),
                trayEscapeBound:       !!window._annapTrayEscapeBound,
                scrollLockState: {
                    guestScrollLock: document.body.classList.contains('guest-scroll-lock'),
                    orderTrayLocked: document.body.classList.contains('order-tray-locked'),
                    bodyTop:         document.body.style.top
                }
            };
            try {
                console.log('%c[ANNAP STRESS RELOAD] Boot-guard state', 'color:#8C2318', r);
                console.warn('[ANNAP STRESS RELOAD] For real reload stress: reload the page 20+ times and run __ANNAP_RUNTIME_REPORT() after each.');
            } catch (_) {}
            return r;
        }

        /* Expose — full forensics mode overrides these below when enabled */
        window.__ANNAP_RUNTIME_REPORT = _lightReport;
        window.__ANNAP_STRESS_OVERLAY = _stressOverlay;
        window.__ANNAP_STRESS_RELOAD  = _stressReload;
    }());

    if (!_enabled()) return;

    /* ── identity ─────────────────────────────────────────────────────────── */
    /* PAGE_ID changes every hard reload (new execution context).
       SESSION_ID survives bfcache restores (stays in sessionStorage). */
    var _pageId = 'P' + Math.random().toString(36).slice(2, 8).toUpperCase() +
                  '-' + Date.now().toString(36).toUpperCase();

    var _sessionId;
    try {
        _sessionId = sessionStorage.getItem('_annap_session_id');
        if (!_sessionId) {
            _sessionId = 'S' + Math.random().toString(36).slice(2, 8).toUpperCase();
            sessionStorage.setItem('_annap_session_id', _sessionId);
        }
    } catch (_) {
        _sessionId = 'S-UNKNOWN';
    }

    /* ── event log ────────────────────────────────────────────────────────── */
    var _log = [];
    var _t0 = Date.now();

    function _ts() { return '+' + (Date.now() - _t0) + 'ms'; }

    function _emit(kind, detail) {
        var entry = {
            ts: _ts(),
            absMs: Date.now(),
            pageId: _pageId,
            sessionId: _sessionId,
            kind: kind,
            url: location.href,
            detail: detail || {}
        };
        _log.push(entry);
        /* Always print — forensics mode means we WANT noise */
        try {
            console.log(
                '%c[ANNAP FORENSIC] ' + kind + ' ' + entry.ts,
                'color:#8C2318;font-weight:bold',
                detail || ''
            );
        } catch (_) {}
    }

    /* ── stack trace helper ───────────────────────────────────────────────── */
    function _stack() {
        try { throw new Error('trace'); } catch (e) {
            return (e.stack || '').split('\n').slice(2).join('\n');
        }
    }

    /* ── navigation-type from PerformanceAPI ─────────────────────────────── */
    function _navType() {
        try {
            var nav = performance.getEntriesByType('navigation')[0];
            return nav ? nav.type : 'unknown';
        } catch (_) { return 'unknown'; }
    }

    /* ── announce self ────────────────────────────────────────────────────── */
    _emit('FORENSICS_BOOT', {
        pageId: _pageId,
        sessionId: _sessionId,
        navType: _navType(),
        href: location.href,
        referrer: document.referrer,
        readyState: document.readyState
    });

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 1. MONKEY-PATCH HISTORY APIs                                            */
    /* ─────────────────────────────────────────────────────────────────────── */
    var _origPushState    = history.pushState.bind(history);
    var _origReplaceState = history.replaceState.bind(history);
    var _origBack         = history.back.bind(history);
    var _origForward      = history.forward.bind(history);
    var _origGo           = history.go.bind(history);

    History.prototype.pushState = function (state, title, url) {
        _emit('history.pushState', { url: url, state: state, stack: _stack() });
        return _origPushState(state, title, url);
    };

    History.prototype.replaceState = function (state, title, url) {
        _emit('history.replaceState', { url: url, state: state, stack: _stack() });
        return _origReplaceState(state, title, url);
    };

    History.prototype.back = function () {
        _emit('history.back ⚠️', { stack: _stack() });
        return _origBack();
    };

    History.prototype.forward = function () {
        _emit('history.forward', { stack: _stack() });
        return _origForward();
    };

    History.prototype.go = function (delta) {
        _emit('history.go', { delta: delta, stack: _stack() });
        return _origGo(delta);
    };

    /* ── location.assign / replace ────────────────────────────────────────── */
    try {
        var _origAssign  = location.assign.bind(location);
        var _origReplace = location.replace.bind(location);

        Location.prototype.assign = function (url) {
            _emit('location.assign ⚠️', { url: url, stack: _stack() });
            return _origAssign(url);
        };

        Location.prototype.replace = function (url) {
            _emit('location.replace ⚠️', { url: url, stack: _stack() });
            return _origReplace(url);
        };
    } catch (_la) {
        _emit('WARN_location_patch_failed', { err: String(_la) });
    }

    /* ── location.href setter via Object.defineProperty ──────────────────── */
    /* NOTE: location.href setter cannot be trapped on most browsers without
       breaking things — we rely on the href-poll below instead. */

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 2. PAGE-LIFECYCLE EVENT LISTENERS (capture phase)                       */
    /* ─────────────────────────────────────────────────────────────────────── */
    var _lifecycleEvents = [
        'beforeunload', 'unload',
        'pagehide', 'pageshow',
        'visibilitychange',
        'popstate', 'hashchange',
        'focus', 'blur'
    ];

    _lifecycleEvents.forEach(function (evName) {
        window.addEventListener(evName, function (ev) {
            var detail = { type: evName };
            if (evName === 'pagehide' || evName === 'pageshow') {
                detail.persisted = ev.persisted;
            }
            if (evName === 'visibilitychange') {
                detail.visibilityState = document.visibilityState;
            }
            if (evName === 'popstate') {
                detail.state = ev.state;
                detail.url   = location.href;
            }
            if (evName === 'hashchange') {
                detail.oldURL = ev.oldURL;
                detail.newURL = ev.newURL;
            }
            _emit('EVENT_' + evName.toUpperCase(), detail);
        }, true); /* capture so we see it before any other handler */
    });

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 3. POLL location.href EVERY 50ms — catch setter changes                 */
    /* ─────────────────────────────────────────────────────────────────────── */
    var _lastHref = location.href;
    setInterval(function () {
        try {
            var cur = location.href;
            if (cur !== _lastHref) {
                _emit('HREF_CHANGED ⚠️', { from: _lastHref, to: cur, stack: _stack() });
                _lastHref = cur;
            }
        } catch (_) {}
    }, 50);

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 4. MUTATIONOBSERVER — modal root attribute changes                       */
    /* ─────────────────────────────────────────────────────────────────────── */
    function _watchModal() {
        var root = document.getElementById('drink-detail-modal');
        if (!root) return false;

        new MutationObserver(function (records) {
            records.forEach(function (r) {
                if (r.type === 'attributes') {
                    _emit('MODAL_ATTR_CHANGE', {
                        attr: r.attributeName,
                        oldValue: r.oldValue,
                        newValue: root.getAttribute(r.attributeName)
                    });
                }
            });
        }).observe(root, {
            attributes: true,
            attributeOldValue: true,
            attributeFilter: ['class', 'data-annap-modal-open', 'aria-hidden', 'style']
        });

        _emit('MODAL_OBSERVER_ATTACHED', { id: root.id });
        return true;
    }

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 5. MUTATIONOBSERVER — body child removal (menu root removal detection)   */
    /* ─────────────────────────────────────────────────────────────────────── */
    function _watchBodyChildren() {
        new MutationObserver(function (records) {
            records.forEach(function (r) {
                if (r.removedNodes.length) {
                    r.removedNodes.forEach(function (node) {
                        if (node.nodeType === 1) { /* Element */
                            _emit('BODY_CHILD_REMOVED ⚠️', {
                                tagName: node.tagName,
                                id: node.id || '(no-id)',
                                className: (node.className || '').toString().slice(0, 80)
                            });
                        }
                    });
                }
            });
        }).observe(document.body, { childList: true });
    }

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 6. FETCH INTERCEPT — flag non-asset fetches on close                    */
    /* ─────────────────────────────────────────────────────────────────────── */
    var _origFetch = window.fetch;
    window.fetch = function (input, init) {
        var url = (typeof input === 'string' ? input : (input && input.url)) || '';
        /* Flag document-type or unexpected fetches; API + static assets are normal */
        var isAsset = /\.(js|css|woff2?|png|jpe?g|webp|svg|ico)(\?|$)/i.test(url);
        var isApi   = /^\/(api|hub|signalr|order)/i.test(url) || url.indexOf('/api/') !== -1;
        if (!isAsset && !isApi) {
            _emit('FETCH_SUSPICIOUS ⚠️', { url: url, stack: _stack() });
        } else {
            _emit('FETCH', { url: url });
        }
        return _origFetch.apply(this, arguments);
    };

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 7. LISTENER COUNT TRACKING                                               */
    /* ─────────────────────────────────────────────────────────────────────── */
    var _listenerCounts = {};
    var _origAddEventListener = EventTarget.prototype.addEventListener;
    EventTarget.prototype.addEventListener = function (type, fn, opts) {
        if (this === window) {
            _listenerCounts[type] = (_listenerCounts[type] || 0) + 1;
            if (_listenerCounts[type] > 1 &&
                (type === 'popstate' || type === 'hashchange' ||
                 type === 'beforeunload' || type === 'pagehide')) {
                _emit('WARN_DUPLICATE_LISTENER', {
                    type: type,
                    count: _listenerCounts[type],
                    stack: _stack()
                });
            }
        }
        return _origAddEventListener.call(this, type, fn, opts);
    };

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 8. DOM-READY: attach MutationObservers                                   */
    /* ─────────────────────────────────────────────────────────────────────── */
    function _onDomReady() {
        _watchBodyChildren();
        if (!_watchModal()) {
            /* modal root might not exist yet; poll briefly */
            var _pollModal = 0;
            var _modalPoll = setInterval(function () {
                if (_watchModal() || ++_pollModal > 60) clearInterval(_modalPoll);
            }, 250);
        }
        _emit('DOM_READY', { navType: _navType() });
        /* Bind overlay counters; retry after 1s for scripts that defer init */
        _bindOverlayCounters();
        window.setTimeout(_bindOverlayCounters, 1000);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', _onDomReady, { once: true });
    } else {
        _onDomReady();
    }

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 9. PUBLIC DUMP HELPER                                                    */
    /* ─────────────────────────────────────────────────────────────────────── */
    window.__annapForensicsDump = function () {
        var overlay = null;
        try {
            if (window.DrinkDetailModal && typeof window.DrinkDetailModal.isOpen === 'function') {
                overlay = { drinkDetailOpen: window.DrinkDetailModal.isOpen() };
            }
        } catch (_ov) {}
        return JSON.stringify({
            pageId: _pageId,
            sessionId: _sessionId,
            capturedAt: new Date().toISOString(),
            logLength: _log.length,
            listenerCounts: _listenerCounts,
            drinkDetail: overlay,
            currentHref: location.href,
            historyLength: history.length,
            navType: _navType(),
            log: _log
        }, null, 2);
    };

    /* also expose raw log array for live DevTools inspection */
    window.__annapForensicsLog = _log;

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 10.5 OVERLAY LIFECYCLE COUNTERS                                          */
    /* ─────────────────────────────────────────────────────────────────────── */
    var _overlayOpens = 0, _overlayCloses = 0, _overlayBound = false;

    function _bindOverlayCounters() {
        if (_overlayBound) return;
        var dm = window.DrinkDetailModal;
        if (!dm || typeof dm.open !== 'function') return;
        _overlayBound = true;
        var _origOpen  = dm.open;
        var _origClose = dm.close;
        dm.open = function () {
            _overlayOpens++;
            _emit('OVERLAY_OPEN', { n: _overlayOpens });
            return _origOpen.apply(dm, arguments);
        };
        dm.close = function () {
            _overlayCloses++;
            _emit('OVERLAY_CLOSE', { n: _overlayCloses, imbalance: _overlayOpens - _overlayCloses });
            return _origClose.apply(dm, arguments);
        };
        _emit('OVERLAY_COUNTERS_BOUND', {});
    }

    /* ── Full __ANNAP_RUNTIME_REPORT (overrides the lightweight version) ──── */
    window.__ANNAP_RUNTIME_REPORT = function () {
        var r = {};

        /* Overlay state */
        try {
            var dm = window.DrinkDetailModal;
            r.overlay = {
                isOpen:        dm ? dm.isOpen() : false,
                opensTracked:  _overlayOpens,
                closesTracked: _overlayCloses,
                imbalance:     _overlayOpens - _overlayCloses
            };
        } catch (_ov) { r.overlay = { error: String(_ov) }; }

        /* Scroll-lock sanity */
        r.scrollLock = {
            guestScrollLock: document.body.classList.contains('guest-scroll-lock'),
            orderTrayLocked: document.body.classList.contains('order-tray-locked'),
            drinkDetailOpen: document.body.classList.contains('drink-detail-open'),
            bodyTop:         document.body.style.top || ''
        };

        /* Mount-node state */
        var mount = document.getElementById('drink-detail-mount');
        r.mountNode = {
            exists:        !!mount,
            contentLength: mount ? mount.innerHTML.length : 0,
            childCount:    mount ? mount.childNodes.length : 0
        };

        /* Listener counts */
        r.listenerCounts = _listenerCounts;

        /* Compositor-pressure audit */
        try {
            var compositorNodes = [];
            document.querySelectorAll('*').forEach(function (el) {
                var s = window.getComputedStyle(el);
                var reasons = [];
                if (s.willChange && s.willChange !== 'auto')                         reasons.push('will-change:' + s.willChange);
                if (s.backdropFilter && s.backdropFilter !== 'none')                 reasons.push('backdrop-filter');
                if (s.webkitBackdropFilter && s.webkitBackdropFilter !== 'none')     reasons.push('-webkit-backdrop-filter');
                if (s.filter && s.filter !== 'none' && s.filter !== '')              reasons.push('filter');
                if (s.transform && s.transform !== 'none' && s.transform !== 'matrix(1, 0, 0, 1, 0, 0)') reasons.push('transform');
                if (s.opacity !== '1' && s.opacity !== '')                           reasons.push('opacity:' + s.opacity);
                if (reasons.length >= 2) {
                    compositorNodes.push({
                        tag: el.tagName, id: el.id || '-',
                        cls: (el.className || '').toString().slice(0, 48),
                        reasons: reasons.join(' | ')
                    });
                }
            });
            r.compositorNodes = { count: compositorNodes.length, nodes: compositorNodes.slice(0, 40) };
        } catch (_cn) { r.compositorNodes = { error: String(_cn) }; }

        /* Heap (Chrome / Android WebView only) */
        try {
            if (window.performance && window.performance.memory) {
                var m = window.performance.memory;
                r.heap = {
                    usedMB:  Math.round(m.usedJSHeapSize  / 1024 / 1024),
                    totalMB: Math.round(m.totalJSHeapSize / 1024 / 1024),
                    limitMB: Math.round(m.jsHeapSizeLimit / 1024 / 1024)
                };
            }
        } catch (_hm) {}

        /* Active animations */
        try {
            var anims = document.getAnimations ? document.getAnimations() : [];
            r.animations = {
                count:   anims.length,
                running: anims.filter(function (a) { return a.playState === 'running'; }).length,
                names:   anims.slice(0, 20).map(function (a) {
                    return (a.animationName || 'css') + ':' + a.playState;
                })
            };
        } catch (_an) { r.animations = { error: String(_an) }; }

        /* Image count — total in DOM, decoded (complete), still loading */
        try {
            var allImgs = Array.prototype.slice.call(document.images);
            r.images = {
                total:   allImgs.length,
                decoded: allImgs.filter(function (i) { return i.complete && i.naturalWidth > 0; }).length,
                loading: allImgs.filter(function (i) { return !i.complete; }).length,
                error:   allImgs.filter(function (i) { return i.complete && i.naturalWidth === 0 && i.src; }).length
            };
        } catch (_im) { r.images = { error: String(_im) }; }

        r.bodyClasses = document.body.className;
        r.logLength   = _log.length;
        r.pageId      = _pageId;
        r.capturedAt  = new Date().toISOString();

        _emit('RUNTIME_REPORT', {
            compositorCount: r.compositorNodes && r.compositorNodes.count,
            heap: r.heap,
            overlay: r.overlay
        });

        try {
            console.group('%c[ANNAP RUNTIME REPORT]', 'color:#8C2318;font-weight:bold');
            console.log('overlay:',         r.overlay);
            console.log('scrollLock:',      r.scrollLock);
            console.log('mountNode:',       r.mountNode);
            console.log('compositorNodes:', r.compositorNodes && r.compositorNodes.count, '— see .nodes for detail');
            console.log('animations:',      r.animations);
            console.log('images:',          r.images);
            if (r.heap) console.log('heap:', r.heap);
            console.log('listenerCounts:',  r.listenerCounts);
            console.log('full report:',     r);
            console.groupEnd();
        } catch (_c) {}
        return r;
    };

    /* ─────────────────────────────────────────────────────────────────────── */
    /* 10. COMPOSITOR PRESSURE — layer heuristic, long tasks, reflow probe     */
    /* ─────────────────────────────────────────────────────────────────────── */

    /* 10a. Long-task observer: flags any task ≥16ms that blocks the main thread */
    try {
        if (typeof PerformanceObserver !== 'undefined') {
            var _ltTypes = (PerformanceObserver.supportedEntryTypes || []);
            if (_ltTypes.indexOf('longtask') !== -1) {
                new PerformanceObserver(function (list) {
                    list.getEntries().forEach(function (entry) {
                        _emit('LONG_TASK ⚠️', {
                            duration: Math.round(entry.duration) + 'ms',
                            start:    Math.round(entry.startTime) + 'ms'
                        });
                    });
                }).observe({ entryTypes: ['longtask'] });
                _emit('LONGTASK_OBSERVER_ON', {});
            }
        }
    } catch (_ltErr) {}

    /* 10b. Compositor layer heuristic — DevTools: window.__annapAuditLayers()
       Counts elements with properties that force GPU compositing.
       Anything above ~8 during an overlay open/close cycle is suspicious. */
    window.__annapAuditLayers = function () {
        var layers = [];
        try {
            document.querySelectorAll('*').forEach(function (el) {
                var s = window.getComputedStyle(el);
                var r = [];
                if (s.opacity !== '1' && s.opacity !== '') r.push('opacity:' + s.opacity);
                if (s.transform && s.transform !== 'none') r.push('transform');
                if (s.filter && s.filter !== 'none') r.push('filter:' + s.filter.slice(0, 28));
                if (s.backdropFilter && s.backdropFilter !== 'none') r.push('backdrop-filter');
                if (s.webkitBackdropFilter && s.webkitBackdropFilter !== 'none') r.push('-webkit-backdrop-filter');
                if (s.willChange && s.willChange !== 'auto') r.push('will-change:' + s.willChange);
                if (s.isolation === 'isolate') r.push('isolation');
                if (r.length) layers.push({ tag: el.tagName, id: el.id || '-', cls: (el.className || '').toString().slice(0, 40), reasons: r.join(' | ') });
            });
        } catch (_q) {}
        var result = { count: layers.length, layers: layers };
        _emit('LAYER_AUDIT', { count: layers.length, sample: layers.slice(0, 40) });
        try { console.log('[ANNAP LAYERS] count:', layers.length); console.table(layers.slice(0, 40)); } catch (_) {}
        return result;
    };

    /* 10c. Reflow probe — detects layout reads immediately after innerHTML writes.
       The intentional void-offsetWidth in openDrinkDetail is expected; anything
       else in a tight loop indicates thrashing. Capped at 20 events to avoid noise. */
    (function () {
        var _lastWrite = 0, _reflows = 0;
        try {
            var _ihd = Object.getOwnPropertyDescriptor(Element.prototype, 'innerHTML');
            if (_ihd && _ihd.set) {
                var _ohSet = _ihd.set;
                Object.defineProperty(Element.prototype, 'innerHTML', {
                    configurable: true, get: _ihd.get,
                    set: function (v) { _lastWrite = performance.now(); return _ohSet.call(this, v); }
                });
            }
        } catch (_) {}
        try {
            var _owd = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'offsetWidth');
            if (_owd && _owd.get) {
                var _ohOW = _owd.get;
                Object.defineProperty(HTMLElement.prototype, 'offsetWidth', {
                    configurable: true,
                    get: function () {
                        if (_lastWrite && performance.now() - _lastWrite < 10 && _reflows < 20) {
                            _reflows++;
                            _emit('REFLOW_AFTER_WRITE ⚠️', { n: _reflows, ms: Math.round(performance.now() - _lastWrite) });
                        }
                        return _ohOW.call(this);
                    }
                });
            }
        } catch (_) {}
    }());

    _emit('COMPOSITOR_PROBES_READY', { helpers: ['__annapAuditLayers', 'longtask observer', 'reflow probe'] });

    _emit('FORENSICS_READY', {
        patchedApis: ['history.pushState', 'history.replaceState', 'history.back',
                      'history.forward', 'history.go', 'location.assign',
                      'location.replace', 'window.fetch',
                      'EventTarget.prototype.addEventListener']
    });

}());
