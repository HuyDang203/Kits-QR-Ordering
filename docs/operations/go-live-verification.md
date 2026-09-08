# Go-Live Verification (Phase 8.6)

Automated production-readiness verification for Annap specialty coffee launch. Replaces manual SQL checks from the Phase 8.4 deployment runbook with a single authoritative command.

## Command

From the web project directory:

```bash
cd Annap.CoffeeQrOrdering.Web
dotnet run -- verify-go-live
```

Production (Docker Compose):

```bash
docker compose -f docker-compose.prod.yml exec annap-web \
  dotnet Annap.CoffeeQrOrdering.Web.dll verify-go-live
```

Set `ASPNETCORE_ENVIRONMENT=Production` and ensure `ConnectionStrings__DefaultConnection` (or `.env`) points at the target database before running.

### Optional health URL

When the app is already serving traffic, set a public base URL so the verifier hits the live `/health` endpoint instead of starting a temporary local listener:

```bash
# appsettings.Production.json or environment
GoLive__HealthUrl=https://order.example.com/health
# or
AppUrl__PublicBaseUrl=https://order.example.com
```

## What it checks

| Gate | Description |
|------|-------------|
| **Database** | PostgreSQL reachable; prints database name and environment |
| **Specialty Lots** | Catalog keys `03732`, `03734`, `03757`, `03028` exist with `IsAvailable=true`, `IsArchived=false`, `IsSignature=true`, category **Specialty Coffee** |
| **Signature Integrity** | No other coffee-family items (`Espresso`, `Cold Brew`, `Vietnamese Coffee`, `Coffee`, `Specialty Coffee`) have `IsSignature=true` |
| **Pool Size** | Specialty recommendation pool count matches production engine logic; **expected `specialty_pool_count = 4`** |
| **Editorial Content** | `Origin`, `ShortStory`, and `ProducerStory` populated for all four flagship coffees |
| **Bootstrap** | Specialty catalog rows plus guided discovery questions `q_sp_body` and `q_sp_flavor` present |
| **Health** | EF health checks, database connectivity, and `GET /health` |
| **OpenAI (advisory)** | Reports `Sommelier:ApiKey` presence; **does not block** specialty go-live |

The verifier does **not** mutate data. It skips production bootstrap and migrations.

## Expected output (ready)

```
ANNAP Go-Live Verification
============================

1. Database connectivity
   Database: annap_qr_ordering
   Environment: Production
   Target: postgres:5432/annap_qr_ordering
   Status: REACHABLE

4. Recommendation pool audit
   specialty_pool_count = 4
   Status: PASS

6. Bootstrap verification
   Specialty bootstrap readiness: PASS

====================================
ANNAP GO-LIVE REPORT
====================

Database ............. PASS
Specialty Lots ....... PASS
Signature Integrity .. PASS
Pool Size ............ PASS
Editorial Content .... PASS
Bootstrap ............ PASS
Health ............... PASS

FINAL STATUS:

READY FOR PRODUCTION TRAFFIC
```

Exit code: **0**

## Expected output (not ready)

Any failed gate prints `FAIL` with an exact reason under the summary line, for example:

```
Pool Size ............ FAIL
  -> specialty_pool_count=15, expected 4 (production specialty recommendation pool).

FINAL STATUS:

NOT READY FOR PRODUCTION TRAFFIC
Pool Size: specialty_pool_count=15, expected 4 (production specialty recommendation pool).
```

Exit code: **1**

## Interpretation

| Exit code | Verdict | Action |
|-----------|---------|--------|
| `0` | **READY FOR PRODUCTION TRAFFIC** | Proceed with deployment authorization (Phase 8.4 runbook) |
| `1` | **NOT READY FOR PRODUCTION TRAFFIC** | Fix the listed gate(s); re-run until exit code `0` |

Common failures:

- **Database FAIL** — Postgres down, wrong connection string, or network/firewall between web and DB.
- **Specialty Lots / Bootstrap FAIL** — Run production bootstrap once (`Database__ApplyMigrationsOnStartup=true` on first deploy) or restore from a known-good backup.
- **Signature Integrity FAIL** — A non-flagship coffee item was marked `IsSignature=true`; clear the flag in staff admin or SQL.
- **Pool Size FAIL** — Usually signature contamination or missing specialty rows; fix Specialty Lots and Signature Integrity first.
- **Editorial Content FAIL** — Re-run specialty bootstrap or restore editorial fields for the four catalog keys.
- **Health FAIL** — App unhealthy or `/health` unreachable.
- **OpenAI WARN (non-blocking)** — `Sommelier:ApiKey` / `Sommelier__ApiKey` not set. Specialty guided recommendations still work; legacy mood sommelier uses `SimulatedSommelierService` instead of RAG.

## Deployment procedure

1. Deploy application and database to staging or production per `docs/operations/deployment-runbook.md` (Phase 8.4).
2. Ensure environment variables and connection strings target the **production** database.
3. Run `dotnet run -- verify-go-live` (or the Docker exec variant above).
4. Confirm exit code `0` and **READY FOR PRODUCTION TRAFFIC**.
5. Archive the command output with the deployment record.
6. Switch traffic / complete go-live checklist only after a passing run.

Re-run verification after any menu, bootstrap, or infrastructure change that could affect specialty coffee or health.

## Notes

- Verification uses the same coffee-family filter and signature-only pool narrowing as `/api/guest/guided-sommelier/recommend` for the specialty discovery path.
- OpenAI is **optional** for specialty launch. It powers RAG enhancements on `POST /api/sommelier/suggest` and menu embedding backfill; both degrade gracefully when the key is absent.
- In development, a failing run is expected if the database is empty or bootstrap has not run.
