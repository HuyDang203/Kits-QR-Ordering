/**
 * Guest feedback — event-driven only. Listens to GuestInteractionContract emissions.
 * No cart or navigation logic here.
 */
(function () {
    "use strict";

    var TOAST_MS = 1800;

    function tToastLong() {
        try {
            if (window.LuxuryI18n && window.LuxuryI18n.t) {
                return window.LuxuryI18n.t("toast.addedLong") || "";
            }
        } catch (e) {
            /* ignore */
        }
        return "";
    }

    function showToast(text) {
        var toast = document.getElementById("guestToast");
        var toastText = document.getElementById("guestToastText");
        if (!toast || !toastText) return;
        toastText.textContent = text;
        toast.classList.remove("hidden");
        clearTimeout(window.__guestToastTimer);
        window.__guestToastTimer = setTimeout(function () {
            toast.classList.add("hidden");
        }, TOAST_MS);
    }

    function showAddedToast() {
        if (window.AddToOrderAnimationProvider && window.AddToOrderAnimationProvider.handlesAmbientConfirmation) return;
        function go() {
            showToast(tToastLong());
        }
        if (window.LuxuryI18n && LuxuryI18n.ready) LuxuryI18n.ready.then(go).catch(go);
        else go();
    }

    function pulseAddButton(btn) {
        if (!btn || !btn.classList) return;
        btn.classList.remove("is-added");
        void btn.offsetWidth;
        btn.classList.add("is-added");
    }

    function onGuestInteraction(e) {
        var d = (e && e.detail) || {};
        switch (d.type) {
            case "itemAdded":
                showAddedToast();
                if (d.sourceElement) pulseAddButton(d.sourceElement);
                break;
            case "itemRemoved":
            case "cartUpdated":
                break;
            default:
                break;
        }
    }

    function wireNavPatches() {
        var C = window.GuestInteractionContract;
        if (C && C.navigation) C.navigation.applyVtNavPatches();
    }

    document.addEventListener("annap:guest-interaction", onGuestInteraction);

    document.addEventListener("DOMContentLoaded", function () {
        wireNavPatches();
        var btn = document.getElementById("guestToastViewTray");
        if (btn) {
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                if (window.GuestInteractionContract) GuestInteractionContract.openTray();
            });
        }
    });
    window.addEventListener("load", wireNavPatches);
    document.addEventListener("luxury:i18n-changed", wireNavPatches);

    /* Deprecated facade: delegate to contract + events where possible */
    window.AnnapGuestFeedback = {
        showToast: showToast,
        showAddedToast: showAddedToast,
        viewTray: function () {
            if (window.GuestInteractionContract) GuestInteractionContract.openTray();
        },
        pulseAddButton: pulseAddButton,
        augmentVtQueryLinks: wireNavPatches,
        augmentDrinkNavAnchors: function (root) {
            if (window.GuestInteractionContract && GuestInteractionContract.navigation) {
                GuestInteractionContract.navigation.patchDrinkAnchors(root);
            }
        }
    };
})();
