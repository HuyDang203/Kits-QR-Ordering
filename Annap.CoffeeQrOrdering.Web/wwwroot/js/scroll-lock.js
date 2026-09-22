/**
 * AnnapScrollLock — shared scroll-lock module with reference counting.
 *
 * Single source of truth for overlay background scroll locking.
 *
 * Architecture:
 *   - document/window is the page scroller
 *   - body is NOT a scroll container
 *   - JavaScript captures/restores scroll position and applies/removes
 *     temporary body inline positioning required for touch-locking
 *   - CSS only provides the minimum necessary overlay scroll-lock styling
 *
 * Reference counting:
 *   - lock() increments the counter; first lock applies body scroll-lock
 *   - unlock() decrements; last unlock removes body scroll-lock
 *   - Counter is bounded: unlock() never makes the counter negative
 *
 * Cleanup guarantees:
 *   - pagehide: force-unlocks all locks, resets state
 *   - pageshow (bfcache restore): force-unlocks all locks, resets state
 *   - resize from coarse→fine or fine→coarse: state remains consistent
 *
 * Exposed as: window.AnnapScrollLock
 */
(function (global) {
    "use strict";

    var BODY_LOCK_CLASS = "annap-scroll-lock";

    var lockCount = 0;
    var savedScrollY = 0;
    var savedBodyStyleTop = "";
    var savedBodyStylePosition = "";
    var savedBodyStyleLeft = "";
    var savedBodyStyleRight = "";
    var savedBodyStyleWidth = "";

    /**
     * Apply body scroll-lock styles.
     * Sets the body class and inline positioning. The class itself is
     * deliberately minimal — CSS only adds `overflow: hidden` to prevent
     * body from being a scroll container; this JS handles the position
     * and scroll preservation.
     */
    function applyBodyLock() {
        var body = document.body;
        if (!body) return;
        // Save existing inline styles so we can restore the exact values on unlock.
        savedBodyStyleTop = body.style.top || "";
        savedBodyStylePosition = body.style.position || "";
        savedBodyStyleLeft = body.style.left || "";
        savedBodyStyleRight = body.style.right || "";
        savedBodyStyleWidth = body.style.width || "";

        body.classList.add(BODY_LOCK_CLASS);
        // Pin the body in place at the saved scroll offset so the visible
        // content does not jump. position:fixed + top:-scrollY is the
        // standard iOS-friendly scroll-lock technique that also works
        // uniformly on desktop (mouse wheel and scrollbar are simply
        // blocked because body cannot move).
        body.style.position = "fixed";
        body.style.top = "-" + savedScrollY + "px";
        body.style.left = "0";
        body.style.right = "0";
        body.style.width = "100%";
    }

    /**
     * Remove body scroll-lock styles and restore scroll position.
     * Restores exactly the inline styles that were on body before lock().
     */
    function releaseBodyLock() {
        var body = document.body;
        if (!body) return;
        body.classList.remove(BODY_LOCK_CLASS);
        body.style.position = savedBodyStylePosition;
        body.style.top = savedBodyStyleTop;
        body.style.left = savedBodyStyleLeft;
        body.style.right = savedBodyStyleRight;
        body.style.width = savedBodyStyleWidth;
        var y = savedScrollY;
        // Defer scroll restoration so it fires after the body returns to
        // its normal flow and the document height re-expands.
        global.requestAnimationFrame(function () {
            global.scrollTo(0, y);
        });
    }

    /**
     * Capture current scroll position.
     * Call this BEFORE applying any position-fixed state.
     */
    function captureScroll() {
        savedScrollY = (global.scrollY != null ? global.scrollY : 0)
            || (document.documentElement.scrollTop || 0);
    }

    /**
     * Force-reset all lock state.
     * Called on pagehide / pageshow (bfcache restore) / navigation.
     * Always restores body inline styles regardless of lock count.
     */
    function reset() {
        lockCount = 0;
        savedScrollY = 0;
        var body = document.body;
        if (body) {
            body.classList.remove(BODY_LOCK_CLASS);
            body.style.position = savedBodyStylePosition || "";
            body.style.top = savedBodyStyleTop || "";
            body.style.left = savedBodyStyleLeft || "";
            body.style.right = savedBodyStyleRight || "";
            body.style.width = savedBodyStyleWidth || "";
        }
        savedBodyStyleTop = "";
        savedBodyStylePosition = "";
        savedBodyStyleLeft = "";
        savedBodyStyleRight = "";
        savedBodyStyleWidth = "";
    }

    /**
     * Lock the background page scroll.
     * Multiple overlays can call lock() independently; background stays locked
     * until the last unlock().
     *
     * @returns {number} Current lock count after this call.
     */
    function lock() {
        if (lockCount === 0) {
            captureScroll();
            applyBodyLock();
        }
        lockCount++;
        return lockCount;
    }

    /**
     * Unlock the background page scroll.
     * Decrements the reference counter; restores scroll only when the
     * final unlock() is reached.
     *
     * Counter is bounded: never goes below 0.
     *
     * @returns {number} Current lock count after this call.
     */
    function unlock() {
        if (lockCount <= 0) {
            return 0;
        }
        lockCount--;
        if (lockCount === 0) {
            releaseBodyLock();
        }
        return lockCount;
    }

    /**
     * @returns {boolean} True if any overlay currently holds a scroll lock.
     */
    function isLocked() {
        return lockCount > 0;
    }

    /**
     * @returns {number} Current lock reference count (for diagnostics).
     */
    function getCount() {
        return lockCount;
    }

    // ── Navigation / bfcache cleanup ──────────────────────────────────────────

    /**
     * pagehide fires when the document is being unloaded or when the user
     * navigates away. persisted=true means bfcache.
     * Force-reset so the page is never permanently locked after restore.
     */
    global.addEventListener("pagehide", function () {
        reset();
    });

    /**
     * pageshow fires after bfcache restore. persisted=true means bfcache restore.
     * Force-reset in case pagehide did not fire or state leaked.
     */
    global.addEventListener("pageshow", function (e) {
        if (e.persisted) {
            reset();
        }
    });

    /**
     * popstate can fire without a full pagehide/pageshow cycle (e.g., back
     * button with SPA routing). Ensure we clean up any stale locks.
     */
    global.addEventListener("popstate", function () {
        reset();
    });

    // ── Public API ────────────────────────────────────────────────────────────

    global.AnnapScrollLock = {
        lock: lock,
        unlock: unlock,
        isLocked: isLocked,
        getCount: getCount,
        reset: reset
    };

})(typeof window !== "undefined" ? window : globalThis);