# System Design Document
## CTBW Technical Assessment — Scenario B: The Intelligent Inventory Dashboard

**Author:** tungnguyen
**Date:** 2026-06-03
**Scenario:** B — Intelligent Inventory Dashboard
**Implementation Layer:** Backend (RESTful API)
**Tech Stack:** .NET Core 8, SQL Server, Clean Architecture

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture Diagram](#2-architecture-diagram)
3. [Component Descriptions](#3-component-descriptions)
4. [Data Flow](#4-data-flow)
5. [Data Model](#5-data-model)
6. [API Contract Summary](#6-api-contract-summary)
7. [Technology Stack & Justifications](#7-technology-stack--justifications)
8. [Observability Strategy](#8-observability-strategy)
9. [Scalability, Performance & Reliability](#9-scalability-performance--reliability)
10. [Assumptions & Out-of-Scope Decisions](#10-assumptions--out-of-scope-decisions)
11. [GenAI Collaboration Narrative](#11-genai-collaboration-narrative)

---

## 1. Overview

The Intelligent Inventory Dashboard is a backend RESTful API that gives dealership managers a real-time overview of their vehicle stock. It fulfils three core requirements:

1. **Inventory Visualization** — A paginated, filterable list of all vehicles in a dealership's inventory (filter by make, model, year, aging status).
2. **Aging Stock Identification** — Vehicles in inventory for more than 90 days are automatically identified and flagged via a scheduled background job. Every API response includes a summary count of aging stock.
3. **Actionable Insights** — Managers can log a structured action type against any aging vehicle. All actions are persisted in an append-only audit log, providing a full chronological history.

The frontend layer is mocked via Swagger UI (`/swagger`), which serves as the official API contract and interactive client stub for the assessment.

---

## 2. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          CLIENT LAYER                               │
│                   Swagger UI  /  cURL  /  Postman                   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │ HTTP/REST
┌───────────────────────────────▼─────────────────────────────────────┐
│                           API LAYER                                 │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  │
│  │ InventoryController│ │ActionLogController│  │  AdminController │  │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘  │
│           │                     │                      │            │
│  ┌────────▼─────────────────────▼──────────────────────▼─────────┐  │
│  │              Middleware Pipeline                               │  │
│  │   RequestLogging │ ExceptionHandling │ CorrelationId          │  │
│  └────────────────────────────┬───────────────────────────────────┘  │
└───────────────────────────────┼─────────────────────────────────────┘
                                │ MediatR
┌───────────────────────────────▼─────────────────────────────────────┐
│                       APPLICATION LAYER                             │
│                                                                     │
│  Pipeline Behaviors (cross-cutting concerns):                       │
│  ┌─────────────────────┐    ┌──────────────────────────────────┐   │
│  │  LoggingBehavior    │───▶│      ValidationBehavior          │   │
│  │  (Serilog + OTel)   │    │      (FluentValidation)          │   │
│  └─────────────────────┘    └──────────────────────────────────┘   │
│                                                                     │
│  Queries:                          Commands:                        │
│  ┌───────────────────────┐         ┌──────────────────────────┐    │
│  │  GetInventoryQuery    │         │ LogVehicleActionCommand  │    │
│  │  GetAgingStockQuery   │         └──────────────────────────┘    │
│  └───────────────────────┘                                         │
│                                                                     │
│  Interfaces (ports):                                                │
│  IVehicleRepository │ IVehicleActionLogRepository │ IUnitOfWork    │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                     INFRASTRUCTURE LAYER                            │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    EF Core DbContext                         │   │
│  │         VehicleRepository │ VehicleActionLogRepository      │   │
│  └──────────────────────────┬───────────────────────────────────┘   │
│                             │                                       │
│  ┌──────────────────────────▼───────────────────────────────────┐   │
│  │                Hangfire Scheduler                            │   │
│  │         AgingStampJob (nightly cron, configurable)          │   │
│  │         Manual trigger: POST /api/admin/jobs/trigger-aging  │   │
│  └──────────────────────────────────────────────────────────────┘   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                         DATA LAYER                                  │
│                                                                     │
│   SQL Server (Docker container for dev / full SQL Server for prod)  │
│   ┌──────────────┐   ┌────────────────────┐   ┌────────────────┐   │
│   │   Vehicles   │   │  VehicleActionLogs  │   │ HangfireSchema │   │
│   └──────────────┘   └────────────────────┘   └────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      OBSERVABILITY PLANE                            │
│                                                                     │
│  Serilog ──▶ Console (JSON) + File (rolling) + Seq                  │
│  OpenTelemetry ──▶ Prometheus scrape (metrics) + OTLP/Jaeger (traces)│
│  Grafana ──▶ dashboards over Prometheus + Jaeger datasources         │
│  Health Checks ──▶ /health (DB connectivity + Hangfire)             │
│  Hangfire Dashboard ──▶ /hangfire (job history, retries, schedule)  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Component Descriptions

### 3.1 API Layer

**Controllers** handle HTTP concerns only — routing, model binding, HTTP status code mapping. They contain zero business logic. Three controllers:

| Controller | Responsibility |
|---|---|
| `InventoryController` | `GET /api/inventory` — paginated, filtered vehicle list |
| `ActionLogController` | `GET /api/inventory/{id}/actions`, `POST /api/inventory/{id}/actions` |
| `AdminController` | `POST /api/admin/jobs/trigger-aging-stamp`, `GET /health` passthrough |

**Middleware Pipeline** (executed on every request, in order):
- `CorrelationIdMiddleware` — generates/propagates `X-Correlation-ID` header, enriches Serilog context
- `RequestLoggingMiddleware` — logs method, path, status code, elapsed time as structured Serilog event
- `ExceptionHandlingMiddleware` — catches unhandled exceptions, returns RFC 7807 `ProblemDetails` responses

### 3.2 Application Layer

The application layer is the heart of the system. It has no dependencies on infrastructure or HTTP — it is purely business logic expressed through MediatR handlers.

**MediatR Pipeline Behaviors** (executed for every request):

| Behavior | Responsibility |
|---|---|
| `LoggingBehavior<TRequest, TResponse>` | Logs request name, input, elapsed time, and outcome via Serilog + OpenTelemetry |
| `ValidationBehavior<TRequest, TResponse>` | Runs FluentValidation validators; throws `ValidationException` on failure |

**Query Handlers:**

| Handler | Input | Output |
|---|---|---|
| `GetInventoryQueryHandler` | Make, Model, Year, IsAging filters + Page/PageSize | `InventoryResponse` (paged items + `AgingStockCount`) |
| `GetAgingStockQueryHandler` | DealershipId | List of aging vehicles with latest action |

**Command Handlers:**

| Handler | Input | Output |
|---|---|---|
| `LogVehicleActionCommandHandler` | VehicleId, ActionType (enum) | `VehicleActionLogEntry` |

### 3.3 Domain Layer

Pure C# — no framework dependencies.

**Entities:** `Vehicle`, `VehicleActionLog`
**Enums:** `VehicleStatus` (`Available`, `Reserved`, `Sold`), `VehicleActionType` (`PriceReductionPlanned`, `TransferRequested`, `DiscountApplied`, `WriteOffScheduled`, `NoActionRequired`)
**Interfaces (ports):** `IVehicleRepository`, `IVehicleActionLogRepository`, `IUnitOfWork`

### 3.4 Infrastructure Layer

Implements the interfaces defined in the Domain layer.

**EF Core DbContext** (`InventoryDbContext`): Maps entities to SQL Server tables. Configured with:
- Indexes on `IsAging`, `Make`, `Model`, `Year`, `DealershipId` for fast filtered queries
- Unique constraint on `VIN`
- Value conversion for enums stored as strings (human-readable in DB)

**Repositories:** `VehicleRepository`, `VehicleActionLogRepository` — thin wrappers around EF Core, no business logic.

**AgingStampJob (Hangfire):** Queries all vehicles where `ArrivedAt < UTC_NOW - 90 days AND IsAging = false`, bulk-updates `IsAging = true`, logs the count as a structured Serilog event and OpenTelemetry counter.

### 3.5 Data Layer

**SQL Server** (Docker container for development, full SQL Server for production; LocalDB also supported on Windows).

Three schema groups:
- **Application tables:** `Vehicles`, `VehicleActionLogs`
- **Hangfire tables:** Auto-created by Hangfire on startup (`HangFire.*`)
- **EF Core migration history:** `__EFMigrationsHistory`

---

## 4. Data Flow

### 4.1 Get Inventory (Filtered + Paginated)

```
Client
  │
  ├─▶ GET /api/inventory?make=Toyota&isAging=true&page=1&pageSize=20
  │
  ▼
InventoryController
  ├─ Binds query parameters to GetInventoryQuery
  ├─▶ MediatR.Send(GetInventoryQuery)
  │
  ▼
LoggingBehavior ──▶ ValidationBehavior ──▶ GetInventoryQueryHandler
                                               │
                                               ├─ IVehicleRepository.GetPagedAsync(filters, page, pageSize)
                                               ├─ IVehicleRepository.CountAgingAsync(dealershipId)
                                               │
                                               ▼
                                         EF Core Query
                                         WHERE Make = 'Toyota'
                                           AND IsAging = true
                                           AND DealershipId = @id
                                         ORDER BY ArrivedAt ASC
                                         OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
                                               │
                                               ▼
                                         InventoryResponse {
                                           Items: [...],
                                           PageNumber: 1,
                                           PageSize: 20,
                                           TotalCount: 47,
                                           AgingStockCount: 12   ← always included
                                         }
  │
  ◀─ 200 OK + JSON response
```

### 4.2 Log Vehicle Action

```
Client
  │
  ├─▶ POST /api/inventory/{vehicleId}/actions
  │   Body: { "actionType": "PriceReductionPlanned" }
  │
  ▼
ActionLogController
  ├─▶ MediatR.Send(LogVehicleActionCommand)
  │
  ▼
ValidationBehavior
  ├─ Validates: vehicleId exists, actionType is valid enum value
  │
  ▼
LogVehicleActionCommandHandler
  ├─ Creates VehicleActionLog { VehicleId, ActionType, LoggedAt = UTC_NOW }
  ├─ IVehicleActionLogRepository.AddAsync(entry)
  ├─ IUnitOfWork.SaveChangesAsync()
  │
  ▼
INSERT INTO VehicleActionLogs (Id, VehicleId, ActionType, LoggedAt)
VALUES (@id, @vehicleId, 'PriceReductionPlanned', @now)
  │
  ◀─ 201 Created + { id, vehicleId, actionType, loggedAt }
```

### 4.3 Aging Stock Background Job

```
Hangfire Scheduler (nightly, cron from appsettings.json)
  │
  ├─▶ AgingStampJob.ExecuteAsync()
  │
  ▼
EF Core:
  UPDATE Vehicles
  SET IsAging = 1
  WHERE ArrivedAt < DATEADD(day, -90, GETUTCDATE())
    AND IsAging = 0
  │
  ├─ Serilog: Information("Aging stamp complete. {VehiclesStamped} vehicles flagged.", count)
  ├─ OpenTelemetry: vehicles_aged_stamped_total.Add(count)
  │
  ◀─ Job completes, Hangfire records execution in HangFire.State table
```

---

## 5. Data Model

### 5.1 Vehicles Table

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uniqueidentifier | PK | GUID, generated by application |
| `VIN` | nvarchar(17) | UNIQUE, NOT NULL | Vehicle Identification Number |
| `Make` | nvarchar(100) | NOT NULL, INDEX | e.g., "Toyota" |
| `Model` | nvarchar(100) | NOT NULL, INDEX | e.g., "Camry" |
| `Year` | int | NOT NULL, INDEX | e.g., 2022 |
| `Colour` | nvarchar(50) | NOT NULL | e.g., "Midnight Black" |
| `Price` | decimal(18,2) | NOT NULL | Listed price |
| `Status` | nvarchar(20) | NOT NULL | Enum: Available / Reserved / Sold |
| `ArrivedAt` | datetime2 | NOT NULL, INDEX | Date vehicle entered inventory |
| `IsAging` | bit | NOT NULL, INDEX, DEFAULT 0 | Stamped by background job |
| `DealershipId` | uniqueidentifier | NOT NULL, INDEX | Multi-tenant scoping |

### 5.2 VehicleActionLogs Table

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uniqueidentifier | PK | GUID |
| `VehicleId` | uniqueidentifier | FK → Vehicles.Id, INDEX | |
| `ActionType` | nvarchar(50) | NOT NULL | Enum stored as string |
| `LoggedAt` | datetime2 | NOT NULL | UTC timestamp |

> **Design note:** This table is append-only. No UPDATE or DELETE operations are performed. The full chronological history is always available.

### 5.3 VehicleActionType Enum Values

| Value | Description |
|---|---|
| `PriceReductionPlanned` | Manager plans to reduce the listed price |
| `TransferRequested` | Vehicle to be transferred to another dealership |
| `DiscountApplied` | A discount has been applied to the vehicle |
| `WriteOffScheduled` | Vehicle scheduled for write-off |
| `NoActionRequired` | Manager reviewed; no action needed at this time |

---

## 6. API Contract Summary

Full interactive contract available via Swagger UI at `/swagger` when the application is running.

### 6.1 Inventory Endpoints

```
GET    /api/inventory
       Query: make, model, year, isAging, page (default: 1), pageSize (default: 20, max: 100)
       Response 200: InventoryResponse { items[], pageNumber, pageSize, totalCount, agingStockCount }

GET    /api/inventory/{id}
       Response 200: VehicleDetail
       Response 404: ProblemDetails
```

### 6.2 Action Log Endpoints

```
GET    /api/inventory/{id}/actions
       Response 200: VehicleActionLog[] (chronological, oldest first)
       Response 404: ProblemDetails (vehicle not found)

POST   /api/inventory/{id}/actions
       Body: { "actionType": "PriceReductionPlanned" }
       Response 201: VehicleActionLogEntry { id, vehicleId, actionType, loggedAt }
       Response 400: ProblemDetails (validation failure)
       Response 404: ProblemDetails (vehicle not found)
```

### 6.3 Admin / Infrastructure Endpoints

```
POST   /api/admin/jobs/trigger-aging-stamp
       Response 202: { "jobId": "hangfire-job-id" }
       (Use after seeding data to immediately run the aging stamp job)

GET    /health
       Response 200: { status: "Healthy", components: { database, hangfire } }
       Response 503: { status: "Unhealthy", ... }
```

### 6.4 Infrastructure Dashboards

```
GET    /swagger         → Swagger UI (interactive API client stub)
GET    /hangfire        → Hangfire Dashboard (job history, schedule, retries)
GET    /metrics         → Prometheus scrape endpoint (OpenTelemetry metrics)
```

---

## 7. Technology Stack & Justifications

| Technology | Version | Justification |
|---|---|---|
| **.NET 10 (ASP.NET Core)** | net10.0 | Current release; minimal hosting, first-class DI, excellent EF Core integration. Aligns with CTBW's .NET stack. |
| **Clean Architecture** | — | Enforces dependency inversion — Application layer has zero infrastructure dependencies. Maximises testability and makes the architectural intent visible to evaluators. |
| **MediatR** | 12.x | Implements CQRS pattern cleanly. Pipeline behaviors provide cross-cutting concerns (logging, validation) without polluting handlers. Each handler is independently testable. |
| **Entity Framework Core** | 9.x | Code First migrations version the schema alongside code. LINQ queries are type-safe. Excellent SQL Server support. |
| **SQL Server** | 2022 | Natural fit for the .NET ecosystem. Runs as a Docker container for zero-friction cross-platform local development (Linux/macOS/WSL); LocalDB remains an option on Windows. Production-grade SQL Server runs the same schema without changes. |
| **Hangfire** | 1.8.x | Persistent job scheduling backed by SQL Server. Built-in dashboard at `/hangfire` provides job execution history and retry visibility. Configurable cron expression loaded from `appsettings.json`. |
| **Serilog** | 9.x (AspNetCore) | Industry standard for structured logging in .NET. Enrichers add `CorrelationId`, `RequestPath`, `Elapsed` to every log event. Sinks to Console, rolling File, and **Seq** for queryable structured log search. |
| **OpenTelemetry .NET SDK** | 1.x | Vendor-neutral observability. Metrics (HTTP request duration, aging job output, inventory filter result sizes) export to **Prometheus** (visualized in **Grafana**); traces export via OTLP to **Jaeger**. |
| **FluentValidation** | 11.x | Declarative, testable validation rules registered as a MediatR pipeline behavior. Keeps handlers clean — they assume valid input always. |
| **xUnit + Moq** | Latest | xUnit is the dominant .NET test framework. Moq provides clean interface mocking. Application layer handlers are pure C# — fast, deterministic, no infrastructure required. |
| **Swashbuckle (Swagger)** | 6.x | Auto-generates OpenAPI spec from controller attributes and XML comments. Swagger UI at `/swagger` serves as the official client-side mock per the assessment requirements. |

---

## 8. Observability Strategy

The observability strategy covers three tiers: HTTP layer, Application layer, and Background job layer.

### 8.0 Why We Need Logs, Metrics, and Traces

The brief calls out observability as an explicit design concern, and for a dealership-facing service that is the right instinct: once this runs in production, **you cannot attach a debugger to a live customer's request**. Observability is how you understand a system you cannot pause. Each pillar answers a different question, and no single one is sufficient — they are complementary, not redundant.

| Pillar | Tool | The question it answers | What goes wrong without it |
|---|---|---|---|
| **Logs** | Serilog → **Seq** | *"What exactly happened during this one request?"* — the narrative detail: which vehicle, which filter, which validation failed. | You know the system broke but not why. You're reduced to guessing or trying to reproduce a customer's exact state. |
| **Metrics** | OpenTelemetry → **Prometheus / Grafana** | *"How is the system behaving in aggregate, right now and over time?"* — request rates, latency percentiles, error ratios, how many vehicles each aging-job run stamped. | No early warning. You learn about a slowdown or a creeping error rate from an angry user, not a dashboard. You can't set alerts on trends you don't measure. |
| **Traces** | OpenTelemetry → **Jaeger** | *"Where in the request path did the time go?"* — the span breakdown across middleware, handler, and database call. | A "the dashboard is slow" report has no actionable detail. You can't tell if it's the DB query, validation, or an external call without one. |

**Why these specific tools (vs. just writing to a file):**

- **Serilog + Seq** — logs are emitted as **structured JSON**, not flat text, so they're *queryable*: "show every `Warning` for `DealershipId = X` in the last hour" is a filter, not a `grep`. The `CorrelationId` enricher ties every log line from one HTTP request together — essential the moment you run more than one instance and logs interleave.
- **OpenTelemetry** — a **vendor-neutral** instrumentation standard. We instrument the code once; the same signals can be exported to Prometheus, Jaeger, or any cloud APM (Datadog, Honeycomb, Azure Monitor) by changing configuration, not code. This avoids lock-in and is why it's the industry default rather than a proprietary SDK.
- **Prometheus + Grafana** — the de-facto open-source metrics pairing: Prometheus pull-scrapes the `/metrics` endpoint and stores time-series; Grafana turns them into dashboards and alert rules.
- **Jaeger** — purpose-built trace storage and a waterfall UI that makes a slow span obvious at a glance.

**Why now, for a take-home, and not "later in production":** instrumenting after the fact means retrofitting correlation IDs, span boundaries, and metric points across an existing codebase — expensive and error-prone. Building it in from the first commit costs little (the pipeline behaviors and middleware were going in anyway) and means the **golden-path demo itself is observable end to end** — you can watch a single request flow from a Seq log line, to a Jaeger trace, to a Prometheus counter. The whole stack runs locally via one `docker compose up -d`, so this fidelity costs the reviewer nothing to reproduce.

### 8.1 Structured Logging (Serilog)

Every log event is a structured JSON object, not a text string. This makes logs queryable and parseable by log aggregation tools (Seq, Splunk, Elastic).

**Enrichers applied globally:**
- `CorrelationId` — generated per request, propagated via `X-Correlation-ID` header
- `MachineName` — for multi-instance deployments
- `Application` — service name for log aggregation

**Key log events:**

| Event | Level | Structured Properties |
|---|---|---|
| HTTP request received | Information | `Method`, `Path`, `QueryString` |
| HTTP response sent | Information | `StatusCode`, `Elapsed`, `CorrelationId` |
| MediatR handler executed | Information | `RequestName`, `Elapsed`, `Success` |
| Validation failure | Warning | `RequestName`, `Errors[]` |
| Aging stamp job complete | Information | `VehiclesStamped`, `JobDuration` |
| Unhandled exception | Error | `ExceptionType`, `Message`, `StackTrace` |

**Sinks:**
- **Console** — structured JSON, suitable for container log collection
- **File** — rolling daily, retained 7 days, path configurable via `appsettings.json`

### 8.2 Metrics (OpenTelemetry)

Custom metrics tracked via `System.Diagnostics.Metrics`:

| Metric Name | Type | Description |
|---|---|---|
| `http.request.duration` | Histogram | HTTP request latency by endpoint and status code |
| `vehicles.aged.stamped.total` | Counter | Cumulative vehicles stamped as aging per job run |
| `inventory.filter.results` | Histogram | Distribution of inventory query result set sizes |
| `action.log.created.total` | Counter | Total action log entries created |

**Export pipeline:**
- **Metrics → Prometheus** — scrape endpoint at `/metrics`, scraped every 15s by the Prometheus container and visualized in **Grafana** (`http://localhost:3000`)
- **Traces → OTLP → Jaeger** — ASP.NET Core and HttpClient spans exported over OTLP gRPC (`http://localhost:4317`) to **Jaeger** (`http://localhost:16686`); endpoint configurable via `OpenTelemetry:OtlpEndpoint`

### 8.3 Health Checks

```
GET /health
Response: {
  "status": "Healthy",
  "components": {
    "database": { "status": "Healthy", "description": "SQL Server connection OK" },
    "hangfire": { "status": "Healthy", "description": "Hangfire storage accessible" }
  }
}
```

Health checks are suitable for load balancer liveness/readiness probes.

### 8.4 Hangfire Dashboard

The Hangfire dashboard at `/hangfire` provides:
- Job execution history with timestamps and durations
- Retry counts and failure reasons
- Next scheduled run time for the aging stamp job
- Manual job enqueueing capability

In production, the dashboard would be restricted by IP allowlist or internal-only routing.

---

## 9. Scalability, Performance & Reliability

### Scalability

- **Stateless API** — no in-memory state, horizontally scalable behind a load balancer
- **Server-side pagination** — responses are bounded regardless of inventory size; no full-table scans on list endpoints
- **Indexed queries** — all filter columns (`Make`, `Model`, `Year`, `IsAging`, `DealershipId`) are indexed; query plans are predictable
- **DealershipId scoping** — every query is naturally partitioned by dealership, enabling future sharding if needed

### Performance

- **Pre-computed aging flag** — `IsAging` is a persisted column, not a computed expression. Filtering aging stock is a single indexed boolean lookup, not a date arithmetic scan across the full table
- **Single-query response** — `AgingStockCount` is returned alongside paginated results in one database round-trip using a split query strategy
- **Async/await throughout** — all I/O operations are non-blocking; the thread pool is not starved under load

### Reliability

- **Hangfire retry policy** — aging stamp job retries automatically on failure (configurable retry count, exponential backoff)
- **Append-only audit log** — `VehicleActionLogs` is insert-only; no UPDATE/DELETE paths means no risk of data loss from concurrent writes
- **EF Core optimistic concurrency** — `Vehicle` entity includes a `RowVersion` concurrency token to prevent lost updates
- **Global exception handler** — all unhandled exceptions are caught by middleware, logged, and returned as RFC 7807 `ProblemDetails` (never raw stack traces)
- **Health check endpoint** — enables infrastructure-level liveness monitoring

### Maintainability

- **Clean Architecture** — adding a new feature means adding a new handler; existing code is not modified (Open/Closed Principle)
- **MediatR pipeline** — new cross-cutting concerns (rate limiting, caching, audit logging) are added as pipeline behaviors without touching handlers
- **Code First migrations** — schema changes are versioned, reviewable, and reversible
- **Configurable values in `appsettings.json`** — cron expression, page size limits, Serilog sinks, OpenTelemetry exporters — all environment-configurable without code changes

---

## 10. Assumptions & Out-of-Scope Decisions

| # | Assumption | Rationale |
|---|---|---|
| 1 | **Authentication is out of scope.** All endpoints are unauthenticated in this implementation. In production, the API would sit behind an API Gateway (e.g., Azure API Management) with OAuth2/JWT bearer token validation handled at the gateway layer. `[AllowAnonymous]` is explicit in all controllers to document the intent. | Keeps the codebase focused on the assessment's core requirements. Auth plumbing would double implementation time without demonstrating relevant skills. |
| 2 | **Single dealership per API instance.** `DealershipId` is configured in `appsettings.json` rather than derived from a JWT claim. All queries are scoped to this value. | Realistic for a single-tenant deployment; multi-tenant requires auth (see assumption 1). The data model supports multi-tenancy — `DealershipId` is on every entity. |
| 3 | **Swagger UI is the client-side layer.** No separate frontend application is built. The Swagger UI at `/swagger` serves as the interactive client stub per the assessment's explicit guidance. | Assessment states: "Mock or stub the client-side layer with... a basic API contract (e.g., OpenAPI spec)." Swagger satisfies this elegantly. |
| 4 | **Aging threshold is fixed at 90 days.** The threshold is a constant in the Domain layer (`AgingThresholdDays = 90`), not a configurable dealership setting. | The requirement specifies ">90 days" as the definition. Making it configurable would require a dealership settings table and adds scope beyond the requirement. |
| 5 | **OpenTelemetry requires a collector for full export.** Metrics are collected internally regardless. The Prometheus scrape endpoint at `/metrics` works without a collector for local observation. | OTLP export requires an OpenTelemetry Collector running separately. The README documents how to enable the Prometheus endpoint for zero-infrastructure local metric inspection. |
| 6 | **Vehicle data is seeded via a seed endpoint.** A `POST /api/admin/seed` endpoint accepts a JSON array of vehicles for local development and demo purposes. | Avoids requiring a separate SQL script or migration seed. Evaluators can seed realistic data through Swagger UI. |

---

## 11. GenAI Collaboration Narrative

### How GenAI Was Used in the Design Phase

This System Design Document was produced through a structured collaboration with Claude (Anthropic), using the BMAD brainstorming framework. The collaboration was deliberately structured across three phases:

**Phase 1 — Question Storming**
Rather than having the AI generate a document immediately, I directed it to surface design questions first. This forced examination of assumptions before any architecture was committed. Key questions raised: What is the inventory scale? Who owns the aging calculation — the database or a job? Is this an audit trail or a last-known-state record? These questions shaped 11 core decisions before a single line of architecture was drawn.

**Phase 2 — Constraint Mapping**
I directed the AI to distinguish real constraints (assessment requirements, .NET Core stack, persistence requirement) from imagined ones (FluentValidation vs. DataAnnotations, Hangfire vs. BackgroundService). This kept the design lean — complexity was added only where the assessment criteria or demo quality demanded it, not as speculative engineering.

**Phase 3 — Six Thinking Hats Stress Test**
The AI was used to adversarially challenge the design across six perspectives before synthesis. The most valuable output: identifying that a first-startup demo failure (empty aging stock before the job runs) was a high-probability risk. The manual trigger endpoint (`POST /api/admin/jobs/trigger-aging-stamp`) was added directly as a result. The `AgingStockCount` summary field in every paginated response also emerged from this phase — a design improvement that wasn't in the original requirements but directly serves the "prominently display" criterion.

### Verification and Ownership

Every decision the AI proposed was evaluated for fit before being accepted. Three examples of AI suggestions that were redirected:

- The AI initially suggested considering DataAnnotations for validation (simpler). I overrode this in favour of FluentValidation because the assessment evaluates design maturity and the MediatR pipeline behavior pattern demonstrates architectural sophistication.
- The AI flagged OpenTelemetry as potentially complex for a demo. I chose to keep it and directed the AI to document the fallback (Prometheus scrape endpoint) in the assumptions section — turning the risk into a documented, handled concern.
- The AI proposed making the aging threshold configurable. I explicitly kept it as a domain constant — configuration adds scope without being required by the assessment.

### What the AI Did Well

- Surfacing non-obvious risks (first-startup demo failure, OpenTelemetry collector dependency)
- Proposing the append-only audit log pattern as a way to exceed requirements without adding complexity
- Maintaining consistency across 18 decisions across a multi-phase session
- Generating this document from the agreed decision log without introducing new assumptions

### What Required Human Judgment

- Deciding which complexity was worth adding (FluentValidation, OpenTelemetry) vs. keeping simple (DataAnnotations, plain `BackgroundService`)
- Scoping decisions: what is intentionally out of scope vs. what is a gap
- Assessing what an evaluator will actually run, and designing the README golden path accordingly
- Final ownership of every architectural decision — the AI proposed, I decided

### From Design to Verified Implementation

This design phase fed a formal, traceable delivery pipeline captured under [`docs/specs/`](specs/): brainstorming → requirements (`FR-1…FR-24`, `NFR-1…NFR-12`) → PRD → TRD → implement & test → review → approve. Each stage has explicit refine/approve gates, and the final review (`docs/specs/05-review.md`) maps every requirement to its implementing code and verifying test.

During implementation the same direct-and-verify approach surfaced and resolved concrete issues the design phase could not predict: the solution was moved off Windows-only LocalDB to a Docker SQL Server for cross-platform builds; a full observability stack (Prometheus/Grafana/Seq/Jaeger) was stood up with distributed tracing wired in; a Hangfire startup crash (static `RecurringJob` API used before `JobStorage.Current` was initialized) was diagnosed and fixed via the DI-resolved `IRecurringJobManager`; and dependency advisories were cleared (Newtonsoft.Json 13.0.4, OpenTelemetry 1.15.x), taking the build from 10 warnings to 0 with all tests green.

---

*Document generated: 2026-06-03; implementation & observability updates: 2026-06-04*
*Brainstorming session: `_bmad-output/brainstorming/brainstorming-session-2026-06-03-155811.md`*
*Delivery pipeline: [`docs/specs/`](specs/) (requirements → PRD → TRD → review)*
*Based on: CTBW Technical Assessment V1.0*
