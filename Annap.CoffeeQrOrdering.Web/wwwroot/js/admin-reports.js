(function () {
    "use strict";

    var COPY_FAIL_MESSAGE = "Không thể sao chép tự động. Vui lòng copy thủ công.";
    var COPY_OK_MESSAGE = "Đã sao chép báo cáo.";

    function showFeedback(el, message) {
        if (!el) return;
        el.textContent = message || COPY_OK_MESSAGE;
        el.classList.remove("hidden");
        clearTimeout(el.__copyTimer);
        el.__copyTimer = setTimeout(function () {
            el.classList.add("hidden");
            el.textContent = COPY_OK_MESSAGE;
        }, 2200);
    }

    async function copyText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }

        var ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.left = "-9999px";
        document.body.appendChild(ta);
        ta.select();
        try {
            return document.execCommand("copy");
        } finally {
            document.body.removeChild(ta);
        }
    }

    document.addEventListener("click", function (e) {
        var btn = e.target && e.target.closest ? e.target.closest("#admin-report-copy-btn") : null;
        if (!btn) return;

        e.preventDefault();
        var targetId = btn.getAttribute("data-copy-target") || "admin-report-text";
        var host = document.getElementById(targetId);
        var feedback = document.getElementById("admin-report-copy-feedback");
        if (!host) return;

        copyText(host.value || host.textContent || "")
            .then(function (ok) {
                if (ok) showFeedback(feedback, COPY_OK_MESSAGE);
                else showFeedback(feedback, COPY_FAIL_MESSAGE);
            })
            .catch(function () {
                showFeedback(feedback, COPY_FAIL_MESSAGE);
            });
    });
})();
