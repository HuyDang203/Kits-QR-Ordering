# Render + Neon + Cloudinary deployment checklist

Complete in order for the first production deployment.

## Pre-flight

- [ ] `dotnet build -c Release` succeeds
- [ ] Full test result reviewed; any infrastructure-skipped tests are recorded
- [ ] Cloudinary account and dedicated `annap/menu-items` folder are ready
- [ ] Neon project is in the same or nearest region to Render
- [ ] Cloudflare production hostname is active with Full (strict) TLS

## Render Starter web service

| Setting | Value |
|---|---|
| Runtime | Docker |
| Root directory | Repository root |
| Dockerfile | `Dockerfile` |
| Health check path | `/health` |
| Instance | Starter |

Attach a Render persistent disk:

| Setting | Value |
|---|---|
| Mount path | `/var/data` |
| Purpose | ASP.NET Core Data Protection keys |
| Minimum size | Smallest available durable disk |

Do not store menu images on this disk. Menu uploads are stored in Cloudinary.

## Required environment variables

| Variable | First deployment value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:${PORT}` |
| `DATABASE_URL` | Neon pooled connection URL including `sslmode=require` |
| `StaffAuth__UserName` | `host` or the chosen shared admin username |
| `StaffAuth__Password` | Unique value, minimum 12 characters |
| `StaffAuth__CheckoutPassword` | Unique value, minimum 12 characters |
| `StaffAuth__BaristaPassword` | Unique value, minimum 12 characters |
| `AppUrl__PublicBaseUrl` | Cloudflare HTTPS origin, e.g. `https://order.example.com` |
| `Database__ApplyMigrationsOnStartup` | `true` |
| `Cloudinary__CloudName` | Cloudinary cloud name |
| `Cloudinary__ApiKey` | Cloudinary API key |
| `Cloudinary__ApiSecret` | Cloudinary API secret |
| `Cloudinary__Folder` | `annap/menu-items` |
| `DataProtection__KeysPath` | `/var/data/dataprotection-keys` |
| `DataProtection__ApplicationName` | `Annap.CoffeeQrOrdering` |
| `BankTransfer__Webhook__DevWebhookEnabled` | `false` |

Use `ConnectionStrings__DefaultConnection` only as an alternative to
`DATABASE_URL`; never set both unintentionally. Production normalizes either
connection to SSL-required mode and rejects `SSL Mode=Disable`.

## Conditional environment variables

Set these only when bank transfer is enabled:

- `BankTransfer__Enabled=true`
- `BankTransfer__Provider=VietQR`
- `BankTransfer__BankBin`
- `BankTransfer__BankName`
- `BankTransfer__AccountNumber`
- `BankTransfer__AccountName`
- `BankTransfer__DescriptionTemplate=ANNAP {Reference}`
- `BankTransfer__QrImageUrlTemplate`

`Sommelier__ApiKey` is optional. There is no `OpenAI__ApiKey` alias.

## First deployment

- [ ] Deploy the Render service
- [ ] Logs contain `EF Core migrations applied successfully`
- [ ] Logs contain `Menu media storage: Cloudinary`
- [ ] `/health` is Healthy
- [ ] Run `dotnet Annap.CoffeeQrOrdering.Web.dll verify-go-live`; exit code is 0
- [ ] Upload, replace, and remove a menu image; confirm the database stores an HTTPS `res.cloudinary.com` URL
- [ ] Redeploy once and confirm staff cookie and uploaded image still work
- [ ] Set `Database__ApplyMigrationsOnStartup=false` after the first healthy migration

## Live smoke test

- [ ] Scan a printed/table QR using the Cloudflare hostname
- [ ] Submit guest notes and one order for each enabled payment method
- [ ] Confirm payment as checkout staff
- [ ] Prepare and complete as barista
- [ ] Close a shift
- [ ] Confirm reports/statistics include the transaction
- [ ] Confirm SignalR updates; verify polling fallback by briefly disconnecting

## Backup

- [ ] Neon automated backups/PITR are enabled for the selected plan
- [ ] A manual logical backup procedure is documented and tested
- [ ] Cloudinary originals/backups policy is understood
- [ ] Render disk snapshot/backup covers `/var/data` Data Protection keys
