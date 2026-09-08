# ANNAP QR ordering — production launch pack

Operational focus: **SCAN → MENU → DETAIL → CART → SUBMIT → STAFF**. Luxury flows are optional.

---

## 1. Current mobile / interaction audit (code-level)

### Boot & dependency chain (seated home)

| Step | Files | Risk if fails |
|------|--------|----------------|
| Layout loads `guest-boot-harness.js`, i18n, `guest-interaction-contract.js` | `_Layout.cshtml` | No `GuestInteractionContract` → menu submit/detail guardrails fail |
| Index inline `annapStartHomeGuestBoot` → `bootGuestExperience` | `Pages/Index.cshtml` | Tray patches / cart pills run before or without experience panels |
| `guest-experience.js` `geInit` on `DOMContentLoaded` | `wwwroot/js/guest-experience.js` | Panels inert until DOM ready; landing remains usable (server HTML) |
| `order-tray-dock.js` + `annapStartOrderTrayDock()` | `Index.cshtml` § Scripts | Tray not bound → no submit from pages that include dock |

**Race / ordering:** `bootGuestExperience` runs from i18n gate; `geInit` is separate on `DOMContentLoaded`. Landing choices do not block the **server-rendered** “Go directly to menu” block when `GuestOperational:MenuFirstArrival` is true.

### Menu interaction pipeline

| Concern | Files | Symptom if wrong |
|---------|--------|------------------|
| Delegated `click` + touch `pointerup` | `order-tray-dock.js` `tryActivateMenuBrowseFromTarget`, `annapOnMenuBrowsePointerUp` | Taps do nothing while scroll works (iOS / WebView) |
| `openDrinkDetail` async + scroll lock | `order-tray-dock.js` | Body stuck `position:fixed` → no scroll, taps feel “dead” |
| Tray backdrop hit area | `_OrderTrayDock.cshtml`, `input.css` `.order-tray-backdrop-layer` | Ghost layer intercepts taps above menu |
| `refreshTableIdentityUi` | `order-tray-dock.js` | `.menu-add-btn` `disabled` + `pointer-events-none` when no `vt` |

### Remaining mobile risks (exact)

1. **In-app browser storage limits** — `guest-interaction-contract.js` / `guest-order-queue.js` use `localStorage`. **Symptom:** cart vanishes or queue never flushes. **Mitigation:** staff sees empty tray; guest re-adds. Long-term: optional server-side draft cart.
2. **iOS low-memory tab reload** — mid-modal state lost. **Mitigation:** `visibilitychange` / `pagehide` now tear down drink modal (`order-tray-dock.js`) to recover `body` scroll classes.
3. **`GuestInteractionContract` undefined** (e.g. `SlimGuestBoot` + skipped script) — `openDrinkDetail` returns early. **Symptom:** Discover / card open silent fail. **Mitigation:** never ship production with contract script disabled; `?safe=1` is explicit minimal shell.
4. **SignalR/WebSocket blocked** on corporate Wi‑Fi — staff board may lag; guest track may rely on polling depending on page flags (`DiagnosticsOptions`).
5. **Animation jank** — heavy discovery CSS. **Mitigation:** `GuestOperational:CalmArrivalAnimations` + class `ge-root--calm` on `#guest-experience-root` (`guest-experience.css`).

### iPhone / WebView failure modes (short)

- **Stuck scroll lock:** modal open → background tab → return; previously orphan `guest-scroll-lock`. **Fix:** close modal on `hidden` + `pagehide`; clear orphan lock on `visible` when modal not marked open.
- **Missing `async function openDrinkDetail`:** was a **parse-breaking bug** (orphan block after `annapBindDrinkModalChrome`). **Fixed:** restored function declaration; run `node --check wwwroot/js/order-tray-dock.js` in CI later.

---

## 2. Production mode (operational) — configuration

| Setting | Default | Purpose |
|---------|---------|---------|
| `GuestOperational:MenuFirstArrival` | `true` | Primary **server-rendered** “Go directly to menu” on seated arrival (`_GuestExperienceEntry.cshtml`). No JS required to start ordering path. |
| `GuestOperational:CalmArrivalAnimations` | `true` | Adds `ge-root--calm` to reduce motion duration on arrival / discovery shell. |

Optional experiences remain; copy states they are **never required to order**.

---

## 3. Mobile QA checklist (real devices)

### Matrix

| Client | Scan QR | Menu scroll | Card tap → modal | Add | Submit | Staff sees |
|--------|---------|-------------|-------------------|-----|--------|--------------|
| iPhone Safari | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Android Chrome | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Facebook in-app | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| Instagram in-app | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| LINE in-app | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |

### Step-by-step (guest)

1. Scan table QR → land on `/` with table context.
2. Without tapping anything else, hit **“Go directly to menu”** (if shown).
3. Scroll categories; tap **Discover** and card body (not only Add).
4. Add 2 different items; change quantity in tray.
5. **Request preparation**; confirm success or queued message.
6. Open **track** link from success UI if present.

### Stress

- **Rapid taps** on Add (double-order intent).
- **Modal spam:** open/close drink detail 10× quickly.
- **Orientation:** rotate with modal open and closed.
- **Background:** send app to background 30s, resume.
- **Flaky Wi-Fi:** airplane mode on during submit, then off (queue path).

### Failure reproduction

- If taps fail but scroll works: note **URL**, **in-app vs Safari**, **was modal open?**, **any overlay visible?** Screenshots + remote debug WebView if available.

---

## 4. Production deployment checklist

### Config & secrets

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` set for Compose, or `ConnectionStrings__DefaultConnection` set for a managed Postgres URL.
- [ ] `StaffAuth__UserName`, `StaffAuth__Password`, `StaffAuth__CheckoutPassword`, `StaffAuth__BaristaPassword` — every password **≥12 chars**, not weak list (`ProductionStartupGuard`).
- [ ] `AppUrl__PublicBaseUrl` — canonical HTTPS base for printed QR / emails.
- [ ] `Database__ApplyMigrationsOnStartup` — `true` for first deploy; consider `false` after stable with manual migrations.
- [ ] `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret`, `Cloudinary__Folder`.
- [ ] `DataProtection__KeysPath=/var/data/dataprotection-keys` on a persistent disk.

### Infra

- [ ] Reverse proxy (Caddy / nginx / cloud LB) terminates TLS, forwards `X-Forwarded-For` / `X-Forwarded-Proto`.
- [ ] `UseForwardedHeaders` enabled (already outside Development in `Program.cs`).
- [ ] Health: `GET /health` wired in monitor.
- [ ] Logs shipped (file / cloud / journald).

### Docker (`docker-compose.prod.yml`)

- [ ] `POSTGRES_PASSWORD` and all canonical `StaffAuth__*Password` values set in `.env` or deployment secrets.
- [ ] PostgreSQL container is `annap-postgres` with `5432:5432`; web container is `annap-web` with `8080:8080`.
- [ ] Volume backup for Postgres data dir / managed snapshot.

### Rollback

- [ ] Keep previous image tag; `docker compose pull` only after smoke test.
- [ ] DB: forward-only migrations; have downgrade script only if you maintain one.

### Minimum VPS (starting point)

- **2 vCPU, 4 GB RAM**, SSD, for light café traffic + Postgres co-hosted OR separate small DB instance.
- Scale CPU first if sommelier/chat traffic grows.

---

## 5. Security baseline (already in codebase)

- **Staff cookie:** `HttpOnly`, `Secure` in non-Development, `SameSite=Lax` (`Program.cs`).
- **Production startup guard:** weak/default staff password and toy localhost connection rejected in Production (`ProductionStartupGuard.cs`).
- **Rate limits:** `anon-order-post`, `anon-ai-post`, `anon-chat-post` on anonymous POST endpoints (`Program.cs`). Partition uses **`X-Forwarded-For` first client IP** when present.

**Token risk:** guest order read APIs use unguessable token in query; treat shared screenshots of track URL as credential leaks.

---

## 6. Refactoring plan (stability first — do not execute as one PR)

| Phase | Action | Regression risk |
|-------|--------|-----------------|
| 1 | Extract **rate limit policy registration** to `Web/Internal/RateLimitPolicies.cs` | Low |
| 2 | Extract **anonymous API group** to static class + `MapGroup("/api")` | Medium — test all routes |
| 3 | Extract **guest discovery/sommelier** POSTs to separate file partial | Medium |
| 4 | Keep **Razor** in Pages; avoid moving views until API stable | N/A |

**Rule:** no big-bang `Program.cs` split until one production week is clean.

---

## 7. Implementation order (this sprint)

1. **Ship JS parse fix** for `openDrinkDetail` + modal lifecycle hardening — **blocker** for menu modal path.
2. **Operational config** — `MenuFirstArrival` + `CalmArrivalAnimations` documented and set in prod JSON.
3. **Rate limiter partition** — correct client IP behind proxy.
4. **Menu `noscript`** — honest fallback copy.
5. **Run device QA** using section 3.

---

## 8. Files touched in this engineering pass

- `wwwroot/js/order-tray-dock.js` — restored `openDrinkDetail`, extended visibility/pagehide handling.
- `Program.cs` — rate limit partition reads `X-Forwarded-For` first.
- `GuestOperationalOptions.cs`, `appsettings*.json`, `Index.cshtml.cs`, `_GuestExperienceEntry.cshtml` — `CalmArrivalAnimations`.
- `wwwroot/css/guest-experience.css` — `.ge-root--calm` rules.
- `Pages/Menu/Index.cshtml` — `noscript` fallback strip.
