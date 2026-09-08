/**
 * Live miniature homepage composition preview — updates as lamps toggle.
 */
(function (global) {
    "use strict";

    var modes = [
        { id: "group", short: "Đi nhóm" },
        { id: "sommelier", short: "Một mình" },
        { id: "discovery", short: "Bất ngờ" }
    ];

    function layoutLabel(count) {
        if (count === 0) return "Tạm dừng sắp đặt";
        if (count === 1) return "Một lối · trung tâm";
        if (count === 2) return "Hai lối · đối xứng";
        return "Ba lối · tam giác biên tập";
    }

    function kickerFor(count) {
        return count === 1 ? "Hành trình gợi ý hôm nay" : "106/1 · sảnh";
    }

    function readVisible(form) {
        return modes.filter(function (m) {
            var inp = form.querySelector('[data-homepage-flow="' + m.id + '"]');
            return inp && inp.checked;
        });
    }

    function paintMini(panel, visible) {
        var preview = panel.querySelector("[data-exp-hp-mini-preview]");
        var stack = panel.querySelector("[data-hp-preview-stack]");
        var empty = panel.querySelector("[data-hp-preview-empty]");
        var kickerEl = panel.querySelector("[data-hp-preview-kicker]");
        var countEl = panel.querySelector("[data-hp-status-count]");
        var layoutEl = panel.querySelector("[data-hp-status-layout]");
        var warn = panel.querySelector("[data-hp-zero-warning]");
        var count = visible.length;
        var reduce =
            global.matchMedia && global.matchMedia("(prefers-reduced-motion: reduce)").matches;

        if (preview) {
            preview.classList.remove(
                "exp-hp-mini-preview--0",
                "exp-hp-mini-preview--1",
                "exp-hp-mini-preview--2",
                "exp-hp-mini-preview--3"
            );
            preview.classList.add("exp-hp-mini-preview--" + count);
            preview.setAttribute("data-hp-count", String(count));
            if (!reduce) preview.classList.add("exp-hp-mini-preview--pulse");
            global.setTimeout(function () {
                preview.classList.remove("exp-hp-mini-preview--pulse");
            }, 480);
        }

        if (countEl) countEl.textContent = String(count);
        if (layoutEl) layoutEl.textContent = layoutLabel(count);
        if (kickerEl) kickerEl.textContent = kickerFor(count);

        if (stack) {
            stack.classList.remove(
                "exp-hp-mini-preview__stack--0",
                "exp-hp-mini-preview__stack--1",
                "exp-hp-mini-preview__stack--2",
                "exp-hp-mini-preview__stack--3"
            );
            if (count >= 0 && count <= 3) stack.classList.add("exp-hp-mini-preview__stack--" + count);
            stack.innerHTML = visible
                .map(function (m, i) {
                    var ix = i + 1;
                    var label = ix < 10 ? "0" + ix : String(ix);
                    return (
                        '<div class="exp-hp-mini-preview__card" data-hp-card="' +
                        m.id +
                        '"><span>' +
                        label +
                        "</span><strong>" +
                        m.short +
                        "</strong></div>"
                    );
                })
                .join("");
        }

        if (empty) {
            if (count === 0) {
                empty.classList.remove("hidden");
                empty.removeAttribute("hidden");
            } else {
                empty.classList.add("hidden");
                empty.setAttribute("hidden", "hidden");
            }
        }

        if (warn) {
            if (count === 0) {
                warn.classList.remove("hidden");
                warn.removeAttribute("hidden");
            } else {
                warn.classList.add("hidden");
                warn.setAttribute("hidden", "hidden");
            }
        }
    }

    function bindPanel(panel) {
        var form = panel.querySelector("[data-homepage-preview-form]");
        if (!form) return;

        function tick() {
            paintMini(panel, readVisible(form));
            try {
                global.dispatchEvent(
                    new CustomEvent("annap:homepage-composition-changed", {
                        detail: { visible: readVisible(form).map(function (m) {
                            return m.id;
                        }) }
                    })
                );
            } catch (_ev) {
                /* ignore */
            }
        }

        form.addEventListener("change", tick);
        form.addEventListener("input", tick);
        tick();
    }

    function init() {
        [].slice.call(document.querySelectorAll("[data-homepage-composition]")).forEach(bindPanel);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})(window);
