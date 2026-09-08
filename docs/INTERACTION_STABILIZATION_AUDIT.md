# ANNAP Interaction Stabilization Audit

Date: 2026-06-05  
Scope: Order tray, homepage interaction, add-to-order motion, overlays.

---

## 1. Ownership Map

### Order tray (canonical stack)

| Concern | Owner | File(s) |
|---------|-------|---------|
| Tray DOM | `_OrderTrayDock.cshtml` | `Pages/Shared/_OrderTrayDock.cshtml` |
| Tray open/close, render, chip summary | `order-tray-dock.js` | `updateTraySummary`, `renderCart`, `setTrayOpenImmediate` |
| Cart state | `guest-interaction-contract.js` | `getCartLines`, `addItem`, `setTrayOpener` |
| Tray backdrop | `#order-tray-backdrop` | z-55, `correspondence.css` + `order-tray-dock.js` |
| Fly-to-tray animation | `add-to-order-animation-provider.js` | `flyToTray`, `#annap-add-order-layer` z-390 |
| Tray visual CSS | `correspondence.css` + `site.css` | Sheet, chip, correspondence cards, tasting flyer |
| Drink detail modal | `drink-detail-modal.js` + `_OrderTrayDock` | z-350 |

### Homepage interaction stack

| Layer | z | pointer-events | Owner |
|-------|---|----------------|-------|
| `#guest-experience-root` | 1 | auto | `guest-experience.js` |
| `#order-tray-backdrop` (closed) | 55 | none + display:none | `order-tray-dock.js` |
| `#order-tray-root` (closed) | 60 | none; chip only auto | `_OrderTrayDock` + `correspondence.css` |
| `#annap-arrival-scene` | 90 | none (visual only) | `arrival/choreography.js` |
| `#drink-detail-modal` | 350 | when open | `drink-detail-modal.js` |

### Lifecycle

1. SSR: landing + panels + arrival scene + dock (closed)
2. `DOMContentLoaded`: `geInit`, `annapStartOrderTrayDock` → `setTrayOpenImmediate(false)`
3. `load`: arrival choreo (optional skip via sessionStorage / `?arrival=0`)
4. User tap: `guest-experience.js` click delegate → `openFlow` / native menu link
5. Add item: GIC `itemAdded` → provider `flyToTray` → `annap:tray-chip-settle` → dock `updateTraySummary`

---

## 2. Dead Systems (removed)

| System | Was | Action |
|--------|-----|--------|
| `tray-ritual.js` / `tray-ritual.css` | Envelope ritual, duplicate open state | **Deleted** |
| `AnnapTrayRitual` hooks | Dead optional path in dock + provider | **Removed** |
| `updateDetailTray()` | Duplicate chip updater in provider | **Removed** — dock owns chip |
| `#annap-tray-line` ink line | Second tray UI when no chip | **Removed** (JS + CSS) |
| `correspondence.js` ceremony | `playAddCeremony`, cup-flyer duplicate | **Removed** — provider owns fly |
| `#cartOverlay` on seated Index | Legacy sheet under dock | **Not rendered** when `HasSeatedTable` |
| `docs/MOTION_OWNERSHIP_MAP.md` | Superseded audit | **Deleted** |

---

## 3. Files Safe to Delete (done or N/A)

- `wwwroot/js/tray-ritual.js` — deleted
- `wwwroot/css/tray-ritual.css` — deleted (styles merged into `correspondence.css`)
- `docs/MOTION_OWNERSHIP_MAP.md` — deleted

**Keep (non-seated only):** `Index.cshtml` `#cartOverlay` block + inline `openCart` — preview-home path without seated table.

**Deferred (not tray-critical):** `menu-browse-physical.css/js` — menu-only proximity; does not affect homepage stack.

---

## 4. Canonical Systems (single source of truth)

1. **Tray UI** — `_OrderTrayDock` + `order-tray-dock.js`
2. **Tray backdrop** — `#order-tray-backdrop` only (no body::before hijack, no ritual backdrop class)
3. **Chip / header summary** — `updateTraySummary()` in `order-tray-dock.js` only
4. **Cart lines render** — `renderCart()` → `#cart` correspondence cards
5. **Add animation** — `add-to-order-animation-provider.js` only (ends with `annap:tray-chip-settle`)
6. **Cart state** — `guest-interaction-contract.js`
7. **Homepage flows** — `guest-experience.js` click delegate
8. **Arrival** — `arrival/choreography.js` (visual z-90, never captures taps)

---

## 5. Cleanup Plan (executed)

- [x] Delete tray-ritual assets
- [x] Unify chip updates under dock
- [x] Strip correspondence.js to page transitions + tray chrome helpers
- [x] Remove ink-line CSS/JS
- [x] Stop rendering `#cartOverlay` on seated homepage
- [x] Merge orphaned tray-surface CSS into `correspondence.css`
- [ ] Future: remove `annap-mode-focus` dead CSS path (JS never sets `html.annap-mode-focus`)
- [ ] Future: set `annap-seated-guest` on seated Index to hide header chrome server-side

**Motion rebuild gate:** Do not add envelope ritual, second backdrop, or parallel chip updaters until verification passes on device.

---

## 6. Verification (mobile 390×844, `http://localhost:8080/table/T12?arrival=0`)

| Check | Result |
|-------|--------|
| `elementFromPoint` on `#ge-ritual-begin` | Hits button (not tray wrapper) |
| `elementFromPoint` on `#ge-ritual-menu` | Hits anchor |
| `#order-tray-root` height when closed | ~106px (not 515px) |
| `#order-tray-backdrop` when closed | `display: none`, `pointer-events: none` |
| Sommelier CTA click | Opens `#ge-panel-sommelier` |
| Browse menu click | Navigates to `/Menu?vt=...` |
| Tray open | Sheet + backdrop `backdrop-filter: none` |
| Tray close | Backdrop hidden, homepage tappable |

---

## Z-index quick reference

```
0   ambient / body::before
1   guest-main, #guest-experience-root
40  .guest-header
55  #order-tray-backdrop (open only)
60  #order-tray-root
90  #annap-arrival-scene (pe:none)
205 annap-tasting-flyer
350 #drink-detail-modal
390 #annap-add-order-layer
```
