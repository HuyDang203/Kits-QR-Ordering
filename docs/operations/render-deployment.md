# Render deployment guide (ANNAP demo)

Operational guide for deploying ANNAP to [Render](https://render.com) for demonstration. Does not change guest UX or recommendation logic.

## Production architecture

| Resource | Role |
|-----------------|------|
| **Neon PostgreSQL** | Managed Postgres with pgvector; SSL required |
| **Render Starter Web Service** | ASP.NET Core 8 Docker app; binds to Render `PORT` |
| **Cloudinary** | Durable menu image uploads and delivery |
| **Render Persistent Disk** | Data Protection keys at `/var/data` |
| **Cloudflare** | Public HTTPS hostname in front of Render |

The app automatically:

- Reads **`DATABASE_URL`** when `ConnectionStrings__DefaultConnection` is not set
- Detects Render via `RENDER`, `RENDER_SERVICE_ID`, or `RENDER_EXTERNAL_URL`
- Logs bootstrap and public-URL warnings without failing startup

---

## Neon PostgreSQL setup

1. Create a Neon project in the nearest region to the Render service.
2. Enable/confirm the `vector` extension is available (the initial EF migration runs `CREATE EXTENSION IF NOT EXISTS vector`).
3. Copy the pooled PostgreSQL connection URL with `sslmode=require`.
4. Set it as `DATABASE_URL` on the Render web service.

ANNAP converts `DATABASE_URL` to an Npgsql connection string, enforces SSL in Production, and logs:

```
Using DATABASE_URL-derived PostgreSQL connection.
```

If you prefer an explicit string, set `ConnectionStrings__DefaultConnection` instead (see below).

---

## Required environment variables

| Variable | Required | Example / notes |
|----------|----------|-----------------|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `ASPNETCORE_URLS` | Yes | `http://+:${PORT}` |
| `DATABASE_URL` | Yes* | Auto-injected when Postgres is linked |
| `ConnectionStrings__DefaultConnection` | Alt* | Npgsql format; overrides `DATABASE_URL` if set |
| `StaffAuth__Password` | Yes | Admin/shared password; ≥12 characters and not weak |
| `StaffAuth__CheckoutPassword` | Yes | Checkout shared password; ≥12 characters and not weak |
| `StaffAuth__BaristaPassword` | Yes | Barista shared password; ≥12 characters and not weak |
| `StaffAuth__UserName` | No | Default `host` |
| `AppUrl__PublicBaseUrl` | Yes | Cloudflare HTTPS hostname |
| `Database__ApplyMigrationsOnStartup` | First deploy | `true` then `false` after stable |
| `Cloudinary__CloudName` | Yes | Cloudinary cloud name |
| `Cloudinary__ApiKey` | Yes | Cloudinary API key |
| `Cloudinary__ApiSecret` | Yes | Cloudinary API secret |
| `Cloudinary__Folder` | Yes | `annap/menu-items` |
| `DataProtection__KeysPath` | Yes | `/var/data/dataprotection-keys` on a Render persistent disk |
| `DataProtection__ApplicationName` | Yes | `Annap.CoffeeQrOrdering` |

\* Use **either** linked `DATABASE_URL` **or** `ConnectionStrings__DefaultConnection`, not both unless intentional.

### Optional

| Variable | Purpose |
|----------|---------|
| `Sommelier__ApiKey` | Legacy RAG sommelier only; **not required** for specialty demo |
| `KiotViet__IsEnabled` | Default `false` — leave off for demo |
| `GoLive__HealthUrl` | Full URL for `verify-go-live` health probe against live service |
| `BankTransfer__Enabled` | `true` when VietQR bank transfer is configured |
| `BankTransfer__Provider` | `VietQR` |
| `BankTransfer__BankBin` | VietQR bank code (e.g. `970416` for ACB) |
| `BankTransfer__BankName` | Display name (e.g. `ACB`) |
| `BankTransfer__AccountNumber` | Business account number |
| `BankTransfer__AccountName` | Account holder as registered with the bank |
| `BankTransfer__DescriptionTemplate` | Transfer memo template — default `ANNAP {Reference}` |
| `BankTransfer__QrImageUrlTemplate` | VietQR image URL template with `{bankBin}`, `{accountNumber}`, `{amount}`, `{memo}`, `{accountName}` |
| `BankTransfer__Webhook__DevWebhookEnabled` | **`false` in production** — dev/mock webhook only |

### Bank transfer QR and auto-confirmation

- **Guest QR** requires real bank config (`BankTransfer__Enabled=true` plus bank BIN, account number, account name). Without it, the BankTransfer payment tile is disabled.
- **Auto-confirm** requires a trusted provider webhook that sends transfer memo + exact amount. Phase 4A includes a **dev-only** endpoint `POST /api/webhooks/bank-transfer/dev` for local testing — not a production payment provider.
- **Matching rules:** memo must match the order transfer reference (via `BankTransfer:DescriptionTemplate`, default `ANNAP {Reference}`) and amount must equal the locked order total exactly.
- **Manual fallback:** staff can still click **Xác nhận thanh toán** when webhook does not match.

Local dev webhook (Development only):

```bash
curl -X POST http://localhost:8080/api/webhooks/bank-transfer/dev \
  -H "Content-Type: application/json" \
  -d '{"provider":"dev","transactionId":"dev-txn-001","amount":90000,"memo":"ANNAP AC6D9D13A"}'
```

The endpoint is not mapped outside Development. Production startup also rejects
`BankTransfer__Webhook__DevWebhookEnabled=true`.

---

## Build command

From repository root:

```bash
dotnet publish Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj -c Release -o ./publish
```

**Publish directory:** `./publish`

Tailwind CSS (`site.css`) is committed; run `npm run build:css` in `Annap.CoffeeQrOrdering.Web/` only if you changed `Styles/input.css`.

---

## Start command

```bash
dotnet Annap.CoffeeQrOrdering.Web.dll
```

Run from the publish output directory.

---

## Render service settings

| Setting | Value |
|---------|-------|
| **Runtime** | Docker |
| **Root directory** | Repository root (or path containing `.csproj`) |
| **Health check path** | `/health` |
| **Health check initial delay** | `90` seconds (first deploy with migrations) |
| **Instance type** | Starter or higher recommended (avoid free-tier cold starts for live demo) |

---

## First deployment procedure

1. Create the Neon database and Render **Starter Web Service** in nearby regions.
2. Set the Neon pooled connection URL as `DATABASE_URL`.
3. Set environment variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ASPNETCORE_URLS=http://+:${PORT}`
   - `StaffAuth__Password=<strong-password>`
   - `StaffAuth__CheckoutPassword=<strong-password>`
   - `StaffAuth__BaristaPassword=<strong-password>`
   - `AppUrl__PublicBaseUrl=https://<cloudflare-hostname>`
   - `Database__ApplyMigrationsOnStartup=true`
   - `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret`
   - `Cloudinary__Folder=annap/menu-items`
   - `DataProtection__KeysPath=/var/data/dataprotection-keys`
   - `DataProtection__ApplicationName=Annap.CoffeeQrOrdering`
   - `BankTransfer__Webhook__DevWebhookEnabled=false`
4. Attach a Render persistent disk mounted at `/var/data`.
5. Deploy. Watch logs for:
   - `Using DATABASE_URL-derived PostgreSQL connection.`
   - `ANNAP PRODUCTION STARTUP` banner (Render deployment: yes)
   - `EF Core migrations applied successfully.`
   - `Specialty coffee bootstrap: 4 flagship coffees ensured.`
6. Confirm `/health` returns Healthy in Render dashboard.

### Order workflow migrations (staff order board + item preparation)

Before deploying changes that include the payment workflow (`BillNumber`, `PaidAtUtc`, etc.) or barista preparation checklist (`PreparedQuantity`, `PreparedAtUtc`, `PreparedBy` on `order_items`), apply EF migrations **once**:

```bash
dotnet ef database update --project Annap.CoffeeQrOrdering.Infrastructure --startup-project Annap.CoffeeQrOrdering.Web
```

Or set `Database__ApplyMigrationsOnStartup=true` for the first deploy only, then set it back to `false`.

If migrations are missing, startup logs a critical error in Production (fail-fast) and `/health` reports **Unhealthy** for `payment_workflow_schema`. Staff board API returns `503` with `database_migration_required` instead of a raw PostgreSQL error.

7. Open `/staff/orders` after login and confirm the order board loads.
8. Run go-live verification (below).
9. Set `Database__ApplyMigrationsOnStartup=false` for subsequent deploys.
10. Smoke test: table QR → specialty sommelier → cup moment → order submit.

### Startup warnings (non-blocking)

| Log | Meaning |
|-----|---------|
| `AppUrl__PublicBaseUrl is not configured…` | Set public URL for correct QR links |
| `Hospitality catalog bootstrap failed…` | Run `verify-go-live`; check DB and migrations |
| OpenAI WARN in `verify-go-live` | Safe for specialty demo |

---

## verify-go-live procedure

After deploy, run from Render shell or one-off job:

```bash
cd /app   # or your publish directory
dotnet Annap.CoffeeQrOrdering.Web.dll verify-go-live
```

Or locally against production DB:

```bash
cd Annap.CoffeeQrOrdering.Web
ConnectionStrings__DefaultConnection="<render-npgsql-string>" \
  ASPNETCORE_ENVIRONMENT=Production \
  StaffAuth__Password="<password>" \
  dotnet run -- verify-go-live
```

**Success:** exit code `0`, `READY FOR PRODUCTION TRAFFIC`.

**Failure:** exit code `1` — fix listed gates and redeploy or re-run bootstrap.

Set `GoLive__HealthUrl=https://your-service.onrender.com/health` to probe the live health endpoint.

See [go-live-verification.md](./go-live-verification.md) for gate details.

---

## Render deployment checklist

- [ ] PostgreSQL PG 13+ created and linked
- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ASPNETCORE_URLS=http://+:${PORT}`
- [ ] `DATABASE_URL` present (linked) **or** `ConnectionStrings__DefaultConnection` set
- [ ] `StaffAuth__Password` ≥ 12 chars
- [ ] `AppUrl__PublicBaseUrl=https://….onrender.com`
- [ ] `Database__ApplyMigrationsOnStartup=true` (first deploy only)
- [ ] Health check `/health`, initial delay 90s
- [ ] Deploy logs show DATABASE_URL conversion + production startup banner
- [ ] `/health` Healthy
- [ ] `verify-go-live` exit 0
- [ ] Specialty flow smoke test on phone
- [ ] `Database__ApplyMigrationsOnStartup=false` after stable

---

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Startup fails on password | `ProductionStartupGuard` rejects weak/default passwords |
| Migration fails on `vector` | Postgres version ≥ 13; run `CREATE EXTENSION vector;` in SQL shell |
| Health 503 during deploy | Migrations still running; increase health check delay |
| Wrong QR URLs | Set `AppUrl__PublicBaseUrl` |
| Specialty gates FAIL | Logs for `Hospitality catalog bootstrap failed`; re-run with migrations enabled |
