# Stage 2 — Requirements

**Project:** StockPulse — Intelligent Inventory Dashboard
**Scenario:** CTBW Technical Assessment — Scenario B
**Upstream:** `01-brainstorming.md` · **Downstream:** `03-prd.md`
**Status:** Baselined

> Formalizes the promoted decisions from Stage 1 into traceable, testable requirements. Each requirement has a stable ID used for traceability through the PRD, TRD, implementation, and review stages.

---

## 1. Glossary

| Term | Definition |
|---|---|
| **Vehicle** | A physical car in a dealership's stock, uniquely identified by VIN. |
| **Dealership** | The tenant boundary. All data is scoped to a `DealershipId`. |
| **Aging stock** | A vehicle whose `ArrivedAt` is older than the aging threshold (default 90 days). |
| **Aging threshold** | The configurable age, in days, beyond which a vehicle is aging. |
| **Action** | An immutable, timestamped event recording a manager's decision about a vehicle. |
| **Aging stamp job** | The scheduled background process that sets `IsAging` on vehicles past the threshold. |

---

## 2. Actors

| Actor | Description |
|---|---|
| **Dealership Manager** | Primary user. Views inventory, identifies aging stock, logs actions. |
| **Scheduler** | System actor (Hangfire). Runs the aging stamp job on a cron. |
| **Operator / Demo Admin** | Seeds data and manually triggers the aging job (assessment/ops convenience). |

---

## 3. Functional Requirements

Traceability: each FR maps back to a Scenario B core requirement (CR1 Visualization, CR2 Aging, CR3 Actions) or is an enabling requirement (EN).

### 3.1 Inventory Visualization (CR1)

| ID | Requirement | Acceptance Criteria |
|---|---|---|
| **FR-1** | The system shall return a paginated list of vehicles for a dealership. | Given vehicles exist, when the list is requested with `page` and `pageSize`, then the response contains at most `pageSize` items plus `pageNumber`, `pageSize`, `totalCount`, `totalPages`. |
| **FR-2** | The system shall filter the list by make. | `?make=Toyota` returns only Toyota vehicles; matching is case-insensitive. |
| **FR-3** | The system shall filter the list by model. | `?model=Camry` returns only Camry vehicles. |
| **FR-4** | The system shall filter the list by year. | `?year=2021` returns only vehicles of that model year. |
| **FR-5** | The system shall filter the list by aging status. | `?isAging=true` returns only aging vehicles; `false` returns only non-aging. |
| **FR-6** | The system shall support combining filters. | `?make=Ford&year=2020` applies both predicates (logical AND). |
| **FR-7** | The system shall return a single vehicle by its identifier. | `GET /api/inventory/{id}` returns the vehicle or `404` if not found in the dealership. |
| **FR-8** | Pagination inputs shall be validated. | `page < 1` or `pageSize` outside `[1, 100]` yields `400` with a validation message. |

### 3.2 Aging Stock Identification (CR2)

| ID | Requirement | Acceptance Criteria |
|---|---|---|
| **FR-9** | The system shall flag a vehicle as aging when its `ArrivedAt` is older than the aging threshold. | A vehicle arrived 91 days ago is flagged `isAging=true`; one arrived 89 days ago is `false`. |
| **FR-10** | The aging threshold shall be a named constant (default 90 days). | Threshold is defined once in code (`AgingConstants`) and not duplicated as a literal. |
| **FR-11** | The system shall recompute aging on a recurring schedule without human action. | A scheduled job runs on a configurable cron and stamps newly-aged vehicles. |
| **FR-12** | The cron schedule shall be configurable without recompilation. | Changing `AgingJob:CronExpression` in configuration changes the schedule on next start. |
| **FR-13** | The system shall expose a manual trigger for the aging job. | `POST /api/admin/jobs/trigger-aging-stamp` enqueues the job and returns `202` with a job id. |
| **FR-14** | Every inventory list response shall include the total aging stock count for the dealership. | `agingStockCount` is present and equals the count of all aging vehicles, independent of the current filter/page. |
| **FR-15** | The aging job shall be idempotent. | Running the job twice with no new arrivals stamps zero additional vehicles. |

### 3.3 Actionable Insights (CR3)

| ID | Requirement | Acceptance Criteria |
|---|---|---|
| **FR-16** | The system shall let a manager log an action against a vehicle. | `POST /api/inventory/{id}/actions` with a valid action type returns `201` and persists the entry. |
| **FR-17** | Actions shall be drawn from a controlled vocabulary. | Allowed: `PriceReductionPlanned`, `TransferRequested`, `DiscountApplied`, `WriteOffScheduled`, `NoActionRequired`. An unknown value yields `400`. |
| **FR-18** | Each action shall be timestamped at creation. | The persisted entry has a `loggedAt` UTC timestamp set by the server. |
| **FR-19** | The action log shall be append-only. | No API path updates or deletes an existing action; the interface exposes only create and read. |
| **FR-20** | The system shall return the full action history for a vehicle in chronological order. | `GET /api/inventory/{id}/actions` returns all entries oldest-to-newest; logging more entries grows the list without overwriting. |
| **FR-21** | Logging an action for a non-existent vehicle shall fail cleanly. | A `404` is returned; nothing is persisted. |

### 3.4 Enabling Requirements (EN)

| ID | Requirement | Acceptance Criteria |
|---|---|---|
| **FR-22** | The system shall seed demo inventory via an endpoint. | `POST /api/admin/seed` accepts a list of vehicles and persists them under the dealership. |
| **FR-23** | All data access shall be scoped to a single dealership. | Queries filter by `DealershipId`; data from other dealerships is never returned. |
| **FR-24** | The database schema shall be created/migrated automatically on startup. | First run on an empty server applies EF Core migrations without manual steps. |

---

## 4. Non-Functional Requirements

| ID | Category | Requirement | Acceptance Criteria |
|---|---|---|---|
| **NFR-1** | Performance | Filtered inventory queries shall use indexed columns. | `Make`, `Model`, `Year`, `IsAging`, `DealershipId` are indexed; no full-table scans for the standard filters. |
| **NFR-2** | Scalability | The list endpoint shall never return an unbounded result set. | Pagination is mandatory; `pageSize` is capped at 100. |
| **NFR-3** | Reliability | The aging job shall survive process restarts and retry on failure. | Job state is persisted (Hangfire/SQL); a failed run is retried with backoff. |
| **NFR-4** | Reliability | Application startup shall not crash on a first-run/empty database. | Migrations apply and job registration succeeds with zero rows present. |
| **NFR-5** | Observability — Logs | All requests and job runs shall emit structured logs with a correlation id. | Each log event carries `CorrelationId`; logs are queryable (JSON + Seq). |
| **NFR-6** | Observability — Metrics | Business and HTTP metrics shall be exported for scraping. | `/metrics` exposes request duration, aging-stamped count, filter result size; scraped by Prometheus. |
| **NFR-7** | Observability — Traces | Requests shall be distributed-traced. | ASP.NET Core + HttpClient spans export via OTLP to a trace backend (Jaeger). |
| **NFR-8** | Observability — Health | The system shall expose a health endpoint reflecting DB connectivity. | `GET /health` returns `Healthy`/`Unhealthy` with a DB component status. |
| **NFR-9** | Maintainability | Business logic shall be independently unit-testable without infrastructure. | Application-layer handlers are tested with mocks; no DB required to run unit tests. |
| **NFR-10** | Maintainability | The architecture shall enforce inward-pointing dependencies. | Application layer has no dependency on Infrastructure or API. |
| **NFR-11** | Portability | The system shall build and run on Linux/macOS/WSL, not only Windows. | A Docker-based SQL Server path is documented and works without LocalDB. |
| **NFR-12** | Security/Validation | All external input shall be validated before reaching business logic. | Invalid input is rejected at the pipeline boundary with `400` and a message. |

---

## 5. Constraints & Assumptions

**Constraints**
- C-1: Backend service layer only; the client is mocked (Swagger UI / cURL).
- C-2: RESTful API over a persistent relational database.
- C-3: .NET / SQL Server ecosystem (aligns with CTBW's stack).

**Assumptions (documented per the brief's ambiguity note)**
- A-1: A single dealership is seeded for the assessment, but multi-tenant scoping is built in.
- A-2: Authentication is out of scope; all endpoints are public for evaluation.
- A-3: Aging is measured from `ArrivedAt` (dealership arrival), not manufacture or listing date.
- A-4: A 24-hour maximum staleness of the aging flag is acceptable for a managerial dashboard; the manual trigger covers demo/cold-start needs.
- A-5: Inventory ingestion uses a seed endpoint; full vehicle master-data CRUD is out of scope.

---

## 6. Out of Scope

- Authentication / authorization / RBAC.
- Front-end UI beyond Swagger UI.
- Real-time push notifications (SignalR/WebSockets).
- Editing or deleting vehicles and actions after creation.
- Multi-dealership management console.

---

## 7. Traceability Matrix (Scenario → FR)

| Scenario B Core Requirement | Requirement IDs |
|---|---|
| CR1 — Inventory Visualization | FR-1 … FR-8 |
| CR2 — Aging Stock Identification | FR-9 … FR-15 |
| CR3 — Actionable Insights | FR-16 … FR-21 |
| Enabling / cross-cutting | FR-22 … FR-24, all NFRs |
