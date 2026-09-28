---
stepsCompleted: [1, 2, 3]
inputDocuments: ["docs/CBTW_Coding_Challenge.md"]
session_topic: "System Design for CBTW Scenario B - Intelligent Inventory Dashboard (.NET Core Backend)"
session_goals: "Explore and validate architecture decisions, component design, data models, API design, observability strategy, and technology justifications to produce a comprehensive System Design Document"
selected_approach: "AI-Recommended Techniques: Question Storming → Constraint Mapping → Six Thinking Hats"
techniques_used: ["Question Storming", "Constraint Mapping", "Six Thinking Hats"]
ideas_generated: 18
context_file: "docs/CBTW_Coding_Challenge.md"
---

# Brainstorming Session Results

**Facilitator:** tungnguyen
**Date:** 2026-06-03

## Session Overview

**Topic:** System Design for CBTW Scenario B — Intelligent Inventory Dashboard
**Tech Stack:** .NET Core (Backend), frontend mocked/stubbed via Swagger UI
**Goals:** Surface design options, trade-offs, and assumptions to produce a thorough System Design Document for the CBTW Technical Assessment

### Context Guidance

CBTW Scenario B requires:
1. **Inventory Visualization** — filterable list of vehicles (make, model, age)
2. **Aging Stock Identification** — vehicles in inventory >90 days, prominently displayed
3. **Actionable Insights** — log and persist a status/action for each aging vehicle

Assessment evaluates: scalability, performance, reliability, maintainability, observability, and AI collaboration strategy.

---

## Technique Selection

**Approach:** AI-Recommended Techniques
**Sequence:** Question Storming (foundation) → Constraint Mapping (validation) → Six Thinking Hats (stress-test)

---

## Phase 1: Question Storming — Decision Log

### [Architecture #1]: Server-Side Pagination Baseline
_Concept_: Inventory bounded at ~5,000 vehicles per dealership. Server-side pagination with `OFFSET/FETCH` or keyset pagination. API responses are lean. No need for Elasticsearch at this scale.
_Novelty_: Rules out event-streaming or CQRS complexity for the list view — a well-indexed SQL query is sufficient.

### [Architecture #2]: Background Job Aging Stamper
_Concept_: A scheduled background service runs periodically to query vehicles where `arrived_at < NOW() - 90 days` and sets `is_aging = true`. The flag is persisted, making it a cheap indexed column filter at query time.
_Novelty_: Provides a natural hook for future features — notifications, escalation workflows, audit trails of when a vehicle crossed the aging threshold.

### [Architecture #3]: Append-Only Action Audit Log
_Concept_: A `VehicleActionLog` table stores every action — `vehicle_id`, `action_type` (enum), `logged_by`, `logged_at`. Never updated, only inserted. API returns full chronological log per vehicle alongside latest status.
_Novelty_: Append-only gives a free audit trail without triggers or CDC. Future analytics ("which actions sell cars faster?") fall out naturally.

### [Architecture #4]: Clean Architecture Layering
_Concept_: Four layers — `Domain` (entities, enums), `Application` (use cases, MediatR handlers, interfaces), `Infrastructure` (EF Core, repositories, background job), `API` (controllers, middleware, DI). Dependencies point inward only.
_Novelty_: Application layer is fully unit-testable with mocked interfaces — the architectural seam is clearly visible to evaluators.

### [Technology #5]: SQL Server + EF Core
_Concept_: SQL Server (LocalDB for dev) with EF Core Code First migrations. Indexed columns on `is_aging`, `make`, `model`, `arrived_at`, `dealership_id`. Migrations versioned in source control.
_Novelty_: LocalDB means zero setup friction — `dotnet ef database update` and it works.

### [Technology #6]: Hangfire Background Scheduler
_Concept_: Hangfire hosted inside the API process, backed by SQL Server. Aging stamp job registered as a recurring job with a configurable cron expression. Dashboard at `/hangfire` exposed in development.
_Novelty_: Dashboard doubles as a built-in observability artifact for the background process.

### [Observability #7]: Serilog Structured Logging
_Concept_: Serilog with enrichers (`CorrelationId`, `RequestPath`, `StatusCode`, `Elapsed`) writing to Console (JSON) and File (rolling daily) sinks. Every HTTP request logged. Background job execution logged with structured properties (`{VehiclesStamped}`).
_Novelty_: Structured properties make logs queryable — not just text grep.

### [Observability #8]: Health Checks + OpenTelemetry
_Concept_: ASP.NET Core Health Checks at `/health` (DB + Hangfire), plus OpenTelemetry SDK tracking `http.request.duration`, `vehicles.aged.stamped`, `inventory.filter.results`. Exported via OTLP or Prometheus scrape endpoint.
_Novelty_: The `vehicles.aged.stamped` custom metric tells an operational story — aging stock trends over time, not just job execution status.

### [Architecture #9]: MediatR CQRS Handlers
_Concept_: All Application layer use cases as MediatR requests — `GetInventoryQuery`, `GetAgingStockQuery`, `LogVehicleActionCommand`. Pipeline behaviors for logging (`LoggingBehavior`) and validation (`ValidationBehavior` with FluentValidation).
_Novelty_: Cross-cutting concerns handled in pipeline — handlers assume valid input, keeping them single-responsibility.

### [Testing #10]: xUnit + Moq Unit Tests
_Concept_: xUnit test project targeting Application layer exclusively. Each handler gets its own test class. Repository interfaces mocked with Moq. Tests cover: pagination logic, aging filter, enum validation, audit log append, edge cases.
_Novelty_: Pure C# tests — fast, deterministic, no flaky infrastructure.

### [Architecture #11]: Auth Out of Scope
_Concept_: All endpoints unauthenticated. Documented assumption: "Auth is out of scope. In production, endpoints would sit behind an API Gateway with OAuth2/JWT handled upstream." `[AllowAnonymous]` explicit in controllers.
_Novelty_: Documenting the assumption correctly signals security awareness without polluting the codebase.

---

## Phase 2: Constraint Mapping — Decision Log

### [Constraint #12]: Swagger as Client Stub
_Concept_: `Swashbuckle.AspNetCore` with XML doc comments on all controllers. Swagger UI at `/swagger` serves as the official client-side mock. Endpoint descriptions, request/response schemas, and example values document the full API contract.
_Novelty_: Satisfies the assessment's "mock the client layer" requirement — no separate mock server needed.

### [Constraint #13]: Configurable Cron Expression
_Concept_: Hangfire cron expression loaded from `appsettings.json` (`"AgingJob": { "CronExpression": "0 2 * * *" }`). Defaults to nightly 2AM but configurable per environment without code changes.
_Novelty_: Signals operational maturity — frequency is a deployment concern, not a code concern.

### [Constraint #14]: FluentValidation in MediatR Pipeline
_Concept_: FluentValidation registered as `ValidationBehavior<TRequest, TResponse>` pipeline behavior. Validators: `LogVehicleActionCommandValidator` (enum range, vehicle exists), `GetInventoryQueryValidator` (page size bounds, valid filter values). Structured `ValidationException` responses.
_Novelty_: Zero validation code in handlers — handlers are single-responsibility, assuming valid input always.

### [Data Model #15]: Vehicle Entity
_Concept_: 11-field Vehicle entity — `Id` (Guid), `VIN` (unique), `Make`, `Model`, `Year`, `Colour`, `Price`, `Status` (enum), `ArrivedAt`, `IsAging`, `DealershipId`. EF Core indexes on filterable and aging columns. Multi-tenant by design.
_Novelty_: `DealershipId` scoping makes the API multi-tenant-ready — every query filters by dealership, no cross-dealership leakage.

---

## Phase 3: Six Thinking Hats — Stress Test

### White Hat (Facts)
All 3 core requirements covered. Clean Architecture + MediatR + EF Core is proven .NET stack. No feature gaps identified.

### Red Hat (Gut) → Resolution
**Risk identified:** OpenTelemetry adds setup complexity — could look broken without a collector.
**Resolution:** Document clearly in README: "OpenTelemetry exports to OTLP by default. Configure Prometheus exporter for local scraping. Metrics are collected internally regardless of exporter configuration."

### Yellow Hat (Benefits)
Standout design decision: append-only `VehicleActionLog` exceeds the requirement ("persist a status") by delivering a full audit trail — no extra complexity, just INSERT instead of UPDATE.

### Black Hat (Risks) → Resolution
**Risk identified:** First-startup demo failure — no vehicles stamped as aging yet, aging stock endpoint returns empty list.

### [Architecture #16]: Manual Job Trigger Endpoint
_Concept_: `POST /api/admin/jobs/trigger-aging-stamp` fires the Hangfire aging job immediately. Returns `202 Accepted` with Hangfire job ID. Documented in Swagger with clear description.
_Novelty_: Transforms a potential demo failure into a feature showcase — seed data, trigger, watch dashboard update in real time.

### Green Hat (Creativity) → Decision

### [Architecture #17]: Aging Stock Count in Paginated Response
_Concept_: `GetInventoryResponse` includes `AgingStockCount` (int) alongside paginated items — a single `COUNT WHERE IsAging = true` in the same query transaction. Response: `{ items, pageNumber, pageSize, totalCount, agingStockCount }`.
_Novelty_: Turns the list API into a lightweight analytics endpoint. Directly answers "prominently display aging stock" at the API contract level — no second round-trip.

### Blue Hat (Process) → Decision

### [Architecture #18]: README Golden Path Walkthrough
_Concept_: README demo section as a numbered sequence: (1) Clone & configure, (2) Run migrations, (3) Launch app, (4) Open Swagger, (5) Seed vehicles, (6) Trigger aging job, (7) Filter aging stock, (8) Log action, (9) Check Hangfire dashboard, (10) Check `/health`.
_Novelty_: The golden path doubles as a manual acceptance test — if all 10 steps work, every assessment criterion is demonstrated.

---

## Session Summary

**Total Decisions:** 18
**Techniques Used:** Question Storming, Constraint Mapping, Six Thinking Hats
**Key Breakthroughs:**
- Append-only audit log exceeds requirements at zero extra cost
- Swagger UI as the official client stub — elegant solution to the frontend mock requirement
- Manual job trigger endpoint prevents first-startup demo failure
- `AgingStockCount` in paginated response answers the "prominently display" requirement at the API contract level
- Golden path README doubles as a manual acceptance test

**Documented Assumptions:**
1. Auth is out of scope — API Gateway handles OAuth2/JWT in production
2. Single dealership scope per API instance (DealershipId configurable)
3. Frontend/client layer mocked via Swagger UI
4. OpenTelemetry requires collector configuration for full export; works internally without one

**Ready for:** System Design Document generation
