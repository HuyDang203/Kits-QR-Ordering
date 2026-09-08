(function () {
    "use strict";

    function toast(msg) {
        try {
            alert(msg);
        } catch {
            /* ignore */
        }
    }

    async function copyText(text) {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                toast("Link copied.");
                return;
            }
        } catch {
            /* fall through */
        }
        try {
            var ta = document.createElement("textarea");
            ta.value = text;
            ta.style.position = "fixed";
            ta.style.left = "-9999px";
            document.body.appendChild(ta);
            ta.select();
            document.execCommand("copy");
            document.body.removeChild(ta);
            toast("Link copied.");
        } catch {
            toast("Could not copy — select the URL manually.");
        }
    }

    function setSoloPrint(code, on) {
        document.querySelectorAll(".demo-qr-card").forEach(function (el) {
            var c = el.getAttribute("data-table");
            if (on) {
                if (c !== code) el.classList.add("demo-qr-card--hidden-print");
                else el.classList.remove("demo-qr-card--hidden-print");
            } else el.classList.remove("demo-qr-card--hidden-print");
        });
    }

    document.addEventListener("click", function (e) {
        var t = e.target;
        if (!t || !t.closest) return;
        var copyBtn = t.closest("[data-copy]");
        if (copyBtn) {
            e.preventDefault();
            void copyText(copyBtn.getAttribute("data-copy") || "");
            return;
        }
        var printCard = t.closest("[data-print-card]");
        if (printCard) {
            e.preventDefault();
            var code = printCard.getAttribute("data-print-card");
            setSoloPrint(code, true);
            window.print();
            window.addEventListener(
                "afterprint",
                function onAfter() {
                    window.removeEventListener("afterprint", onAfter);
                    setSoloPrint(code, false);
                },
                { once: true }
            );
            return;
        }
        if (t.id === "demo-qr-print-all" || t.closest("#demo-qr-print-all")) {
            e.preventDefault();
            setSoloPrint("", false);
            window.print();
        }
    });
})();
