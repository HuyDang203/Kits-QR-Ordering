# ANNAP Phase 0 — UI authority foundation

Canonical references: `docs/ANNAP_EXPERIENCE_BLUEPRINT.md`, `docs/ANNAP_IMPLEMENTATION_DOCTRINE.md`, `docs/correspond.md`.

This phase adds a **visual operating system** (tokens, motion vocabulary, typography contracts) without redesigning screens. Production behavior must remain unchanged unless you explicitly opt into optional classes documented below.

---

## 1. Audit — current frontend (Annap.CoffeeQrOrdering.Web)

| Area | Finding |
|------|---------|
| **Styling system** | Tailwind 3 + `Styles/input.css` importing `wwwroot/css/app.css` (ANNAP dark tokens `--annap-*`, legacy `--bg` / `--fg` RGB triplets). Large `input.css` with component-level utilities (mood, postal, guest experience). |
| **Tailwind** | `tailwind.config.js` — `extend` only for fonts, `annap.*` colors, spacing, z-index, easing. |
| **Global CSS** | `app.css`, `guest-experience.css`, `arrival-scene.css`, `atmosphere-modes.css`, `site.css` (built). No CSS Modules. |
| **Animation** | CSS transitions/keyframes in `input.css` and guest CSS; `ambient.js` rAF drift; `arrival/choreography.js` timed classes. **No** Framer Motion in the Razor app. |
| **Color fragmentation** | Parallel systems: legacy `rgb(var(--fg))`, ANNAP `annap-*`, atmosphere thermal vars, guest inline styles in lobby prototype. |
| **Spacing / type fragmentation** | `--annap-space-*` vs ad-hoc Tailwind `px-*` / `rem` across Pages and guest CSS. Mix of DM Serif / Crimson / Outfit vs correspond targets (Playfair / EB Garamond). |
| **Buttons / cards** | `btn-ink`, `surface-glass`, `surface-paper`, mood pills, postal patterns — many valid, not yet mapped to correspondence tokens. |

**annap-coffee-lobby** (Vite + React): demo lobby with extensive **inline styles** and hard-coded hex — highest drift risk if treated as reference implementation.

---

## 2. Created files (authoritative paths)

| Path | Role |
|------|------|
| `Annap.CoffeeQrOrdering.Web/src/styles/tokens.css` | Paper / ink / wax / surfaces / rules / typography scale / spacing / motion durations / easings / radii / shadows / z-index / `[data-mode]` hooks. |
| `Annap.CoffeeQrOrdering.Web/src/styles/motion.css` | Motion duration + easing aliases (doctrine + correspond). |
| `Annap.CoffeeQrOrdering.Web/src/styles/globals.css` | Text smoothing, selection, overscroll; optional `html.annap-correspond-ui` paper shell; **Razor registers** `.annap-reg-*`. |
| `Annap.CoffeeQrOrdering.Web/src/motion/variants.js` | Plain variant objects for future Framer Motion (`paperReveal`, `writingReveal`, `pageScene`, `trayRise`, `letterChild`, `letterEntryStagger`). |
| `Annap.CoffeeQrOrdering.Web/src/components/typography/*.jsx` | `DisplayText`, `NarrativeText`, `FunctionalLabel`, `GhostText`, `index.jsx` — **CSS-variable-only** styles (no new npm deps). |

**Razor:** use `.annap-reg-display`, `.annap-reg-narrative`, `.annap-reg-functional`, `.annap-reg-ghost` from `globals.css` until a surface is built in React.

**Wire-up:** `Styles/input.css` imports the three CSS files **after** `app.css` and **before** `@tailwind base`.

**Fonts:** `_Layout.cshtml` loads **Playfair Display** (italic 300) and **EB Garamond** alongside existing DM Serif / Crimson / Outfit.

**Tailwind:** `tailwind.config.js` `extend` adds `paper.*`, `ink.*`, `wax.*`, correspondence font families, `annap-*` spacing aliases for editorial scale, `fontSize` tokens, `transitionDuration` / `transitionTimingFunction`, `boxShadow`, `borderRadius`, `zIndex` `sheet-*` keys.

---

## 3. Token system (summary)

- **Paper / ink / wax / surfaces / rules** — hex and rgba per `correspond.md`; mode overlays on `[data-mode="solo"|"group"|"adventurous"]`.
- **Motion** — `--duration-*`, `--ease-*`, plus `motion.css` doctrine labels (`--motion-micro` … `--motion-ceremonial`).
- **Bridge** — `--annap-ease-enter` maps to `--ease-unfold` so existing ANNAP layers and new correspondence curves stay aligned.
- **Z-index** — new stack uses `--z-*` (Tailwind: `z-sheet-*`). Legacy `--annap-z-*` unchanged for boot overlays, ambient, etc.

---

## 4. Guardrails (non-negotiables for future work)

1. **No random colors** — use `paper.*` / `ink.*` / `wax.*` / `annap.*` Tailwind theme keys or raw `var(--…)` from `tokens.css`.
2. **No inline animation timing** — use `duration-*` / `ease-*` tokens (`duration-seal`, `ease-unfold`, etc.).
3. **No arbitrary spacing** — prefer `annap-line`, `annap-section`, `annap-inset`, or existing `annap-1` … `annap-24` scale.
4. **No ecommerce UI patterns** — no dense promo grids, shouty all-caps CTAs, star ratings, or “cart” language without hospitality copy review (see blueprint §9).
5. **Typography** — new React surfaces use the four primitives under `src/components/typography/`; Razor uses `.annap-reg-*` until migrated.

---

## 5. Migration path

1. **Opt-in light shell** — add `annap-correspond-ui` to `html` when a page is ready to use paper/ink as default; verify contrast on all states.
2. **Strangle `input.css`** — move page-specific rules behind domain partials; replace one screen at a time with token-backed utilities.
3. **Introduce Framer Motion** (optional) — `npm i framer-motion`, import `src/motion/variants.js` into scene components only.
4. **Lobby** — configure Vite `alias` to `@annap/styles` / `@annap/ui` / `@annap/motion` (see `annap-coffee-lobby/vite.config.js`) and delete inline hex over time.

---

## 6. Risky legacy UI areas

- **`Pages/Index.cshtml`** — very large inline script + mixed Tailwind; high coupling.
- **`Styles/input.css`** — thousands of lines; easy to regress specificity vs new `@layer base` rules.
- **Guest experience CSS/JS** — arrival, discovery, group flows with their own motion vocabulary.
- **annap-coffee-lobby `App.jsx`** — inline styles bypass the token system entirely until refactored.

---

## 7. Optional: Vite consumer (`annap-coffee-lobby`)

`vite.config.js` resolves:

- `@annap/styles` → `Annap.CoffeeQrOrdering.Web/src/styles`
- `@annap/ui` → `Annap.CoffeeQrOrdering.Web/src/components`
- `@annap/motion` → `Annap.CoffeeQrOrdering.Web/src/motion`

Import in `main.jsx`:

```js
import "@annap/styles/tokens.css";
import "@annap/styles/motion.css";
import "@annap/styles/globals.css";
```

This keeps **one token file** for the monorepo while the production site continues to use the Tailwind build pipeline.
