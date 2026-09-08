(function (window, document) {
    "use strict";

    function geById(id) {
        return document.getElementById(id);
    }

    function geInit() {

        var root = geById("guest-experience-root");

        // CRITICAL:
        // This script must safely do nothing when the seated guest
        // experience partial is not rendered.
        if (!root) return;

        root.addEventListener("click", function (ev) {

            var target = ev.target;

            if (!target || !target.closest) return;

            // --------------------------------------------------
            // CORE SEATED ADD-TO-CART CONTRACT
            // --------------------------------------------------

            var addButton = target.closest("[data-ge-add]");

            if (addButton) {

                ev.preventDefault();

                var id = addButton.getAttribute("data-ge-add");

                if (id && typeof window.quickAdd === "function") {

                    var groupLabel =
                        typeof window.__annapActiveGroupGuestLabel === "string"
                            ? window.__annapActiveGroupGuestLabel
                            : "";

                    window.quickAdd(
                        id,
                        groupLabel,
                        addButton
                    );
                }

                return;
            }

            // --------------------------------------------------
            // CORE DRINK DETAIL CONTRACT
            // --------------------------------------------------

            var detailButton =
                target.closest("[data-ge-open-detail]");

            if (detailButton) {

                ev.preventDefault();

                var drinkId =
                    detailButton.getAttribute(
                        "data-ge-open-detail"
                    );

                if (!drinkId) return;

                if (
                    typeof window.openDrinkDetail === "function" &&
                    document.getElementById(
                        "drink-detail-modal"
                    )
                ) {

                    window.openDrinkDetail(drinkId);

                } else if (
                    window.GuestInteractionContract &&
                    window.GuestInteractionContract.navigation
                ) {

                    window.location.href =
                        window.GuestInteractionContract
                            .navigation
                            .drinkDetailHref(drinkId);

                } else {

                    window.location.href =
                        "/menu/drink/" +
                        encodeURIComponent(drinkId);
                }

                return;
            }

        });
    }

    if (document.readyState === "loading") {

        document.addEventListener(
            "DOMContentLoaded",
            geInit
        );

    } else {

        geInit();
    }

})(window, document);
