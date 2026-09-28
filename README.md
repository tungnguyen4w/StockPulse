# StockPulse — Intelligent Inventory Dashboard

CBTW Technical Assessment — Scenario B

A .NET 10 backend REST API for dealership inventory management with automated aging stock detection, action audit logging, and full observability.

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0+ |
| Docker + Docker Compose | For SQL Server and the observability stack |
| dotnet-ef (optional, for migrations) | `dotnet tool install -g dotnet-ef` |

> **Windows / Visual Studio?** SQL Server LocalDB also works — set `ConnectionStrings:DefaultConnection` in `appsettings.json` to `Server=(localdb)\\mssqllocaldb;...`. The Docker path below is the cross-platform default (Linux/macOS/WSL) and is required for the monitoring stack.

---

## Build & Run

```bash
# 1. Clone the repository
git clone <repo-url>
cd StockPulse

# 2. Start SQL Server + observability stack (Prometheus, Grafana, Seq, Jaeger)
docker compose up -d

# 3. Build the solution
dotnet build StockPulse.slnx

# 4. Run the API (migrations apply automatically on startup)
dotnet run --project StockPulse.API
```

The API starts at `http://localhost:5153` (or `https://localhost:7143`).

---

## Observability Stack

`docker compose up -d` brings up the full local monitoring stack alongside SQL Server. Once the API is running:

| Tool | URL | What it shows |
|---|---|---|
| **Grafana** | `http://localhost:3000` | Metrics dashboards (login `admin` / `admin`) |
| **Prometheus** | `http://localhost:9090` | Raw metrics & query explorer (scrapes the API every 15s) |
| **Seq** | `http://localhost:5341` | Structured logs with full-text search (Serilog sink) |
| **Jaeger** | `http://localhost:16686` | Distributed traces & request timelines (OTLP exporter) |

Grafana ships with Prometheus and Jaeger datasources pre-provisioned. To add a metrics dashboard: **Dashboards → New → Import** and use ID `17706` (ASP.NET Core).

---

## Run Tests

```bash
dotnet test StockPulse.Tests/StockPulse.Tests.csproj
```

Expected: **10 tests, all passing** — Application layer unit tests using xUnit + Moq.

---

## Golden Path Walkthrough (10 Steps)

Follow these steps in order to demonstrate all three core features.

**Open Swagger UI:** `http://localhost:5153/swagger`

### Step 1 — Seed vehicles

`POST /api/admin/seed`

```json
[
  { "vin": "1HGCM82633A004352", "make": "Toyota", "model": "Camry", "year": 2021, "colour": "Silver", "price": 28000, "status": "Available", "arrivedAt": "2025-01-01T00:00:00Z" },
  { "vin": "2T1BURHE0JC043821", "make": "Honda", "model": "Civic", "year": 2022, "colour": "Blue", "price": 22000, "status": "Available", "arrivedAt": "2025-02-15T00:00:00Z" },
  { "vin": "3VWFE21C04M000001", "make": "Ford", "model": "Focus", "year": 2020, "colour": "Red", "price": 18000, "status": "Available", "arrivedAt": "2026-05-01T00:00:00Z" }
]
```

The first two vehicles have `arrivedAt` dates more than 90 days ago — they will be stamped as aging.

### Step 2 — Trigger the aging stamp job

`POST /api/admin/jobs/trigger-aging-stamp`

Returns `202 Accepted` with a Hangfire `jobId`. The job runs immediately and stamps vehicles older than 90 days with `isAging: true`.

### Step 3 — View the Hangfire dashboard

Open `http://localhost:5153/hangfire`

You'll see the aging stamp job in the **Succeeded** queue with execution time and the count of vehicles stamped.

### Step 4 — List all inventory

`GET /api/inventory?page=1&pageSize=20`

Response includes `agingStockCount` — the total number of aging vehicles across the dealership — in every paginated response.

### Step 5 — Filter aging stock

`GET /api/inventory?isAging=true`

Returns only vehicles with `isAging: true`. Confirm the two seeded old vehicles appear.

### Step 6 — Filter by make/model

`GET /api/inventory?make=Toyota`

`GET /api/inventory?make=Ford&year=2020`

### Step 7 — View a single vehicle

`GET /api/inventory/{id}` — use a vehicle ID from the previous response.

### Step 8 — Log an action on an aging vehicle

`POST /api/inventory/{id}/actions`

```json
{ "actionType": "PriceReductionPlanned" }
```

Returns `201 Created` with the action log entry. Available action types:
- `PriceReductionPlanned`
- `TransferRequested`
- `DiscountApplied`
- `WriteOffScheduled`
- `NoActionRequired`

### Step 9 — View the full action audit trail

`GET /api/inventory/{id}/actions`

Returns all actions in chronological order. Log another action and call again — the trail grows; nothing is overwritten.

### Step 10 — Check application health

`GET /health`

```json
{ "status": "Healthy", "components": { "database": { "status": "Healthy" } } }
```

**Bonus:** View Prometheus metrics at `http://localhost:5153/metrics`, then explore them in **Grafana** (`http://localhost:3000`), logs in **Seq** (`http://localhost:5341`), and request traces in **Jaeger** (`http://localhost:16686`).

---

## Project Structure

```
StockPulse/
├── StockPulse.Domain/          # Entities, enums, interfaces (no dependencies)
├── StockPulse.Application/     # MediatR handlers, validators, DTOs
├── StockPulse.Infrastructure/  # EF Core, repositories, Hangfire job
├── StockPulse.API/             # Controllers, middleware, Program.cs
└── StockPulse.Tests/           # xUnit + Moq unit tests
```

## Key Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/inventory` | Paginated, filterable vehicle list |
| `GET` | `/api/inventory/{id}` | Single vehicle detail |
| `GET` | `/api/inventory/{id}/actions` | Chronological action audit log |
| `POST` | `/api/inventory/{id}/actions` | Log a new action |
| `POST` | `/api/admin/seed` | Seed vehicles for demo |
| `POST` | `/api/admin/jobs/trigger-aging-stamp` | Trigger aging job manually |
| `GET` | `/health` | Health check (DB connectivity) |
| `GET` | `/metrics` | Prometheus scrape endpoint |
| `GET` | `/swagger` | Swagger UI (interactive client stub) |
| `GET` | `/hangfire` | Hangfire job dashboard |

---

## Configuration

Base settings live in `appsettings.json`; the Docker/observability overrides (connection string, Seq sink, OTLP endpoint) live in `appsettings.Development.json`.

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Docker SQL Server (`localhost,1433`) in Development; LocalDB in base config | SQL Server connection string |
| `DealershipId` | `11111111-...` | Dealership GUID — all queries scoped to this |
| `AgingJob:CronExpression` | `0 2 * * *` | Nightly 2AM; change without code recompile |
| `OpenTelemetry:OtlpEndpoint` | `http://localhost:4317` | OTLP gRPC endpoint for traces (Jaeger) |
| `Serilog:WriteTo` (Seq) | `http://localhost:5341` | Seq log sink (Development) |

---

## Architecture

Clean Architecture with 4 layers. Dependencies point inward only:

```
API → Application ← Infrastructure
        ↓
      Domain
```

- **MediatR CQRS** — every use case is a discrete handler with pipeline behaviors for logging and validation
- **FluentValidation** — all input validated in the pipeline before reaching handlers
- **EF Core** — Code First migrations, SQL Server, indexed columns for fast filtered queries
- **Hangfire** — persistent background scheduling with dashboard visibility
- **Serilog** — structured JSON logging with `CorrelationId` enrichment; sinks to Console, rolling File, and **Seq**
- **OpenTelemetry** — custom metrics (`vehicles.aged.stamped.total`, `inventory.filter.results`) exported to **Prometheus** (visualized in Grafana), plus ASP.NET Core / HttpClient **traces** exported via OTLP to **Jaeger**
- **Append-only audit log** — `VehicleActionLog` is INSERT-only, no UPDATE/DELETE paths exist at the interface level

**Diagrams:** [Architecture diagram (draw.io)](https://app.diagrams.net/#G1qJ6q3tTFg8NewGOT_VppIJ3uD_ZaodEI#%7B%22pageId%22%3A%22stockpulse-arch%22%7D) — source files are also in the repo at [`docs/architecture.drawio`](docs/architecture.drawio) and [`docs/sequence-diagrams.drawio`](docs/sequence-diagrams.drawio). Full details in the [System Design Document](docs/System_Design_Document.md).

---

## AI Collaboration Narrative

### Strategy

This solution was built using Claude Code (Anthropic) with a deliberate three-phase collaboration strategy:

**Phase 1 — Design through brainstorming.** Rather than generating code immediately, I used the BMAD brainstorming framework to run a structured Question Storming → Constraint Mapping → Six Thinking Hats session. This produced 18 locked architectural decisions before any code was written. Key decisions shaped by this process: the append-only audit log pattern, the manual job trigger endpoint (to prevent a first-startup demo failure), and the `AgingStockCount` summary field in every paginated response.

**Phase 2 — Spec-driven implementation.** A formal, human-validated scope was written and approved before the AI began coding — no speculative features, no over-engineering. This artifact chain is captured end-to-end under [`docs/specs/`](docs/specs/): brainstorming → requirements → PRD → TRD → implement & test → review → approve. Every requirement carries a stable ID (`FR-1…FR-24`, `NFR-1…NFR-12`) that is traced from the requirements file all the way to the implementing code and its verifying test in the review (`docs/specs/05-review.md`).

**Phase 3 — Iterative verification.** Every phase ended with a build check. Compilation errors were diagnosed and fixed before moving to the next phase. The test suite was written before claiming any feature was complete. The final review audited all 24 functional and 12 non-functional requirements against code, then closed three dependency/documentation findings (F-1…F-3) and re-verified a clean build (**0 errors, 0 warnings**) with **10/10 tests** green.

### What the AI Generated

- All boilerplate (project scaffolding, NuGet references, EF Core configuration)
- MediatR handler structure and pipeline behaviors
- Repository implementations
- Controller routing
- Middleware (CorrelationId, ExceptionHandling)
- Unit tests (10 tests covering all handlers and edge cases)

### What Required Human Judgment

- Choosing FluentValidation over DataAnnotations (design maturity signal)
- Keeping OpenTelemetry despite its collector complexity (with a documented fallback)
- Downgrading Swashbuckle from 9.x to 6.x when a breaking API change was discovered
- Replacing `IConfiguration` in the Application layer with `IOptions<InventorySettings>` to preserve Clean Architecture
- Identifying and removing `Microsoft.AspNetCore.OpenApi` (incompatible with Swashbuckle 6.x)
- Moving local persistence off Windows-only LocalDB to a **Docker SQL Server** so the solution builds and runs on Linux/macOS/WSL (the original code threw `PlatformNotSupportedException` outside Windows)
- Standing up a real **observability stack** (Prometheus + Grafana for metrics, Seq for logs, Jaeger for OTLP traces) via `docker-compose`, and wiring distributed tracing into the app
- Diagnosing a startup crash where Hangfire's **static `RecurringJob` API** ran before `JobStorage.Current` was initialized, and fixing it to use the DI-resolved `IRecurringJobManager`
- **Dependency security hardening**: pinning `Newtonsoft.Json` to 13.0.4 to override a transitive high-severity advisory, and bumping OpenTelemetry to 1.15.x to clear moderate advisories — taking the build from 10 warnings to 0

### Verification Process

Each AI-generated code block was reviewed before acceptance. Build failures were diagnosed by reading the actual compiler errors, not by blindly retrying. The test suite was run and verified to pass before the implementation was declared complete. Verification is documented as a first-class artifact: [`docs/specs/05-review.md`](docs/specs/05-review.md) maps every requirement to its implementing code and verifying test, records build/test evidence, and tracks residual findings (F-4/F-5) as explicit, accepted follow-ups rather than silent omissions.
