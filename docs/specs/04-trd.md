# Stage 4 — Technical Requirements Document (TRD)

**Product:** StockPulse — Intelligent Inventory Dashboard
**Scenario:** CBTW Technical Assessment — Scenario B
**Upstream:** `03-prd.md` · **Downstream:** Implementation & Test (Stage 5)
**Status:** Refined / Baselined (Refine gate passed — proposals accepted)
**Companion:** `docs/System_Design_Document.md` (architecture narrative + diagrams)

> The PRD says *what* and *why*. This TRD says *how*: the technology choices, layer boundaries, data model, API contract, algorithms, and cross-cutting concerns that realize each requirement. Every section traces to FR/NFR IDs from `02-requirements.md`.

---

## 1. Architecture Overview

Clean Architecture, four projects, dependencies pointing inward only:

```
StockPulse.API           → controllers, middleware, composition root, observability wiring
StockPulse.Application    → MediatR handlers, validators, DTOs, pipeline behaviors  (no infra deps)
StockPulse.Infrastructure → EF Core, repositories, Hangfire job, DI wiring
StockPulse.Domain         → entities, enums, constants, repository interfaces (no deps)

        API ──▶ Application ──▶ Domain ◀── Infrastructure
```

- **Why Clean Architecture:** satisfies **NFR-10** (inward dependencies) and **NFR-9** (business logic testable without infrastructure). The Application layer references only `Domain` + abstractions, so handlers unit-test against mocked repositories.
- **Why CQRS via MediatR:** each use case is a discrete `IRequest`/`IRequestHandler`. Cross-cutting concerns (validation, logging) attach as **pipeline behaviors** rather than handler code.

---

## 2. Technology Stack & Justifications

| Concern | Technology | Version | Justification | Trace |
|---|---|---|---|---|
| Runtime / API | .NET / ASP.NET Core | **net10.0** | CBTW-aligned stack; minimal hosting, first-class DI, fast. | — |
| Mediation / CQRS | MediatR | 12.x | Clean handler-per-use-case; pipeline behaviors for cross-cutting. | NFR-9, NFR-10 |
| Validation | FluentValidation | 11.x | Declarative, testable rules as a pipeline behavior; handlers assume valid input. | FR-8, FR-17, NFR-12 |
| ORM / migrations | EF Core | 9.x | Code-first migrations, type-safe LINQ, `ExecuteUpdateAsync` for set-based stamping. | FR-24, NFR-1 |
| Database | SQL Server | 2022 (Docker) | Relational fit; Docker path for cross-platform run. LocalDB optional on Windows. | NFR-11 |
| Background jobs | Hangfire | 1.8.x | Persistent, retrying scheduler with dashboard; SQL-backed survives restarts. | FR-11, FR-13, NFR-3 |
| Logging | Serilog | 9.x | Structured JSON; CorrelationId enrichment; Console + File + Seq sinks. | NFR-5 |
| Metrics | OpenTelemetry + Prometheus | 1.x | Vendor-neutral metrics; `/metrics` scrape → Prometheus → Grafana. | NFR-6 |
| Tracing | OpenTelemetry + OTLP | 1.x | ASP.NET Core + HttpClient spans → OTLP → Jaeger. | NFR-7 |
| Health | AspNetCore.HealthChecks.SqlServer | 9.x | `/health` reflects DB connectivity. | NFR-8 |
| API docs / mock client | Swashbuckle (Swagger) | 6.x | OpenAPI + interactive UI = the mocked client. | C-1 |
| Tests | xUnit + Moq | latest | Dominant .NET stack; fast mock-based handler tests. | NFR-9 |

---

## 3. Domain Model

### 3.1 Entities

**Vehicle** *(aggregate root for inventory)*
```
Id            : Guid (PK)
VIN           : string(17), unique, required
Make          : string(100), required, indexed
Model         : string(100), required, indexed
Year          : int, indexed
Colour        : string(50), required
Price         : decimal(18,2)
Status        : enum→string(20)   // VehicleStatus
ArrivedAt     : DateTime (UTC), indexed   // aging is measured from here
IsAging       : bool, indexed              // stamped by the job
DealershipId  : Guid, indexed              // tenant scope
ActionLogs    : ICollection<VehicleActionLog>
```

**VehicleActionLog** *(append-only event)*
```
Id          : Guid (PK)
VehicleId   : Guid (FK → Vehicle), indexed
ActionType  : enum→string(50)   // VehicleActionType
LoggedAt    : DateTime (UTC)    // server-set at creation
```

### 3.2 Enums
- `VehicleStatus` — vehicle lifecycle status (stored as string for readability/stability).
- `VehicleActionType` — controlled vocabulary: `PriceReductionPlanned`, `TransferRequested`, `DiscountApplied`, `WriteOffScheduled`, `NoActionRequired`. → **FR-17**

### 3.3 Constants
- `AgingConstants.AgingThresholdDays = 90` — single source of truth for the aging rule. → **FR-10**

### 3.4 Indexing strategy *(NFR-1)*
Indexes on `VIN (unique)`, `Make`, `Model`, `Year`, `IsAging`, `DealershipId`, `ArrivedAt`. The hot path — filter by dealership + (make/model/year/isAging), order by `ArrivedAt` — is index-supported; the aging count is an indexed boolean count.

### 3.5 Enum persistence
Both enums persist as **strings** via `HasConversion<string>()`. Rationale: human-readable rows, resilient to enum reordering (vs. ordinal ints). Trade-off: slightly larger columns — acceptable.

---

## 4. Application Layer Design

### 4.1 Use cases (MediatR)

| Use case | Type | Handler responsibility | Trace |
|---|---|---|---|
| `GetInventoryQuery` | Query | Page+filter via repo; attach whole-dealership aging count; map to DTOs. | FR-1…FR-6, FR-14 |
| `GetVehicleByIdQuery` | Query | Fetch one vehicle → DTO or null. | FR-7 |
| `LogVehicleActionCommand` | Command | Verify vehicle exists (else `NotFoundException`); append timestamped log; save. | FR-16, FR-18, FR-21 |
| `GetVehicleActionsQuery` | Query | Return chronological action history for a vehicle. | FR-20 |

### 4.2 Pipeline behaviors
- **ValidationBehavior** — runs FluentValidation validators before the handler; aggregates failures into a validation error → `400`. → **FR-8, NFR-12**
- **LoggingBehavior** — logs request name, elapsed time, outcome (Serilog + correlation). → **NFR-5**

### 4.3 Validators
- `GetInventoryQueryValidator` — `Page ≥ 1`, `PageSize ∈ [1,100]`. → **FR-8, NFR-2**
- `LogVehicleActionCommandValidator` — `ActionType` must be a defined enum value. → **FR-17**

### 4.4 Tenant scoping
`DealershipId` is injected via `IOptions<InventorySettings>` (bound from configuration at composition root) — the Application layer never reads `IConfiguration` directly, preserving **NFR-10**. Every query is scoped by it. → **FR-23**

### 4.5 DTO boundary
Handlers return DTOs (`VehicleDto`, `VehicleActionLogDto`, `InventoryResponse`), never entities — the persistence model never leaks across the API boundary.

---

## 5. Infrastructure Layer Design

### 5.1 Persistence
- `InventoryDbContext` — `DbSet<Vehicle>`, `DbSet<VehicleActionLog>`; Fluent config for keys, indexes, lengths, enum→string, FK. → **FR-24, NFR-1**
- **Repositories** implement Domain interfaces:
  - `VehicleRepository` — `GetPagedAsync` (filter+page), `GetByIdAsync`, `CountAgingAsync`, `AddRangeAsync`, `ExistsAsync`.
  - `VehicleActionLogRepository` — `AddAsync`, `GetByVehicleIdAsync` (chronological, explicit `OrderBy(LoggedAt)`). **Create + read only — no update/delete path exists.** → **FR-19, FR-20**
- `UnitOfWork` — wraps `SaveChangesAsync`, decoupling handlers from `DbContext`.

### 5.2 Filtering semantics
`make`/`model` matched case-insensitively (`ToLower()` comparison); `year`/`isAging` exact. Filters compose with logical AND. Results ordered by `ArrivedAt` ascending (oldest first — aging-relevant ordering). → **FR-2…FR-6**

### 5.3 Aging stamp job *(FR-9, FR-11, FR-15, NFR-3)*
```
threshold = UtcNow - AgingThresholdDays
count = Vehicles.Where(ArrivedAt < threshold && !IsAging)
                .ExecuteUpdateAsync(IsAging = true)
emit metric vehicles.aged.stamped.total += count
log "Aging stamp complete. {count} vehicles flagged."
```
- **Set-based update** (`ExecuteUpdateAsync`) — single SQL `UPDATE`, no entity tracking; scales to large inventories.
- **Idempotent** — the `!IsAging` predicate means a re-run stamps zero. → **FR-15**
- **Scheduling** — registered as a Hangfire recurring job; cron from `AgingJob:CronExpression` (default `0 2 * * *`). → **FR-11, FR-12**
- **Manual trigger** — `AdminController` enqueues `AgingStampJob` via `IBackgroundJobClient`. → **FR-13**

### 5.4 Hangfire job registration ordering *(NFR-4)*
Recurring jobs are registered **after** the app is built, using the DI-resolved `IRecurringJobManager` (service-based API), not the static `RecurringJob` facade — the static API requires `JobStorage.Current` which isn't initialized at config time and throws on a cold start. This is a known correctness fix baked into the design.

---

## 6. API Layer Design

### 6.1 Endpoint contract

| Method | Path | Success | Errors | Trace |
|---|---|---|---|---|
| GET | `/api/inventory` | `200` `InventoryResponse` | `400` | FR-1…FR-6, FR-8, FR-14 |
| GET | `/api/inventory/{id}` | `200` `VehicleDto` | `404` | FR-7 |
| GET | `/api/inventory/{id}/actions` | `200` `VehicleActionLogDto[]` | `404` | FR-20 |
| POST | `/api/inventory/{id}/actions` | `201` `VehicleActionLogDto` | `400`, `404` | FR-16…FR-19, FR-21 |
| POST | `/api/admin/seed` | `201` `{seeded}` | `400` | FR-22 |
| POST | `/api/admin/jobs/trigger-aging-stamp` | `202` `{jobId}` | — | FR-13 |
| GET | `/health` | `200` JSON status | `503` | NFR-8 |
| GET | `/metrics` | `200` Prometheus text | — | NFR-6 |
| GET | `/hangfire` | dashboard | — | FR-11 |
| GET | `/swagger` | UI | — | C-1 |

### 6.2 Request/response conventions
- Query params for filtering+paging; `page=1`, `pageSize=20` defaults.
- Enums serialized as **strings** (`JsonStringEnumConverter`) for readable contracts.
- `InventoryResponse`: `{ items[], pageNumber, pageSize, totalCount, totalPages, agingStockCount }` — `agingStockCount` always whole-dealership. → **FR-14**
- Controllers are thin: build the MediatR request, send, translate result to `IActionResult`. No business logic in controllers.

### 6.3 Middleware pipeline (order)
1. `CorrelationIdMiddleware` — read/generate `X-Correlation-ID`, push to Serilog context. → **NFR-5**
2. `ExceptionHandlingMiddleware` — map `ValidationException → 400`, `NotFoundException → 404`, unhandled → `500`; never leak stack traces. → **FR-21, NFR-12**
3. `UseSerilogRequestLogging` — structured request log.

---

## 7. Cross-Cutting / Observability *(NFR-5…NFR-8)*

| Signal | Implementation | Surface |
|---|---|---|
| Logs | Serilog, JSON, CorrelationId enrichment | Console + rolling File + Seq (`:5341`) |
| Metrics | OpenTelemetry meter `StockPulse` + ASP.NET instrumentation + Prometheus exporter | `/metrics` → Prometheus (`:9090`) → Grafana (`:3000`) |
| Traces | OpenTelemetry ASP.NET Core + HttpClient → OTLP exporter | OTLP `:4317` → Jaeger (`:16686`) |
| Health | SQL Server health check | `/health` |
| Jobs | Hangfire dashboard | `/hangfire` |

**Custom metrics:** `vehicles.aged.stamped.total` (counter, per job run); HTTP request duration (instrumentation). Config: `OpenTelemetry:OtlpEndpoint` (default `http://localhost:4317`).

---

## 8. Configuration

| Key | Default | Purpose | Trace |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | Docker SQL (`localhost,1433`) in Dev | DB connection | NFR-11 |
| `DealershipId` | `11111111-…` | Tenant scope | FR-23 |
| `AgingJob:CronExpression` | `0 2 * * *` | Job schedule (no recompile) | FR-12 |
| `OpenTelemetry:OtlpEndpoint` | `http://localhost:4317` | Trace export target | NFR-7 |
| `Serilog:WriteTo[Seq]` | `http://localhost:5341` | Log sink (Dev) | NFR-5 |

Startup: EF Core `Database.Migrate()` runs on boot (**FR-24, NFR-4**); Hangfire recurring job registered post-build (**§5.4**).

---

## 9. Non-Functional Realization

| NFR | How realized |
|---|---|
| NFR-1 Performance | Indexed filter columns; set-based job update; indexed aging count. |
| NFR-2 Scalability | Mandatory pagination; `pageSize` capped at 100 by validator. |
| NFR-3 Reliability | Hangfire SQL-backed persistence + automatic retry/backoff. |
| NFR-4 Cold-start | Auto-migrate on boot; idempotent job; service-based job registration (no `JobStorage.Current` dependency at config time). |
| NFR-5 Logs | Serilog structured + CorrelationId + Seq. |
| NFR-6 Metrics | OTel + Prometheus `/metrics`. |
| NFR-7 Traces | OTel OTLP → Jaeger. |
| NFR-8 Health | `/health` SQL check. |
| NFR-9 Testability | Mock-based handler unit tests, no infra. |
| NFR-10 Dependencies | Application depends only on Domain + abstractions; `IOptions` not `IConfiguration`. |
| NFR-11 Portability | Docker SQL Server; documented non-Windows run. |
| NFR-12 Validation | FluentValidation pipeline behavior + exception mapping. |

---

## 10. Test Strategy (feeds Stage 5)

**Scope:** Application-layer business logic — the core per the brief — via xUnit + Moq, no infrastructure.

| Test target | Cases | Trace |
|---|---|---|
| `GetInventoryQueryHandler` | filters applied; paging metadata; aging count is whole-dealership not filtered | FR-1…FR-6, FR-14 |
| `GetVehicleByIdQueryHandler` | found → DTO; missing → null | FR-7 |
| `LogVehicleActionCommandHandler` | valid → persisted + timestamped; missing vehicle → `NotFoundException` | FR-16, FR-18, FR-21 |
| `GetVehicleActionsQueryHandler` | chronological order; empty history | FR-20 |

**Boundary cases to assert:** aging threshold at 89 vs 91 days (where exercised through handlers), `pageSize` bounds, unknown action type rejection. **Exit criterion:** `dotnet test` green; all four handlers + edge cases covered.

---

## 11. Resolved at Refine Gate

1. **Case-insensitive filtering via `ToLower()` — RESOLVED:** Kept for M1 (acceptable at assessment scale). Production path = case-insensitive **collation** on `Make`/`Model` to stay SARGable. Recorded as a future enhancement.
2. **`GetByVehicleIdAsync` chronological ordering — RESOLVED:** Verified already explicit `OrderBy(l => l.LoggedAt)`. No change needed.
3. **Metric cardinality — RESOLVED:** Custom metrics stay label-free (no per-vehicle labels) to avoid cardinality blow-up.
