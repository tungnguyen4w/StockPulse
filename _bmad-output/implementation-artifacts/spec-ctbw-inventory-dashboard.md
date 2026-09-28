---
title: 'CTBW Intelligent Inventory Dashboard — Backend API'
type: 'feature'
created: '2026-06-03'
status: 'in-review'
baseline_commit: 'NO_VCS'
context:
  - '{project-root}/docs/System_Design_Document.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CTBW Technical Assessment Scenario B requires a backend API for a dealership inventory dashboard — no such system exists yet. The solution must support filterable vehicle inventory, automated aging stock detection, and an append-only action audit log.

**Approach:** Build a .NET 10 Clean Architecture REST API (Domain / Application / Infrastructure / API / Tests) using MediatR CQRS, EF Core + SQL Server LocalDB, Hangfire scheduled background job, Serilog + OpenTelemetry observability, and Swagger UI as the client stub. The existing `StockPulse.API` project is the starting point.

## Boundaries & Constraints

**Always:**
- Clean Architecture dependency rule: Infrastructure and API depend on Application; Application depends on Domain; Domain has no outward dependencies.
- `VehicleActionLog` is append-only — no UPDATE or DELETE ever executed against that table.
- All queries scoped by `DealershipId` loaded from `appsettings.json`.
- Every paginated `InventoryResponse` includes `AgingStockCount`.
- Hangfire cron expression loaded from `appsettings.json` key `AgingJob:CronExpression`.
- `[AllowAnonymous]` on all controllers — auth is explicitly out of scope.
- Target framework: `net10.0` (matches existing project).

**Ask First:**
- If EF Core migration conflicts arise with the existing `StockPulse.API` project structure.
- If SQL Server LocalDB is unavailable on the build machine (suggest SQLite fallback).

**Never:**
- No frontend implementation.
- No JWT/auth middleware.
- No UPDATE or DELETE on `VehicleActionLogs`.
- No client-side pagination or full-table scans on list endpoints.
- No hardcoded connection strings — always `appsettings.json`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Inventory list — filtered | `GET /api/inventory?make=Toyota&isAging=true&page=1&pageSize=20` | 200 + paged items + `agingStockCount` | 400 if `pageSize > 100` |
| Inventory list — empty | No vehicles seeded | 200 + `{ items: [], totalCount: 0, agingStockCount: 0 }` | N/A |
| Vehicle not found | `GET /api/inventory/{unknown-guid}` | 404 ProblemDetails | N/A |
| Log action — valid | `POST /api/inventory/{id}/actions` with valid enum | 201 + log entry | N/A |
| Log action — invalid enum | Body `{ "actionType": "InvalidValue" }` | 400 ProblemDetails with validation errors | FluentValidation |
| Log action — vehicle not found | Valid enum, unknown vehicleId | 404 ProblemDetails | N/A |
| Aging job — first run | Vehicles with `ArrivedAt < NOW - 90d` exist, `IsAging = false` | `IsAging` set to `true`, count logged via Serilog + OTel counter | Job retries on exception |
| Manual trigger | `POST /api/admin/jobs/trigger-aging-stamp` | 202 + `{ jobId: "..." }` | N/A |
| Health check — healthy | DB reachable, Hangfire storage accessible | 200 `{ status: "Healthy" }` | N/A |
| Health check — DB down | SQL Server unavailable | 503 `{ status: "Unhealthy" }` | N/A |

</frozen-after-approval>

## Code Map

- `StockPulse.slnx` -- solution file; add all new projects here
- `StockPulse.API/` -- existing Web API host; restructure, add packages, wire DI
- `StockPulse.Domain/` -- CREATE: entities (Vehicle, VehicleActionLog), enums, repository interfaces
- `StockPulse.Application/` -- CREATE: MediatR queries/commands/handlers, validators, pipeline behaviors, DTOs
- `StockPulse.Infrastructure/` -- CREATE: EF Core DbContext, repositories, Hangfire job, migrations
- `StockPulse.Tests/` -- CREATE: xUnit + Moq unit tests for all handlers

## Tasks & Acceptance

**Execution:**

### Phase A — Solution Scaffolding

- [ ] `StockPulse.slnx` -- add Domain, Application, Infrastructure, Tests projects to solution
- [ ] `StockPulse.Domain/StockPulse.Domain.csproj` -- CREATE: class library, net10.0, no external dependencies
- [ ] `StockPulse.Application/StockPulse.Application.csproj` -- CREATE: class library, net10.0; ref Domain; add MediatR, FluentValidation.DependencyInjectionExtensions
- [ ] `StockPulse.Infrastructure/StockPulse.Infrastructure.csproj` -- CREATE: class library, net10.0; ref Application; add EF Core SqlServer, Hangfire.SqlServer, Hangfire.AspNetCore
- [ ] `StockPulse.Tests/StockPulse.Tests.csproj` -- CREATE: xUnit test project, net10.0; ref Application; add Moq, FluentAssertions
- [ ] `StockPulse.API/StockPulse.API.csproj` -- remove default OpenApi package; add ref to Application + Infrastructure; add Serilog.AspNetCore, OpenTelemetry packages, Swashbuckle.AspNetCore, Microsoft.AspNetCore.Diagnostics.HealthChecks

### Phase B — Domain Layer

- [ ] `StockPulse.Domain/Entities/Vehicle.cs` -- CREATE: 11-field entity (Id Guid, VIN, Make, Model, Year, Colour, Price decimal, Status VehicleStatus, ArrivedAt DateTime, IsAging bool, DealershipId Guid)
- [ ] `StockPulse.Domain/Entities/VehicleActionLog.cs` -- CREATE: Id Guid, VehicleId Guid, ActionType VehicleActionType, LoggedAt DateTime
- [ ] `StockPulse.Domain/Enums/VehicleStatus.cs` -- CREATE: Available, Reserved, Sold
- [ ] `StockPulse.Domain/Enums/VehicleActionType.cs` -- CREATE: PriceReductionPlanned, TransferRequested, DiscountApplied, WriteOffScheduled, NoActionRequired
- [ ] `StockPulse.Domain/Constants/AgingConstants.cs` -- CREATE: `AgingThresholdDays = 90`
- [ ] `StockPulse.Domain/Interfaces/IVehicleRepository.cs` -- CREATE: GetPagedAsync, GetByIdAsync, CountAgingAsync, AddRangeAsync, ExistsAsync
- [ ] `StockPulse.Domain/Interfaces/IVehicleActionLogRepository.cs` -- CREATE: GetByVehicleIdAsync, AddAsync
- [ ] `StockPulse.Domain/Interfaces/IUnitOfWork.cs` -- CREATE: SaveChangesAsync

### Phase C — Application Layer

- [ ] `StockPulse.Application/Common/PagedResult.cs` -- CREATE: generic `PagedResult<T>` with Items, PageNumber, PageSize, TotalCount
- [ ] `StockPulse.Application/Common/Behaviors/LoggingBehavior.cs` -- CREATE: IPipelineBehavior; log request name + elapsed via ILogger
- [ ] `StockPulse.Application/Common/Behaviors/ValidationBehavior.cs` -- CREATE: IPipelineBehavior; run FluentValidation validators; throw ValidationException on failure
- [ ] `StockPulse.Application/Features/Inventory/Queries/GetInventory/GetInventoryQuery.cs` -- CREATE: IRequest<InventoryResponse>; properties: Make?, Model?, Year?, IsAging?, Page, PageSize
- [ ] `StockPulse.Application/Features/Inventory/Queries/GetInventory/InventoryResponse.cs` -- CREATE: Items (List<VehicleDto>), PageNumber, PageSize, TotalCount, AgingStockCount
- [ ] `StockPulse.Application/Features/Inventory/Queries/GetInventory/VehicleDto.cs` -- CREATE: all Vehicle fields as DTO
- [ ] `StockPulse.Application/Features/Inventory/Queries/GetInventory/GetInventoryQueryHandler.cs` -- CREATE: calls IVehicleRepository.GetPagedAsync + CountAgingAsync; maps to InventoryResponse
- [ ] `StockPulse.Application/Features/Inventory/Queries/GetInventory/GetInventoryQueryValidator.cs` -- CREATE: PageSize 1-100, Page >= 1
- [ ] `StockPulse.Application/Features/Inventory/Queries/GetVehicleById/GetVehicleByIdQuery.cs` -- CREATE: IRequest<VehicleDto?>; property: Id Guid
- [ ] `StockPulse.Application/Features/Inventory/Queries/GetVehicleById/GetVehicleByIdQueryHandler.cs` -- CREATE: calls IVehicleRepository.GetByIdAsync; returns null if not found
- [ ] `StockPulse.Application/Features/Actions/Queries/GetVehicleActions/GetVehicleActionsQuery.cs` -- CREATE: IRequest<List<VehicleActionLogDto>>; property: VehicleId Guid
- [ ] `StockPulse.Application/Features/Actions/Queries/GetVehicleActions/VehicleActionLogDto.cs` -- CREATE: Id, VehicleId, ActionType, LoggedAt
- [ ] `StockPulse.Application/Features/Actions/Queries/GetVehicleActions/GetVehicleActionsQueryHandler.cs` -- CREATE: calls IVehicleActionLogRepository.GetByVehicleIdAsync
- [ ] `StockPulse.Application/Features/Actions/Commands/LogVehicleAction/LogVehicleActionCommand.cs` -- CREATE: IRequest<VehicleActionLogDto>; VehicleId Guid, ActionType VehicleActionType
- [ ] `StockPulse.Application/Features/Actions/Commands/LogVehicleAction/LogVehicleActionCommandHandler.cs` -- CREATE: checks vehicle exists; creates VehicleActionLog; calls AddAsync + SaveChangesAsync; returns dto
- [ ] `StockPulse.Application/Features/Actions/Commands/LogVehicleAction/LogVehicleActionCommandValidator.cs` -- CREATE: VehicleId not empty, ActionType valid enum
- [ ] `StockPulse.Application/DependencyInjection.cs` -- CREATE: IServiceCollection extension; registers MediatR, FluentValidation, pipeline behaviors

### Phase D — Infrastructure Layer

- [ ] `StockPulse.Infrastructure/Persistence/InventoryDbContext.cs` -- CREATE: DbContext with Vehicles + VehicleActionLogs DbSets; configure indexes (IsAging, Make, Model, Year, DealershipId); VIN unique constraint; enums stored as strings
- [ ] `StockPulse.Infrastructure/Persistence/Repositories/VehicleRepository.cs` -- CREATE: implements IVehicleRepository; GetPagedAsync applies filters + OFFSET/FETCH; CountAgingAsync is COUNT WHERE IsAging=true AND DealershipId=x
- [ ] `StockPulse.Infrastructure/Persistence/Repositories/VehicleActionLogRepository.cs` -- CREATE: implements IVehicleActionLogRepository; GetByVehicleIdAsync orders by LoggedAt ASC; AddAsync INSERT only
- [ ] `StockPulse.Infrastructure/Persistence/UnitOfWork.cs` -- CREATE: implements IUnitOfWork; wraps DbContext.SaveChangesAsync
- [ ] `StockPulse.Infrastructure/Jobs/AgingStampJob.cs` -- CREATE: queries Vehicles WHERE ArrivedAt < UTC_NOW-90d AND IsAging=false; bulk-sets IsAging=true; logs count via ILogger + OTel counter `vehicles.aged.stamped.total`
- [ ] `StockPulse.Infrastructure/Migrations/` -- CREATE: initial EF Core migration via `dotnet ef migrations add InitialCreate`
- [ ] `StockPulse.Infrastructure/DependencyInjection.cs` -- CREATE: registers DbContext, repositories, UnitOfWork, Hangfire, recurring job

### Phase E — API Layer

- [ ] `StockPulse.API/Controllers/InventoryController.cs` -- CREATE: GET /api/inventory (binds query → GetInventoryQuery), GET /api/inventory/{id} (→ GetVehicleByIdQuery, 404 if null); [AllowAnonymous]
- [ ] `StockPulse.API/Controllers/ActionLogController.cs` -- CREATE: GET /api/inventory/{id}/actions, POST /api/inventory/{id}/actions (→ LogVehicleActionCommand, 201); [AllowAnonymous]
- [ ] `StockPulse.API/Controllers/AdminController.cs` -- CREATE: POST /api/admin/jobs/trigger-aging-stamp (→ IBackgroundJobClient.Enqueue, 202 + jobId); POST /api/admin/seed (accepts List<CreateVehicleDto>, bulk inserts, 201); [AllowAnonymous]
- [ ] `StockPulse.API/Middleware/CorrelationIdMiddleware.cs` -- CREATE: generates/propagates X-Correlation-ID; enriches Serilog LogContext
- [ ] `StockPulse.API/Middleware/ExceptionHandlingMiddleware.cs` -- CREATE: catches unhandled exceptions + ValidationException; returns RFC 7807 ProblemDetails; logs via ILogger
- [ ] `StockPulse.API/Program.cs` -- REWRITE: wire Serilog, OpenTelemetry (http.request.duration + custom meters), Health Checks (DB + Hangfire), Swagger, MediatR DI, Infrastructure DI, middleware pipeline, Hangfire dashboard, /metrics endpoint
- [ ] `StockPulse.API/appsettings.json` -- REWRITE: add ConnectionStrings:DefaultConnection (LocalDB), DealershipId, AgingJob:CronExpression, Serilog sinks config, OpenTelemetry config

### Phase F — Tests

- [ ] `StockPulse.Tests/Features/Inventory/GetInventoryQueryHandlerTests.cs` -- CREATE: tests pagination math, make/model/year/isAging filter pass-through, AgingStockCount included in response
- [ ] `StockPulse.Tests/Features/Inventory/GetVehicleByIdQueryHandlerTests.cs` -- CREATE: returns dto when found; returns null when not found
- [ ] `StockPulse.Tests/Features/Actions/GetVehicleActionsQueryHandlerTests.cs` -- CREATE: returns ordered list; returns empty list when no actions
- [ ] `StockPulse.Tests/Features/Actions/LogVehicleActionCommandHandlerTests.cs` -- CREATE: happy path creates log entry; vehicle not found returns NotFoundException; action log is appended not replaced
- [ ] `StockPulse.Tests/Jobs/AgingStampJobTests.cs` -- CREATE: stamps vehicles older than 90 days; skips already-stamped; logs correct count

### Phase G — Documentation

- [ ] `README.md` -- CREATE at project root: build/run instructions, 10-step golden path walkthrough, AI Collaboration Narrative section

**Acceptance Criteria:**

- Given a seeded database, when `GET /api/inventory?isAging=true` is called, then response includes only vehicles with `IsAging=true` and `agingStockCount` reflects the total aging vehicles for the dealership.
- Given a vehicle exists, when `POST /api/inventory/{id}/actions` with a valid `actionType`, then 201 is returned and the action appears in subsequent `GET /api/inventory/{id}/actions` responses in chronological order.
- Given `POST /api/admin/jobs/trigger-aging-stamp` is called after seeding vehicles with `ArrivedAt` > 90 days ago, then those vehicles have `IsAging=true` within one job execution.
- Given the application is running, when `GET /health` is called, then `{ "status": "Healthy" }` is returned with DB and Hangfire component statuses.
- Given invalid input (pageSize=200 or unknown actionType), when the endpoint is called, then 400 ProblemDetails is returned with structured validation errors.
- All handler unit tests pass with `dotnet test`.
- `dotnet build` succeeds with zero warnings treated as errors.

## Design Notes

**Append-only audit log:** `VehicleActionLogRepository.AddAsync` is the only write path — no Update/Delete methods exist on the interface. This enforces the invariant at the interface boundary, not just by convention.

**AgingStockCount in every response:** `CountAgingAsync` executes a `COUNT` on the indexed `IsAging` column — O(1) with the index, not a table scan. Always returned even when filtering by `isAging=false` so consumers have a dashboard summary without a second request.

**Hangfire on SQL Server:** Hangfire creates its own schema (`HangFire.*`) in the same database. The `InventoryDbContext` migration and Hangfire schema coexist without conflict. Hangfire storage initialised via `UseSqlServerStorage` in `Program.cs`.

**OpenTelemetry without collector:** Metrics are registered via `System.Diagnostics.Metrics.Meter`. Without an OTLP collector, enable the Prometheus exporter (`UsePrometheusExporter`) to expose `/metrics` for local inspection.

## Verification

**Commands:**
- `dotnet build StockPulse.slnx` -- expected: Build succeeded, 0 Error(s)
- `dotnet test StockPulse.Tests/StockPulse.Tests.csproj` -- expected: All tests pass
- `dotnet run --project StockPulse.API` then `curl http://localhost:5000/health` -- expected: `{"status":"Healthy"}`
- Navigate to `http://localhost:5000/swagger` -- expected: Swagger UI with all endpoints documented

**Manual checks:**
- POST /api/admin/seed with sample vehicles (some with ArrivedAt > 90 days ago)
- POST /api/admin/jobs/trigger-aging-stamp → verify 202 + jobId
- GET /api/inventory?isAging=true → verify aged vehicles returned with agingStockCount > 0
- POST /api/inventory/{id}/actions → verify 201
- GET /api/inventory/{id}/actions → verify chronological log
- Navigate to /hangfire → verify job execution history visible
- GET /health → verify Healthy status
