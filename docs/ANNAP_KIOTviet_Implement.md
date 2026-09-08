 ANNAP × KiotViet — Production Integration Architecture

  ---
  Preface: Integration Philosophy

  KiotViet is an operational sink. It receives finalized ANNAP orders. It never shapes the guest experience. The integration is invisible
  infrastructure — like the kitchen behind a restaurant wall. The guest sees warm paper and sommelier letters. KiotViet sees structured order data.

  The architecture is designed around one iron rule:

  ▎ ANNAP must function completely if KiotViet is unavailable.

  ---
  I. Codebase State Assessment

  From the audit, the current domain is clean and well-bounded:

  ┌─────────────────────┬────────────────────────────────────────────┬────────────────────────────────────────────────────┐
  │        Layer        │                   State                    │               Integration Readiness                │
  ├─────────────────────┼────────────────────────────────────────────┼────────────────────────────────────────────────────┤
  │ Domain entities     │ Clean, no external coupling                │ Ready — add KiotViet outbox entities               │
  ├─────────────────────┼────────────────────────────────────────────┼────────────────────────────────────────────────────┤
  │ Order lifecycle     │ 7 states, idempotency key, serializable TX │ Ready — hook into Submitted transition             │
  ├─────────────────────┼────────────────────────────────────────────┼────────────────────────────────────────────────────┤
  │ Menu/Ingredient     │ Rich editorial + CatalogKey exists         │ Ready — CatalogKey becomes KiotViet product anchor │
  ├─────────────────────┼────────────────────────────────────────────┼────────────────────────────────────────────────────┤
  │ Background services │ One HostedService (embedding only)         │ Ready — register additional workers                │
  ├─────────────────────┼────────────────────────────────────────────┼────────────────────────────────────────────────────┤
  │ External HTTP       │ OpenAI client with retry/fallback          │ Pattern to replicate for KiotViet                  │
  ├─────────────────────┼────────────────────────────────────────────┼────────────────────────────────────────────────────┤
  │ Queue / Outbox      │ None currently                             │ Must add — critical gap                            │
  ├─────────────────────┼────────────────────────────────────────────┼────────────────────────────────────────────────────┤
  │ Webhook receiver    │ None currently                             │ Must add in Phase 4                                │
  └─────────────────────┴────────────────────────────────────────────┴────────────────────────────────────────────────────┘

  Critical finding: There is no transactional outbox. Orders submitted while KiotViet is down would be silently lost if we push synchronously. The    
  outbox pattern is the cornerstone of this integration.

  ---
  II. Integration Boundaries

  ┌─────────────────────────────────────────────────────────────────┐
  │                        ANNAP DOMAIN                              │
  │                                                                  │
  │  Guest QR Experience                                             │
  │  ↓                                                               │
  │  Tray (client-side)                                              │
  │  ↓                                                               │
  │  POST /api/orders                                                │
  │  ↓                                                               │
  │  Order entity (Submitted)     ←— ANNAP owns this forever        │
  │  ↓ (same DB transaction)                                         │
  │  KiotVietOutboxMessage (Pending)  ←— integration anchor         │
  │  ↓                                                               │
  │  Commit                                                          │
  │  ↓ (async, decoupled)                                           │
  │  ┌────────────────────────────────────────────┐                 │
  │  │  KiotViet Integration Layer                │                 │
  │  │  (Infrastructure, invisible to guests)     │                 │
  │  │                                            │                 │
  │  │  OrderDispatchWorker                       │                 │
  │  │  ↓                                         │                 │
  │  │  KiotVietHttpClient (OAuth2)               │                 │
  │  │  ↓                                         │                 │
  │  └─────────────────┬──────────────────────────┘                 │
  └────────────────────┼────────────────────────────────────────────┘
                       │
                       ↓  (HTTPS, structured JSON)
                ┌──────────────┐
                │  KiotViet    │
                │  POS API     │
                │              │
                │  Orders      │
                │  Products    │
                │  Inventory   │
                └──────────────┘

  What ANNAP owns permanently:

  - All guest-facing flows (QR, tray, correspondence, sommelier)
  - Order entity and order lifecycle transitions
  - Editorial content (TastingNotes, MoodProfile, ShortStory, etc.)
  - Sensory profiling, discovery logic, embedding vectors
  - Staff board and floor coordination
  - Operational audit trail
  - Real-time SignalR notifications

  What KiotViet owns:

  - POS record of the order (financial/accounting source of truth)
  - Product catalog as pricing source
  - Ingredient/stock inventory levels
  - Sales reporting

  What is shared (synchronized, not replaced):

  - Order data at submission time (push only, one-way)
  - Product prices and availability (pull from KiotViet → ANNAP)
  - Inventory levels (pull from KiotViet → ANNAP Ingredient stock)

  ---
  III. Full Architecture Design

  1. Integration Layer Structure

  Annap.CoffeeQrOrdering.Domain/
  └── Entities/
      └── KiotViet/
          ├── KiotVietOutboxMessage.cs       ← Outbox record per order
          ├── KiotVietOutboxStatus.cs        ← Enum: Pending/Processing/Succeeded/Failed/DeadLettered
          ├── KiotVietSyncLog.cs             ← General integration event log
          └── KiotVietProductMapping.cs      ← ANNAP MenuItem ↔ KiotViet product anchor

  Annap.CoffeeQrOrdering.Application/
  └── Abstractions/
      ├── IKiotVietOrderSyncService.cs       ← Push interface
      ├── IKiotVietProductSyncService.cs     ← Product pull interface
      └── IKiotVietInventorySyncService.cs   ← Inventory pull interface

  Annap.CoffeeQrOrdering.Infrastructure/
  └── KiotViet/
      ├── Auth/
      │   ├── KiotVietTokenProvider.cs       ← OAuth2 client credentials, token cache
      │   └── KiotVietAuthHandler.cs         ← DelegatingHandler: attaches Bearer token
      ├── Client/
      │   ├── KiotVietHttpClient.cs          ← Typed HttpClient (orders, products, inventory)
      │   └── KiotVietClientOptions.cs       ← Config binding
      ├── DTOs/
      │   ├── Outbound/
      │   │   ├── KvOrderCreateDto.cs        ← ANNAP → KiotViet order shape
      │   │   └── KvOrderLineDto.cs          ← Per-item line
      │   └── Inbound/
      │       ├── KvProductDto.cs            ← Product catalog response
      │       ├── KvInventoryDto.cs          ← Stock levels response
      │       └── KvWebhookPayloadDto.cs     ← Webhook event envelope
      ├── Mapping/
      │   ├── KvOrderMapper.cs               ← Order → KvOrderCreateDto
      │   └── KvProductMapper.cs             ← KvProductDto → MenuItem (price/availability only)
      ├── Services/
      │   ├── KiotVietOrderSyncService.cs    ← Push single order to KiotViet
      │   ├── KiotVietProductSyncService.cs  ← Pull and map products
      │   └── KiotVietInventorySyncService.cs← Pull and update Ingredient stock
      ├── Workers/
      │   ├── KvOrderDispatchWorker.cs       ← Outbox processor (5s poll)
      │   ├── KvInventoryPollWorker.cs       ← Stock sync (configurable interval)
      │   └── KvProductSyncWorker.cs         ← Catalog sync (configurable interval)
      └── Webhooks/
          ├── KvWebhookProcessor.cs          ← Routes webhook events to handlers
          └── KvWebhookSignatureValidator.cs ← HMAC-SHA256 verification

  Annap.CoffeeQrOrdering.Web/
  └── Endpoints/
      └── KiotViet/
          └── KvWebhookEndpoints.cs          ← POST /api/kiotviet/webhook (Phase 4)

  ---
  2. Order Synchronization Flow

  The flow has two completely separate phases, joined only by the outbox table:

  Phase A: Guest Experience (synchronous, zero KiotViet dependency)

  Guest submits tray
    → POST /api/orders
    → Validate inventory gate (ANNAP Ingredient stock — local)
    → BEGIN TRANSACTION (Serializable)
        → INSERT Order (Status = Submitted)
        → INSERT KiotVietOutboxMessage
            (EventType = "OrderSubmitted", Status = Pending, Payload = JSON)
    → COMMIT TRANSACTION
    → SignalR: NotifyStaffBoardAsync()
    → Return 201 to guest with order token

  The guest response is immediate. KiotViet never touches this path.

  Phase B: KiotViet Dispatch (async, background, isolated)

  KvOrderDispatchWorker (every 5 seconds)
    → SELECT TOP 20 FROM KiotVietOutboxMessages
        WHERE Status = Pending
          AND (NextRetryAtUtc IS NULL OR NextRetryAtUtc <= NOW())
        ORDER BY CreatedAtUtc ASC
        FOR UPDATE SKIP LOCKED    ← safe for multi-instance deploys

    FOR EACH message:
      → Mark Status = Processing
      → Deserialize Payload → Order data
      → Map Order → KvOrderCreateDto
          (includes BranchId, TableId from VenueTable.KiotVietTableId,
           product lines with CatalogKey as ProductCode,
           order reference as ANNAP order ID)
      → POST to KiotViet /api/Order
      → ON SUCCESS:
          → Status = Succeeded
          → Store KiotVietOrderId on outbox message
          → Write KiotVietSyncLog (success, latency)
      → ON FAILURE (transient 5xx / timeout):
          → RetryCount++
          → NextRetryAtUtc = exponential_backoff(RetryCount)
          → Status = Failed (if RetryCount < MaxRetries) or DeadLettered
          → Write KiotVietSyncLog (failure, reason)
          → IF DeadLettered: write OperationalAuditEntry "kiotviet.dead_letter"

  Exponential backoff schedule for outbox retries:

  ┌───────┬──────────────────────┐
  │ Retry │        Delay         │
  ├───────┼──────────────────────┤
  │ 1st   │ 30 seconds           │
  ├───────┼──────────────────────┤
  │ 2nd   │ 3 minutes            │
  ├───────┼──────────────────────┤
  │ 3rd   │ 15 minutes           │
  ├───────┼──────────────────────┤
  │ 4th   │ 1 hour               │
  ├───────┼──────────────────────┤
  │ 5th   │ 4 hours → DeadLetter │
  └───────┴──────────────────────┘

  ---
  3. Entity Definitions

  KiotVietOutboxMessage

  public sealed class KiotVietOutboxMessage
  {
      public Guid Id { get; init; }
      public Guid OrderId { get; init; }           // FK → Order
      public string EventType { get; init; }        // "OrderSubmitted" | "OrderCancelled"
      public string Payload { get; init; }          // JSON snapshot of order at event time
      public KiotVietOutboxStatus Status { get; set; }
      public int RetryCount { get; set; }
      public DateTimeOffset? NextRetryAtUtc { get; set; }
      public DateTimeOffset? ProcessedAtUtc { get; set; }
      public string? KiotVietOrderId { get; set; } // Assigned by KiotViet on success
      public string? FailureReason { get; set; }
      public DateTimeOffset CreatedAtUtc { get; init; }
  }

  public enum KiotVietOutboxStatus
  {
      Pending = 0,
      Processing = 1,
      Succeeded = 2,
      Failed = 3,
      DeadLettered = 4
  }

  KiotVietSyncLog

  public sealed class KiotVietSyncLog
  {
      public long Id { get; init; }                  // sequential, not Guid (high-volume)
      public string SyncKind { get; init; }          // "OrderPush" | "ProductSync" | "InventorySync" | "Webhook"
      public bool IsSuccess { get; init; }
      public string? ReferenceId { get; init; }      // ANNAP order ID or product code
      public string? KiotVietReference { get; init; }// KiotViet's assigned ID
      public int? HttpStatusCode { get; init; }
      public string? FailureReason { get; init; }
      public long DurationMs { get; init; }
      public DateTimeOffset OccurredAtUtc { get; init; }
  }

  KiotVietProductMapping

  public sealed class KiotVietProductMapping
  {
      public Guid MenuItemId { get; init; }          // FK → MenuItem
      public string KiotVietProductCode { get; init; } // KiotViet's product code
      public string? KiotVietProductName { get; set; } // Cached for diagnostics
      public bool IsActive { get; set; }
      public DateTimeOffset LastSyncedAtUtc { get; set; }
      public string? SyncNote { get; set; }          // e.g., "price updated from 75000 → 85000"
  }

  VenueTable (modification)

  // Add to existing VenueTable entity:
  public string? KiotVietTableId { get; set; }    // KiotViet's numeric table ID (nullable until mapped)
  public int? KiotVietBranchId { get; set; }      // For future multi-branch support

  ---
  4. Authentication Strategy

  KiotViet uses OAuth2 Client Credentials (Vietnamese POS standard).

  POST https://id.kiotviet.vn/connect/token
    Content-Type: application/x-www-form-urlencoded

    grant_type=client_credentials
    &client_id={CLIENT_ID}
    &client_secret={CLIENT_SECRET}
    &scopes=PublicApi.Access

  → { "access_token": "...", "expires_in": 3600 }

  Token management:

  // KiotVietTokenProvider — in-memory cache, thread-safe
  // Refresh when: (now + 90s) >= expiry
  // Never expose token in logs
  // Config:
  //   KIOTVIET__CLIENTID   (env var)
  //   KIOTVIET__CLIENTSECRET (env var)
  //   KIOTVIET__RETAILER   (retailer subdomain)

  KiotVietAuthHandler (DelegatingHandler) wraps all outbound requests:
  - Calls KiotVietTokenProvider.GetTokenAsync()
  - Sets Authorization: Bearer {token} header
  - On 401 response: force-refreshes token, retries once

  Configuration binding:

  // appsettings.json (keys only — values from environment)
  "KiotViet": {
    "ClientId": "",
    "ClientSecret": "",
    "Retailer": "",
    "BranchId": 0,
    "BaseUrl": "https://public.kiotapi.com",
    "AuthUrl": "https://id.kiotviet.vn/connect/token",
    "OrderDispatchIntervalSeconds": 5,
    "InventoryPollIntervalMinutes": 5,
    "ProductSyncIntervalMinutes": 60,
    "MaxDispatchRetries": 5,
    "WebhookSecret": "",
    "IsEnabled": false
  }

  IsEnabled: false means the integration does nothing until explicitly switched on. ANNAP always functions regardless of this flag.

  ---
  5. DTO Strategy

  Orders flow out of ANNAP in a mapped, POS-compatible shape. KiotViet's API shape should never cross the Domain or Application layers.

  Mapping boundary: Infrastructure layer only. KvOrderMapper.cs is the sole point of translation.

  Order (Domain)
    + MenuItem (via OrderItem.MenuItemId join)
    + VenueTable (via VenueTableId join)
    + KiotVietProductMapping (MenuItem → KV product code lookup)
    ↓
  KvOrderCreateDto (Infrastructure, sent to KiotViet API)

  // KvOrderCreateDto shape:
  {
    BranchId: int,                       // from KiotVietOptions.BranchId
    TableId: long?,                      // VenueTable.KiotVietTableId (nullable)
    OrderCode: string,                   // "ANN-{YYMMdd}-{short order id}"
    SaleChannelId: int,                  // 1 = dine-in
    Note: string,                        // "Bàn {DisplayCode} · Ghi chú của khách"
    OrderDetails: [
      {
        ProductCode: string,             // KiotVietProductMapping.KiotVietProductCode
        ProductName: string,             // MenuItem.Name (fallback if code missing)
        Quantity: decimal,
        Price: decimal,
        Note: string?,                   // OrderItem.Notes
      }
    ]
  }

  Mapping failure strategy: If a MenuItem has no KiotVietProductMapping, include it by name without a product code. KiotViet accepts this as a        
  free-text line item. The order still syncs — it is never blocked by an unmapped product.

  ---
  6. Product Mapping Strategy

  Product sync is a background reconciliation, not a blocking dependency.

  KvProductSyncWorker (hourly)
    → GET /api/products?pageSize=100 (paginated)
    → FOR EACH KvProductDto:
        → Match by KvProductDto.Code ↔ KiotVietProductMapping.KiotVietProductCode
           OR by KvProductDto.Code ↔ MenuItem.CatalogKey (initial bootstrap)
        → IF match found:
            → UPDATE MenuItem.Price (if changed)
            → UPDATE MenuItem.IsAvailable (if KvProduct.IsActive changed)
            → UPDATE KiotVietProductMapping.LastSyncedAtUtc
        → IF no match:
            → INSERT KiotVietProductMapping with MenuItemId = NULL, IsActive = false
            → Log unmapped product (admin resolves mapping manually or via future UI)
        → NEVER overwrite editorial fields (TastingNotes, MoodProfile, etc.)

  Price sync direction: KiotViet → ANNAP only. ANNAP never pushes prices to KiotViet. KiotViet is the pricing source of truth.

  Unmapped products: Logged but ignored. They will not appear on the ANNAP menu until an admin maps them.

  ---
  7. Inventory Synchronization

  KvInventoryPollWorker (every 5 minutes)
    → GET /api/inventories?branchId={BranchId}&pageSize=200
    → FOR EACH KvInventoryDto:
        → Match KvInventoryDto.ProductCode → KiotVietProductMapping → MenuItem
        → IF MenuItem has linked Ingredient:
            → UPDATE Ingredient.CurrentStock = KvInventoryDto.OnHand
            → IF new stock < Ingredient.LowStockThreshold AND was above:
                → SignalR: notify staff board (ingredient blocked)
            → IF new stock >= LowStockThreshold AND was below:
                → SignalR: notify staff board (ingredient unblocked)
        → Write KiotVietSyncLog

  Important: inventory sync updates Ingredient.CurrentStock. The existing IMenuInventoryGate already reads from this field to block menu items. No    
  other changes needed in the guest flow — the gate automatically reflects synced stock levels.

  ---
  8. Table Mapping

  Initial setup (one-time admin operation):

  Admin endpoint (Phase 1, staff-auth protected):
    GET /api/staff/kiotviet/tables     ← pulls KiotViet table list
    POST /api/staff/kiotviet/tables/map
      { annap_table_code: "T12", kiotviet_table_id: "67890" }
    → Updates VenueTable.KiotVietTableId

  Convention fallback: If VenueTable.KiotVietTableId is null, the order is pushed to KiotViet with TableId: null and the note field includes "Bàn T12"
   so staff can identify the table manually. The sync never fails due to an unmapped table.

  ---
  9. Retry and Queue Architecture

  No external message broker is required. The database outbox is sufficient for the order volumes of a boutique café.

  KvOrderDispatchWorker lifecycle:

    [Worker tick — every 5s]
      → IServiceScopeFactory.CreateScope()
      → Query: SELECT ... FOR UPDATE SKIP LOCKED
      → Acquire up to 20 pending messages
      → Process batch concurrently (up to 4 parallel, respect rate limits)
      → Release scope
      → await Task.Delay(5000)

    [Retry logic per message]
      → Polly HTTP: 3 attempts, backoff 1s / 2s / 4s (transient HTTP errors)
      → Outbox retry: 5 attempts, exponential 30s / 3m / 15m / 1h / 4h
      → Dead-letter: write OperationalAuditEntry "kiotviet.dead_letter"
      → Dead-letter: log structured warning with full payload for manual replay

    [Circuit breaker — Polly]
      → Opens after 5 consecutive failures in 30-second window
      → Half-open after 60 seconds
      → Worker continues running but skips HTTP calls while open
      → Logs "kiotviet.circuit_open" to KiotVietSyncLog

  Multi-instance safety: FOR UPDATE SKIP LOCKED ensures that if two instances of ANNAP run (e.g., blue-green deploy), they will not process the same  
  outbox message twice.

  ---
  10. Webhook / Event Handling (Phase 4)

  POST /api/kiotviet/webhook
    → Validate X-Kiotviet-Signature header (HMAC-SHA256, key = KiotViet.WebhookSecret)
    → Return 200 immediately (before processing)
    → Enqueue payload to System.Threading.Channels.Channel<KvWebhookPayloadDto>
    → KvWebhookProcessor (background consumer) processes events:

        "order.update"    → update KiotVietOutboxMessage.KiotVietOrderId if matched
        "product.update"  → trigger KvProductSyncService.SyncSingleProductAsync(code)
        "inventory.update"→ trigger KvInventorySyncService.SyncSingleProductAsync(code)

  In-process Channel (Channel<T>) is sufficient before reaching Redis territory. For a single-instance boutique café deployment, this is appropriate  
  and avoids infrastructure complexity.

  ---
  11. Background Worker Architecture

  Three workers, each an IHostedService, registered in Infrastructure.DependencyInjection:

  // Worker 1: Order dispatch (high-priority, 5s interval)
  services.AddHostedService<KvOrderDispatchWorker>();

  // Worker 2: Inventory poll (medium-priority, configurable, default 5min)
  services.AddHostedService<KvInventoryPollWorker>();

  // Worker 3: Product catalog sync (low-priority, configurable, default 60min)
  services.AddHostedService<KvProductSyncWorker>();

  Each worker:
  - Checks KiotVietOptions.IsEnabled at every tick (can be toggled without restart)
  - Creates a new DI scope per tick (avoids long-lived DbContext)
  - Logs structured telemetry per run
  - Catches all exceptions, logs, and continues (never crashes the host)

  ---
  12. Failure Resilience Design

  ┌───────────────────────────────┬───────────────────────────────────────┬───────────────────────────────────────────────────────────────────────┐   
  │       Failure Scenario        │            ANNAP Behavior             │                             Recovery Path                             │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ KiotViet API unreachable      │ Outbox messages stay Pending          │ Auto-retry on next worker tick                                        │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ KiotViet returns 4xx (bad     │ Outbox message moves to Failed        │ Check payload, fix mapping, manual replay                             │   
  │ request)                      │                                       │                                                                       │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ KiotViet rate limited (429)   │ Outbox message retries with backoff   │ Automatic                                                             │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ Token expired mid-batch       │ Auth handler refreshes and retries    │ Automatic (DelegatingHandler)                                         │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ KiotViet persistently down    │ Messages accumulate in outbox         │ ANNAP operates normally; staff processes manually; bulk replay on     │   
  │                               │                                       │ KiotViet recovery                                                     │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ Dead-lettered order           │ Written to OperationalAuditEntry      │ Admin admin replays via staff endpoint                                │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ Worker crash                  │ Outbox message remains in Processing  │ Watchdog: reset Processing → Pending for messages older than 2        │   
  │                               │ state                                 │ minutes                                                               │   
  ├───────────────────────────────┼───────────────────────────────────────┼───────────────────────────────────────────────────────────────────────┤   
  │ Database unavailable          │ Both ANNAP and integration fail       │ Shared failure — handled by existing retry in DatabaseStartupHelper   │   
  └───────────────────────────────┴───────────────────────────────────────┴───────────────────────────────────────────────────────────────────────┘   

  Stuck Processing message recovery:

  // On worker startup and periodically:
  UPDATE KiotVietOutboxMessages
    SET Status = Pending, NextRetryAtUtc = NOW()
    WHERE Status = Processing
      AND UpdatedAtUtc < NOW() - INTERVAL '2 minutes'

  ---
  13. Observability and Diagnostics

  KiotVietSyncLog provides a queryable integration audit trail:

  -- Integration health check query
  SELECT SyncKind, IsSuccess, COUNT(*), AVG(DurationMs)
  FROM KiotVietSyncLogs
  WHERE OccurredAtUtc > NOW() - INTERVAL '1 hour'
  GROUP BY SyncKind, IsSuccess
  ORDER BY SyncKind;

  Structured log enrichment:

  Every KiotViet operation logs with these properties:
  kiotviet.sync_kind         = "OrderPush" | "InventoryPoll" | etc.
  kiotviet.annap_order_id    = Guid
  kiotviet.kv_order_id       = string (on success)
  kiotviet.retry_count       = int
  kiotviet.duration_ms       = long
  kiotviet.http_status       = int
  kiotviet.circuit_state     = "Closed" | "Open" | "HalfOpen"

  Staff-facing diagnostic endpoint (staff-auth protected):

  GET /api/staff/kiotviet/status
  → {
      is_enabled: bool,
      circuit_state: string,
      pending_outbox_count: int,
      failed_outbox_count: int,
      dead_lettered_count: int,
      last_successful_sync_utc: datetime,
      last_inventory_sync_utc: datetime,
      last_product_sync_utc: datetime
    }

  POST /api/staff/kiotviet/replay/{outboxMessageId}
  → Moves DeadLettered message back to Pending

  POST /api/staff/kiotviet/sync/products
  → Triggers immediate product sync (admin action)

  ---
  14. Database Additions

  New tables:

  -- KiotVietOutboxMessages
  CREATE TABLE "KiotVietOutboxMessages" (
      "Id"               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
      "OrderId"          UUID NOT NULL REFERENCES "Orders"("Id"),
      "EventType"        TEXT NOT NULL,
      "Payload"          TEXT NOT NULL,       -- JSON snapshot
      "Status"           INT NOT NULL DEFAULT 0,
      "RetryCount"       INT NOT NULL DEFAULT 0,
      "NextRetryAtUtc"   TIMESTAMPTZ,
      "ProcessedAtUtc"   TIMESTAMPTZ,
      "KiotVietOrderId"  TEXT,
      "FailureReason"    TEXT,
      "CreatedAtUtc"     TIMESTAMPTZ NOT NULL,
      "UpdatedAtUtc"     TIMESTAMPTZ NOT NULL
  );
  CREATE INDEX ix_kv_outbox_status_retry
      ON "KiotVietOutboxMessages"("Status", "NextRetryAtUtc")
      WHERE "Status" IN (0, 3);             -- Pending + Failed only

  -- KiotVietSyncLogs
  CREATE TABLE "KiotVietSyncLogs" (
      "Id"                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
      "SyncKind"          TEXT NOT NULL,
      "IsSuccess"         BOOLEAN NOT NULL,
      "ReferenceId"       TEXT,
      "KiotVietReference" TEXT,
      "HttpStatusCode"    INT,
      "FailureReason"     TEXT,
      "DurationMs"        BIGINT NOT NULL,
      "OccurredAtUtc"     TIMESTAMPTZ NOT NULL
  );
  CREATE INDEX ix_kv_sync_logs_time ON "KiotVietSyncLogs"("OccurredAtUtc" DESC);

  -- KiotVietProductMappings
  CREATE TABLE "KiotVietProductMappings" (
      "MenuItemId"              UUID PRIMARY KEY REFERENCES "MenuItems"("Id"),
      "KiotVietProductCode"     TEXT NOT NULL,
      "KiotVietProductName"     TEXT,
      "IsActive"                BOOLEAN NOT NULL DEFAULT true,
      "LastSyncedAtUtc"         TIMESTAMPTZ NOT NULL,
      "SyncNote"                TEXT
  );
  CREATE UNIQUE INDEX ix_kv_product_code
      ON "KiotVietProductMappings"("KiotVietProductCode");

  Modified tables:

  -- VenueTable additions
  ALTER TABLE "VenueTables"
      ADD COLUMN "KiotVietTableId"  TEXT,
      ADD COLUMN "KiotVietBranchId" INT;

  No modifications to Order, MenuItem, or OrderItem. The integration never pollutes the core domain schema.

  ---
  15. Security Considerations

  ┌────────────────────────────────┬──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐   
  │            Concern             │                                                  Mitigation                                                  │   
  ├────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤   
  │ KiotViet credentials in config │ Store only as environment variables — KIOTVIET__CLIENTID, KIOTVIET__CLIENTSECRET. Never in appsettings.json  │   
  │                                │ committed to source.                                                                                         │   
  ├────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤   
  │ Webhook spoofing               │ HMAC-SHA256 signature validation on every inbound webhook. Reject without 200 if invalid.                    │   
  ├────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤   
  │ Credential leakage in logs     │ KiotVietTokenProvider explicitly masks token in structured logs. Never log client_secret.                    │   
  ├────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤   
  │ Order payload sensitivity      │ Outbox Payload contains table code and item names — treat as internal data, not guest PII. No guest names or │   
  │                                │  contact info stored.                                                                                        │   
  ├────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤   
  │ Staff replay endpoint          │ Protected by existing StaffAuth basic auth. Dead-letter replay only moves status to Pending — it does not    │   
  │                                │ bypass validation.                                                                                           │   
  ├────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤   
  │ Rate limit abuse               │ Worker respects KiotViet API rate limits via Polly rate-limit policy. Never floods KiotViet under backlog    │   
  │                                │ pressure.                                                                                                    │   
  ├────────────────────────────────┼──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤   
  │ SQL injection via KiotViet     │ All KiotViet responses mapped through typed DTOs with System.Text.Json. Never concatenated into SQL.         │   
  │ response                       │                                                                                                              │   
  └────────────────────────────────┴──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘   

  ---
  16. API Rate Limit Strategy

  KiotViet standard plans allow approximately 60 requests/minute. The dispatch worker processes at most 20 orders per 5-second tick — well under      
  limits for a boutique café.

  // Polly rate limit policy:
  // - Max 10 concurrent requests to KiotViet at any time
  // - Fixed-window: 50 requests per minute (leaving headroom)
  // - On 429: extract Retry-After header, wait that duration, then retry

  // Worker batch size: configurable, default 20 per tick
  // Worker parallelism: configurable, default 4 concurrent per tick

  For inventory and product sync (bulk operations), use server-side pagination with delay between pages:

  // KvInventoryPollWorker: GET /inventories?pageSize=100&currentItem={cursor}
  // Insert 200ms delay between pages to respect rate limits

  ---
  IV. Phased Implementation Plan

  Phase 1: Basic Order Push (Foundation)

  Goal: Every submitted ANNAP order appears in KiotViet in near-real-time. ANNAP never fails if KiotViet is down.

  Deliverables:
  1. KiotVietOutboxMessage entity + EF migration
  2. KiotVietSyncLog entity + EF migration
  3. VenueTable migration (add KiotVietTableId)
  4. KiotVietClientOptions + appsettings binding
  5. KiotVietTokenProvider (OAuth2 client credentials)
  6. KiotVietAuthHandler (DelegatingHandler)
  7. KiotVietHttpClient (typed, orders endpoint only)
  8. KvOrderMapper (Order → KvOrderCreateDto)
  9. KiotVietOrderSyncService (push single order)
  10. Modify order submission endpoint: write outbox message in same transaction
  11. KvOrderDispatchWorker (5s poll, FOR UPDATE SKIP LOCKED)
  12. Retry logic (Polly HTTP + outbox exponential backoff)
  13. Dead-letter to OperationalAuditEntry
  14. Staff diagnostic endpoint: /api/staff/kiotviet/status
  15. Staff replay endpoint: /api/staff/kiotviet/replay/{id}
  16. Table mapping admin endpoint

  Success criteria: Submit an order from QR flow → appears in KiotViet POS within 10 seconds. Disable KiotViet → ANNAP continues, orders queue in     
  outbox, sync resumes automatically on restoration.

  ---
  Phase 2: Product and Price Sync

  Goal: KiotViet is the pricing source of truth. Menu prices stay current automatically.

  Deliverables:
  1. KiotVietProductMapping entity + EF migration
  2. KvProductSyncService (paginated catalog pull)
  3. KvProductSyncWorker (60-minute interval)
  4. Price/availability update logic (never touches editorial fields)
  5. Admin trigger: POST /api/staff/kiotviet/sync/products
  6. Unmapped product logging
  7. Bootstrap: CatalogKey → KV product code initial mapping tool

  Success criteria: Change a drink price in KiotViet → price updates in ANNAP menu within 1 hour (or immediately via manual trigger).

  ---
  Phase 3: Inventory Synchronization

  Goal: ANNAP menu reflects real stock. Guests cannot order unavailable items.

  Deliverables:
  1. KvInventoryPollWorker (5-minute interval)
  2. KvInventorySyncService (stock level pull and update)
  3. Ingredient → KiotViet product mapping (extend KiotVietProductMapping)
  4. SignalR notification when item becomes blocked/unblocked
  5. Stock monitoring in staff diagnostic endpoint

  Success criteria: Deplete an ingredient in KiotViet → corresponding menu items become unavailable in ANNAP within 5 minutes. Restock → items become 
  available within 5 minutes.

  ---
  Phase 4: Realtime Operational Updates via Webhooks

  Goal: Replace polling with event-driven updates. KiotViet pushes changes to ANNAP.

  Deliverables:
  1. KvWebhookEndpoints.cs (POST /api/kiotviet/webhook)
  2. KvWebhookSignatureValidator (HMAC-SHA256)
  3. System.Threading.Channels.Channel<KvWebhookPayloadDto> (in-process queue)
  4. KvWebhookProcessor (background consumer)
  5. Event handlers: order.update, product.update, inventory.update
  6. Reduce KvInventoryPollWorker interval to 30 minutes (webhooks as primary, poll as fallback)
  7. Reduce KvProductSyncWorker interval to 6 hours (same reason)
  8. Polly circuit breaker (5 failures → open, 60s half-open)
  9. Circuit state exposed in staff diagnostic endpoint

  Success criteria: KiotViet fires a webhook → ANNAP processes within 1 second. KiotViet unreachable → circuit opens, poll fallback kicks in, ANNAP   
  continues operating.

  ---
  Phase 5: Advanced Analytics and Operational Intelligence

  Goal: ANNAP operational insights enriched with KiotViet financial data.

  Deliverables:
  1. KiotViet sales report API integration
  2. Cross-reference ANNAP order metrics with KiotViet transaction records
  3. Daily sync reconciliation (detect any missing order pushes)
  4. Staff analytics view: ANNAP order flow vs KiotViet receipt confirmation
  5. Automated reconciliation: detect dead-lettered orders that were not manually replayed

  Success criteria: Zero unaccounted orders. Every ANNAP submitted order has a corresponding KiotViet record or an explicit dead-letter audit entry.  

  ---
  V. Summary: What Must Never Happen

  These anti-patterns are architectural violations, not preferences:

  1. Never block guest order submission on KiotViet availability. The outbox must be written in the same transaction as the Order, and the guest      
  response must return before any KiotViet call is attempted.
  2. Never surface KiotViet IDs, codes, or error messages to guests. All KiotViet references stay in the Infrastructure layer and staff endpoints.    
  3. Never write KiotViet DTOs to the Domain or Application layers. They belong in Infrastructure only, behind the mapper boundary.
  4. Never sync KiotViet product descriptions into ANNAP editorial fields. TastingNotes, MoodProfile, ShortStory, DiscoveryStory are permanently owned
   by ANNAP. Only Price and IsAvailable follow KiotViet.
  5. Never let a worker thread crash the host. Every worker wraps its tick in try/catch, logs, and continues.
  6. Never store credentials in appsettings.json. Environment variables only.

  ---
  This architecture gives you a clean separation: the guest receives hospitality, KiotViet receives structured order data, and the café never has to  
  choose between them. The integration is genuinely invisible — not because it is hidden, but because it was designed to never need to be seen.       

  Ready to proceed to Phase 1 implementation on your command.