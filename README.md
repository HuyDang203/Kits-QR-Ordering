# ANNAP — Coffee QR Ordering

Production hospitality platform for ANNAP: mobile-first guest ordering, staff admin curation, PostgreSQL + pgvector.

## Architecture

```
Annap.CoffeeQrOrdering.Domain/        Entities & domain rules
Annap.CoffeeQrOrdering.Application/   Abstractions & interfaces
Annap.CoffeeQrOrdering.Infrastructure/  EF Core, PostgreSQL, migrations
Annap.CoffeeQrOrdering.Web/           Razor Pages UI, guest runtime, admin
```

Guest drink detail: JSON overlay (`DrinkDetailModal` → `?handler=Data` → `DrinkDetailRenderer`).  
Menu images: WebP pipeline (`MenuImagePipeline`) — card, poster, and thumb variants under `wwwroot/media/menu-items/`.

## Local development

**1. Start PostgreSQL (pgvector):**

```bash
docker compose up -d
```

This starts `annap-postgres` only. PostgreSQL is available to the host at `localhost:5432`, with the default local database `annap_qr_ordering`.

**2. Run the web app:**

```bash
dotnet run --project Annap.CoffeeQrOrdering.Web
```

The app binds to `http://localhost:8080` when the port is free. Migrations apply on startup when `Database:ApplyMigrationsOnStartup` is `true` (default in Development).

**3. Build CSS (from `Annap.CoffeeQrOrdering.Web/`):**

```bash
npm install
npm run build:css
```

## Production deployment

**1. Create `.env` (do not commit):**

```env
POSTGRES_DB=annap_qr_ordering
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your-strong-password
StaffAuth__UserName=host
StaffAuth__Password=your-admin-password-at-least-12-chars
StaffAuth__CheckoutPassword=your-checkout-password-at-least-12-chars
StaffAuth__BaristaPassword=your-barista-password-at-least-12-chars
Database__ApplyMigrationsOnStartup=true
AppUrl__PublicBaseUrl=https://order.example.com
Cloudinary__CloudName=your-cloud-name
Cloudinary__ApiKey=your-api-key
Cloudinary__ApiSecret=your-api-secret
DataProtection__KeysPath=/var/data/dataprotection-keys
```

**Production checklist:** all three `StaffAuth__*Password` values must be at
least 12 characters and not known weak values. Production also requires
Cloudinary credentials and an absolute `DataProtection__KeysPath` on durable
storage. `ProductionStartupGuard` refuses to start with unsafe configuration.

**2. Start stack:**

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

**3. Access:**

| Surface | URL |
|---------|-----|
| Guest menu | `http://localhost:8080/Menu/Index?vt={table-guid}` |
| Staff admin | `http://localhost:8080/admin` |
| Health | `http://localhost:8080/health` |
| Infrastructure diagnostics | `http://localhost:8080/admin/system/infrastructure` |

**4. One-time image migration** (if upgrading from legacy JPG/PNG):

```bash
dotnet run --project Annap.CoffeeQrOrdering.Web -- --migrate-images-only
```

## Staff admin capabilities

- **Drinks** — add, edit, archive, upload images, category, availability, tasting copy, origin, ingredients, seasonal/signature flags
- **Bakery (Bánh)** — lightweight form (name, price, image) in bakery category
- **Menu order** — `DisplaySortOrder` per item; category order at `/admin/menu/categories`
- **Images** — JPG/PNG/WebP upload → Cloudinary WebP resize and durable HTTPS URL

Pairing suggestions on drink detail are algorithmic (bakery items in the Bánh category); no manual pairing CMS yet.

## Guest APIs (stable)

- `GET /api/menu` — menu catalog JSON
- `POST /api/orders` — place order
- `GET /api/orders/{orderId}` — order status
- `GET /menu/drink/{id}?handler=Data` — drink detail JSON for overlay
- `POST /api/guest/guided-sommelier/recommend` — in-app sommelier flow
- `POST /api/guest/discovery/reveal` — discovery envelope flow

Prototype chat and generic recommendation APIs have been removed.

## Configuration

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL |
| `StaffAuth:UserName` / `StaffAuth:*Password` | Shared role login |
| `Database:ApplyMigrationsOnStartup` | Auto-migrate on boot |
| `Cloudinary:*` | Durable menu image storage |
| `DataProtection:KeysPath` | Durable staff-cookie key ring |

Canonical Docker topology:

- PostgreSQL: service `postgres`, container `annap-postgres`, port `5432:5432`, volume `annap_pgdata`.
- Web: service `web`, container `annap-web`, port `8080:8080`, connects internally to `postgres:5432`.
- Host-machine development (`dotnet run`) uses `localhost:5432`; container-to-container traffic uses `postgres:5432`.

If PostgreSQL is unavailable, verify Docker Desktop is running and use:

```bash
docker ps
docker logs annap-postgres
docker compose up -d
```

If Windows reports locked `bin/Debug/net8.0` files during rebuilds, reset the local host cleanly:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/dev-reset.ps1
```

## Debug (development only)

Set in browser console before navigation:

```js
window.__ANNAP_DEBUG = true;
```

Enables boot checklist logs and guest-experience diagnostics. Do not enable in production guest sessions.

## Operational notes

- Production menu uploads are stored in Cloudinary. `wwwroot/media/menu-items/` is a Development/legacy fallback only.
- Demo menu cleanup runs once via `DbInitializer` when no orders reference legacy demo data
- Mobile stability: no page reload on detail open/close; images released on overlay unmount
