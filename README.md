# InventorySync

> An ASP.NET Core integration service that syncs inventory and purchase-order records between a mock ERP system and SQL Server, with field-level audit logging, configurable conflict resolution, and a vanilla-JS operations console.

![CI](https://github.com/AbhiramCommits/InventorySync/actions/workflows/ci.yml/badge.svg)
![Coverage](https://img.shields.io/badge/coverage-82%25-green)
![License](https://img.shields.io/badge/license-MIT-blue)
![Tag](https://img.shields.io/badge/tag-v1.0.0-blue)

InventorySync is a layered .NET 8 service that pages through a (mock) legacy ERP system over REST and SOAP/XML, maps its deliberately awkward wire format
(`ITM_SKU`, string quantities, `yyyyMMddHHmmss` dates) into typed domain entities, and upserts them into SQL Server by natural key. Every field-level change is written to an audit trail, local edits are protected by a configurable conflict-resolution strategy
(`ErpWins` / `LocalWins` / `NewerWins`), and failed records never abort a run — they are isolated, counted, and retried through a child sync run. A framework-free admin console on top of the Web API provides paging, filtering, inline editing, sync triggering with live polling, and visual old/new diffs of every audit entry.

## Architecture

```mermaid
flowchart LR
    subgraph Browser
        UI[Vanilla JS Ops Console]
    end

    subgraph API["InventorySync.Api (ASP.NET Core)"]
        C[Controllers + Validation]
        R[Rate limiter / Output cache / Compression]
    end

    subgraph Core["InventorySync.Core"]
        E[Domain entities + DTOs + interfaces]
    end

    subgraph Infra["InventorySync.Infrastructure"]
        HC[ErpHttpClient<br/>Polly retry + circuit breaker]
        SYNC[SyncService<br/>upsert + conflict resolution + audit]
        REPO[Repositories]
        EF[EF Core SyncDbContext]
    end

    subgraph SQL[(SQL Server)]
    end

    UI -->|"REST (ETag/304, gzip)"| C
    C --> SYNC
    SYNC --> HC
    HC -->|"REST + SOAP/XML, X-Correlation-Id"| MockErp[MockErp<br/>legacy ERP simulator]
    SYNC --> REPO --> EF --> SQL
    C --> EF
```

- **MockErp** (`src/MockErp`) is a standalone ASP.NET Core app that emulates a legacy ERP: paged REST endpoints with prefixed field names, string-typed quantities, compact date strings, a SOAP/XML `GetItemDetail` endpoint, and a configurable fault injector (`failRate`, `malformedRate`).
- **ErpHttpClient** pages through the ERP behind a Polly retry (3 attempts, exponential backoff + jitter) and a circuit breaker, and forwards the request's `X-Correlation-Id` header to the ERP.
- **SyncService** matches ERP records to local rows by natural key (`Sku` / `PoNumber`), diffs field by field, resolves conflicts per strategy, and writes one `SyncAuditEntry` per changed field.
- **EF Core** persists everything to SQL Server with a code-first model, filtered/covering reporting indexes, and an optimistic-concurrency `rowversion` token.

## Quickstart

### Docker (recommended)

```bash
git clone https://github.com/AbhiramCommits/InventorySync.git
cd InventorySync

docker compose up -d --build          # SQL Server + MockErp + API (migrations run on startup)
docker compose exec api dotnet InventorySync.Api.dll seed   # 50k items + 5k POs
open http://localhost:18084           # Ops console (Swagger: /swagger)
```

The first `docker compose up` pulls SQL Server 2022 and builds both images. The API waits for SQL Server to be healthy, applies all EF Core migrations on startup, and serves the admin UI, Swagger, and `/health`. Host ports: API `18084`, MockErp `18083`, SQL Server `1433`.

### Local development (no Docker)

Requirements: .NET 8 SDK, a reachable SQL Server instance.

```bash
# 1. Provide the connection string via user-secrets (never commit secrets)
dotnet user-secrets init --project src/InventorySync.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=InventorySync;User Id=sa;Password=<your-password>;TrustServerCertificate=True" \
  --project src/InventorySync.Api

# 2. Apply migrations and seed
dotnet ef database update --project src/InventorySync.Infrastructure --startup-project src/InventorySync.Api
dotnet run --project src/InventorySync.Api -- seed

# 3. Run MockErp (one terminal) and the API (another)
dotnet run --project src/MockErp --urls http://localhost:8081
dotnet run --project src/InventorySync.Api
```

Open http://localhost:5xxx (the port printed by the API) for the console. The same
`ConnectionStrings__DefaultConnection` environment variable works instead of user-secrets.

## Configuration

No secrets are committed. All configuration keys (table below) come from `appsettings.json`,
`appsettings.Production.json`, environment variables, or user-secrets — environment variables
win, in the usual ASP.NET Core order.

| Key | Default | Purpose |
| --- | ------- | ------- |
| `ConnectionStrings__DefaultConnection` | — *(required)* | SQL Server connection string (env var / user-secrets only) |
| `Erp__BaseUrl` | `http://localhost:8081` (prod: `http://mock-erp:8080`) | Base URL of the ERP system |
| `Sync__PageSize` | `500` | ERP records processed per batch during a sync |
| `Sync__ConflictResolution` | `NewerWins` | `ErpWins`, `LocalWins` or `NewerWins` |
| `Cors__AllowedOrigins` | dev list (empty = allow any) | Origins allowed by the CORS policy; `*` allows any |
| `RateLimiting__PermitLimit` | `10` | Sync-trigger requests allowed per window |
| `RateLimiting__WindowSeconds` | `60` | Rate-limit window length |
| `Serilog__MinimumLevel` | `Information` | Minimum log level (logs are structured JSON on the console) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` additionally enables Swagger UI |
| `MSSQL_SA_PASSWORD` | (compose local default) | SQL Server `sa` password for the compose stack |

## API reference

Interactive documentation is served at `/swagger/index.html` in Development.

| Method | Route | Purpose |
| ------ | ----- | ------- |
| `GET` | `/api/inventory` | Paged inventory list — `search` (SKU contains), `warehouseCode`, `lastSyncedFrom/To`, `sort`, `page`, `pageSize` (max 200). Output-cached 30 s with ETag. |
| `GET` | `/api/inventory/{id}` | Single inventory item |
| `GET` | `/api/inventory/sku/{sku}` | Item lookup by SKU |
| `POST` | `/api/inventory` | Create an item (FluentValidation, 400 ProblemDetails on failure) |
| `PUT` | `/api/inventory/{id}` | Update an item; supply `rowVersion` for optimistic concurrency (409 on conflict) |
| `DELETE` | `/api/inventory/{id}` | Delete an item (204) |
| `GET` | `/api/purchaseorders` | Paged orders — `search`, `vendorCode`, `status`, `orderDateFrom/To`, `sort`, `includeLines`, `page`, `pageSize`. Output-cached 30 s. |
| `GET` | `/api/purchaseorders/{id}` | Single order including lines |
| `POST` | `/api/purchaseorders` | Create a draft order with lines |
| `POST` | `/api/purchaseorders/{id}/submit` | Draft → Submitted |
| `POST` | `/api/purchaseorders/{id}/lines/{lineId}/receive` | Record stock receipt; auto-transitions the order status |
| `POST` | `/api/purchaseorders/{id}/cancel` | Draft/Submitted → Cancelled |
| `POST` | `/api/sync/inventory` | Trigger a full inventory sync (rate-limited) |
| `POST` | `/api/sync/purchase-orders` | Trigger a full PO sync (rate-limited) |
| `POST` | `/api/sync/runs/{id}/retry` | Re-pull only the failed keys of a run; creates a child `SyncRun` (rate-limited) |
| `GET` | `/api/sync/runs` | Paged runs — `entityType`, `status`, `page`, `pageSize`. Output-cached 30 s. |
| `GET` | `/api/sync/runs/{id}` | Single sync run |
| `GET` | `/api/sync/runs/{id}/audit` | Audit entries — `action` filter, `page`, `pageSize` |
| `GET` | `/health` | Health check (database + ERP reachability) |

Errors are always RFC 9457 `application/problem+json` payloads, produced by a global exception-handling middleware (`404`, `409`, `400`, `429`, `500`).

## How sync works

1. **Paging.** `ErpHttpClient` pulls the ERP in pages of 500, following the `{ items, page, pageSize, totalCount }` envelope until all records are seen. Every page goes through Polly (3 retries with exponential backoff + jitter) and a shared circuit breaker, so a transient 503 is retried and a hard outage fails fast.
2. **Mapping with field errors.** `ErpRecordMapper` converts the ERP wire format — `ITM_SKU`, string quantities, `yyyyMMddHHmmss` dates — into entities using invariant-culture parsing, trimming, and strict decimal rules (no thousands separators). Malformed records produce a list of `FieldError`s instead of throwing.
3. **Natural-key matching.** Records are matched to existing rows by `Sku` (inventory) or `PoNumber` (orders) — not by ERP record id — because the ERP id is unknown until the first sync and legacy systems routinely renumber.
4. **Field-level diffing.** Each business field is compared; every change writes one `SyncAuditEntry` with `FieldName`, `OldValue`, `NewValue`, and the action (`Insert`, `Update`, `Skip`, `ConflictResolved`, `Error`).
5. **Conflict resolution.** A row whose `LocallyModifiedUtc` is set after its last sync is in conflict. The strategy from `Sync__ConflictResolution` decides: `ErpWins` overwrites local edits, `LocalWins` keeps them, and `NewerWins` compares the local modification time with the ERP modification time. Conflicts are recorded as `ConflictResolved` audit entries with the discarded value preserved. Purchase-order statuses additionally never regress unless the strategy explicitly overrides them.
6. **Error isolation.** A bad record increments `RecordsFailed`, writes an `Error` audit entry, and the run continues. A run with failures completes as `PartialSuccess`.
7. **Retry.** `POST /api/sync/runs/{id}/retry` collects the `Error` entries with usable keys, re-pulls only those keys from the ERP, and runs them as a new child `SyncRun` (`ParentSyncRunId` links the two).

## Ops console

`GET /` serves a no-build, no-framework admin UI (`src/InventorySync.Api/wwwroot`, ES6 modules + CSS custom properties, dark mode via `prefers-color-scheme`):

- **Inventory** — server-paged table, debounced SKU search, warehouse filter, sortable columns, inline editing of quantity/unit cost with optimistic UI and rollback on failure, delete with confirmation dialog.
- **Orders** — status filter chips, expandable rows that show `PurchaseOrderLine`s.
- **Sync runs** — status badges, duration, per-run counts; "Run inventory sync" / "Run PO sync" buttons disable while a run is in flight and poll every 2 s until it leaves `Running`.
- **Run detail** — timeline, audit table filterable by action, `del`/`ins` diffs for Update entries, and a "Retry failed records" button when `recordsFailed > 0`.

Terminal capture — runs list:

```json
$ curl -s "http://localhost:18084/api/sync/runs?pageSize=5"
{
  "items": [
    { "id": 14, "parentSyncRunId": 7, "entityType": 0, "status": 1,
      "recordsRead": 600, "recordsInserted": 0, "recordsUpdated": 0,
      "recordsFailed": 0, "triggeredBy": "fault-demo" },
    { "id": 13, "parentSyncRunId": null, "entityType": 0, "status": 1,
      "recordsRead": 12000, "recordsInserted": 0, "recordsUpdated": 1,
      "recordsFailed": 0, "triggeredBy": "admin-ui-verify" },
    { "id": 12, "parentSyncRunId": null, "entityType": 1, "status": 1,
      "recordsRead": 2500, "recordsInserted": 0, "recordsUpdated": 1,
      "recordsFailed": 0, "triggeredBy": "fail-rate-demo-recovered" },
    { "id": 11, "parentSyncRunId": null, "entityType": 1, "status": 2,
      "recordsRead": 0, "recordsFailed": 0, "triggeredBy": "fail-rate-demo-recovered" }
  ]
}
```

Terminal capture — run detail, Update entries rendered as diffs in the console:

```text
$ curl -s "http://localhost:18084/api/sync/runs/3/audit?action=1&pageSize=4"
total Update entries: 31901

    SKU-040001  QuantityOnHand                974          -> 4961
    SKU-040001  UnitCost                      269.3044     -> 122.2023
    SKU-040001  WarehouseCode                 WH06         -> WH03
    SKU-040002  Description                   Description for SKU-040002 -> (null)
```

In the UI the same data renders as `<del>974</del> → <ins>4961</ins>`.

## SQL performance

The reporting workload was profiled against the seeded dataset (52k items, 5.5k orders, 168k audit entries); results are reproducible with `docs/queries/measure.sh`. Highlights:

| Query | Technique | Logical reads | Elapsed (median) |
| ----- | --------- | ------------- | ---------------- |
| Low-stock items by warehouse | filtered index | 1,473 → 3 (−99.8%) | 53 ms → ~1 ms |
| Open POs per SKU (correlated subquery → join + `RANK()` window) | rewrite + filtered index | 380 → 296 (−22%) | 136 ms → ~33 ms |
| Sync failures by day (SARGability) | half-open range + `(Action, TimestampUtc)` index | 2,636 → 8 (−99.7%) | 30–166 ms → ~1–2 ms |
| Valuation by warehouse | covering index | 1,473 → 232 (−84%) | 65 ms → ~23 ms |
| EF Core N+1 order lines | `.Include` projection (`includeLines=true`) | 21 → 1 query | — |

Full write-up with plans, explanations, and the filtered-index `QUOTED_IDENTIFIER` / two-column-predicate gotchas: [docs/sql-performance.md](docs/sql-performance.md).

## Tech stack

| Layer | Technology |
| ----- | ---------- |
| API | ASP.NET Core 8 (controllers), FluentValidation, Swagger/OpenAPI, output caching, response compression, rate limiting, health checks |
| Data | Entity Framework Core 8 (SQL Server provider), code-first migrations |
| Sync | Polly (retry + circuit breaker), typed `HttpClient`, SOAP/XML via `XDocument` |
| Observability | Serilog (structured JSON console logs), `X-Correlation-Id` correlation middleware |
| Mock ERP | Standalone ASP.NET Core app with REST + SOAP endpoints and fault injection |
| UI | Vanilla ES6+ modules, HTML5, CSS custom properties — no framework, no build step |
| Tests | xUnit, FluentAssertions, Moq, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers (real SQL Server), Coverlet |
| CI | GitHub Actions (build/test/coverage gate, `dotnet format`, ESLint, Docker builds), Dependabot |

## Project layout

```text
.
├── .github/                      # CI workflow, dependabot, PR/issue templates, coverage gate script
├── docs/
│   ├── adr/                      # Architecture decision records
│   ├── queries/                  # Reporting queries + reproducible measure.sh
│   └── sql-performance.md        # Before/after tuning study
├── src/
│   ├── InventorySync.Api/        # Web API, controllers, middleware, validators, seeder
│   │   └── wwwroot/              # Ops console (js/ css/ index.html)
│   ├── InventorySync.Core/       # Entities, DTOs, enums, options, interfaces
│   ├── InventorySync.Infrastructure/
│   │   ├── Data/                 # SyncDbContext, configurations, migrations
│   │   ├── Erp/                  # ErpHttpClient, mapper, Polly policies, seed generator
│   │   ├── Repositories/
│   │   └── Services/             # SyncService (sync engine), CRUD services
│   └── MockErp/                  # Legacy-ERP simulator (REST + SOAP + faults)
├── tests/InventorySync.Tests/    # Unit + integration + Testcontainers suites
├── Dockerfile                    # API image (non-root, HEALTHCHECK)
├── docker-compose.yml            # SQL Server + MockErp + API
├── eslint.config.mjs             # Flat ESLint config for the admin UI
└── Directory.Build.props         # Nullable, TreatWarningsAsErrors, XML docs
```

## Decisions, contributing, and releases

- Architecture decisions: [docs/adr/](docs/adr/) — EF Core over Dapper, natural-key upsert, vanilla JS over a SPA framework.
- Contributing: [CONTRIBUTING.md](CONTRIBUTING.md) (branch naming, Conventional Commits, PR checklist, running tests).
- SQL tuning study: [docs/sql-performance.md](docs/sql-performance.md).
