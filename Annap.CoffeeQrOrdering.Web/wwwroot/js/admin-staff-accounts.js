(function () {

    function generatePassword(length) {

        var chars = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789!@#$";

        var out = "";

        var n = length || 12;

        if (window.crypto && window.crypto.getRandomValues) {

            var buf = new Uint32Array(n);

            window.crypto.getRandomValues(buf);

            for (var i = 0; i < n; i++) {

                out += chars[buf[i] % chars.length];

            }

            return out;

        }

        for (var j = 0; j < n; j++) {

            out += chars[Math.floor(Math.random() * chars.length)];

        }

        return out;

    }



    function setInputValue(input, value) {

        input.value = value;

        input.dispatchEvent(new Event("input", { bubbles: true }));

    }



    document.querySelectorAll("[data-generate-password]").forEach(function (btn) {

        btn.addEventListener("click", function () {

            var targetId = btn.getAttribute("data-generate-password");

            var input = document.getElementById(targetId);

            if (!input) return;

            setInputValue(input, generatePassword(12));

            input.type = "text";

            var toggle = document.querySelector(

                '[data-password-target="' + targetId + '"]'

            );

            if (toggle) toggle.textContent = "Ẩn";

        });

    });



    document.querySelectorAll(".admin-staff-accounts-password__toggle").forEach(function (btn) {

        btn.addEventListener("click", function () {

            var targetId = btn.getAttribute("data-password-target");

            var input = document.getElementById(targetId);

            if (!input) return;

            var show = input.type === "password";

            input.type = show ? "text" : "password";

            btn.textContent = show ? "Ẩn" : "Hiện";

        });

    });



    var copyBtn = document.getElementById("staff-credential-copy");

    if (copyBtn) {

        copyBtn.addEventListener("click", async function () {

            var text = copyBtn.getAttribute("data-copy") || "";

            if (!text) return;

            try {

                if (navigator.clipboard && navigator.clipboard.writeText) {

                    await navigator.clipboard.writeText(text);

                } else {

                    var ta = document.createElement("textarea");

                    ta.value = text;

                    ta.setAttribute("readonly", "");

                    ta.style.position = "absolute";

                    ta.style.left = "-9999px";

                    document.body.appendChild(ta);

                    ta.select();

                    document.execCommand("copy");

                    document.body.removeChild(ta);

                }

                var prev = copyBtn.textContent;

                copyBtn.textContent = "Đã sao chép";

                window.setTimeout(function () {

                    copyBtn.textContent = prev;

                }, 1800);

            } catch {

                window.alert("Không sao chép được. Hãy chọn và copy thủ công.");

            }

        });

    }



    if (window.location.hash === "#reset-password") {

        var resetSection = document.getElementById("reset-password");

        if (resetSection) {

            resetSection.scrollIntoView({ behavior: "smooth", block: "start" });

            var resetInput = document.getElementById("reset-password-input");

            if (resetInput) resetInput.focus();

        }

    }

})();


