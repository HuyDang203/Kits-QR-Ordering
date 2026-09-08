(function () {
    const confirmMessage =
        "Bạn chắc chắn muốn kết ca? Các bill trong ca hiện tại sẽ được chốt vào báo cáo ca.";

    function wireCloseConfirm(form) {
        if (!form) return;
        form.addEventListener("submit", function (e) {
            const btn = form.querySelector('button[type="submit"]');
            if (btn && btn.disabled) {
                e.preventDefault();
                return;
            }
            if (!window.confirm(confirmMessage)) e.preventDefault();
        });
    }

    const form = document.getElementById("staff-shift-close-form");
    const stickyForm = document.getElementById("staff-shift-close-sticky-form");
    wireCloseConfirm(form);
    wireCloseConfirm(stickyForm);

    const sticky = document.getElementById("staff-shift-close-sticky");
    const root = document.querySelector(".staff-shift-close");
    if (sticky && root && root.getAttribute("data-can-close") === "true") {
        sticky.removeAttribute("aria-hidden");
    }

    document.querySelectorAll("[data-shift-expand-bills], #staff-shift-expand-bills").forEach(function (btn) {
        btn.addEventListener("click", function () {
            document.querySelectorAll("[data-bill-row].is-collapsed, tr.is-collapsed").forEach(function (row) {
                row.classList.remove("is-collapsed");
            });
            document.querySelectorAll(".staff-shift-close__expand-bills").forEach(function (b) {
                b.classList.add("is-expanded");
            });
        });
    });

    const copyBtn = document.getElementById("staff-shift-copy");
    if (copyBtn) {
        copyBtn.addEventListener("click", async function () {
            const text = copyBtn.getAttribute("data-copy") || "";
            if (!text) return;
            try {
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    await navigator.clipboard.writeText(text);
                } else {
                    const ta = document.createElement("textarea");
                    ta.value = text;
                    ta.setAttribute("readonly", "");
                    ta.style.position = "absolute";
                    ta.style.left = "-9999px";
                    document.body.appendChild(ta);
                    ta.select();
                    document.execCommand("copy");
                    document.body.removeChild(ta);
                }
                const prev = copyBtn.textContent;
                copyBtn.textContent = "Đã sao chép";
                window.setTimeout(function () {
                    copyBtn.textContent = prev;
                }, 1800);
            } catch {
                window.alert("Không sao chép được. Hãy chọn và copy thủ công.");
            }
        });
    }
})();
