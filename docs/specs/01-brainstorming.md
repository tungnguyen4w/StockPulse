# Stage 1 — Brainstorming

**Project:** StockPulse — Intelligent Inventory Dashboard
**Scenario:** CTBW Technical Assessment — Scenario B (Domain: Supply)
**Method:** Question Storming → Constraint Mapping → Six Thinking Hats
**Input:** `docs/CTBW_Coding_Challenge.md` (Scenario B core requirements)

> This document captures the divergent thinking that precedes formal requirements. Nothing here is a commitment — it is the raw idea space. Decisions that survive into scope are promoted to `02-requirements.md`.

---

## 0. The Three Seed Requirements (verbatim)

From the challenge brief, Scenario B asks for exactly three capabilities:

1. **Inventory Visualization** — a filterable list of all vehicles in a dealership's inventory (e.g. filter by make, model, age).
2. **Aging Stock Identification** — automatically identify and prominently display "aging stock" (vehicles in inventory for >90 days).
3. **Actionable Insights** — let a manager log and persist a status / proposed action for each aging vehicle (e.g. "Price Reduction Planned").

Chosen service layer (per Part 2): **Backend** — RESTful API + persistent database, client mocked via Swagger UI / cURL.

---

## 1. Question Storming

Generating questions before answers, to expose hidden assumptions.

### On "inventory"
- What is the unit of inventory — a physical vehicle, or a listing? → A physical vehicle, uniquely identified by VIN.
- Is inventory scoped to one dealership or many? → A manager sees **one** dealership. Multi-tenant scoping must exist even if only one tenant is seeded.
- How big can one dealership's inventory get? → Hundreds to low-thousands of vehicles. Pagination is mandatory, not optional.
- What attributes does a manager filter on? → Make, model, year, and aging status at minimum. Price/colour are display-only for now.

### On "aging"
- Is "90 days" a business rule or a magic number? → A business rule that **will** change. It must be a named constant, ideally configurable.
- Aged from what date — arrival, listing, or manufacture? → **Arrival** date (`ArrivedAt`). This is when the vehicle entered *this* dealership's stock.
- Is aging computed on read, or stamped on write? → Big decision (see §2). Both are viable.
- Does a manager need a count of aging stock, or just the flag per vehicle? → Both. A summary count drives the dashboard headline; the per-vehicle flag drives the list.

### On "actionable insights"
- Is an action a mutable status, or an event? → An **event**. "Price Reduction Planned" on Tuesday and "Discount Applied" on Friday are two facts, not one overwritten field.
- Can a manager edit or delete a past action? → No. The audit trail is append-only. This is a compliance-friendly stance and simplifies concurrency.
- Are actions free-text or a controlled vocabulary? → Controlled vocabulary (enum). Free-text invites inconsistency and defeats reporting.
- Can you log an action on a *non*-aging vehicle? → Allowed but unusual; the API should not hard-block it. Business rule lives in the UI/manager judgment, not a 400.

### On "the system around it"
- How does a manager trust the dashboard is live? → Health endpoint + observability (logs/metrics/traces).
- How does aging get recomputed without a human clicking a button? → A scheduled background job.
- What happens on a brand-new database with no job having run yet? → Demo risk: aging would show as all-false. Need a **manual trigger** endpoint to avoid a cold-start demo failure.

---

## 2. Key Tension: Compute-on-Read vs. Stamp-on-Write

The single most consequential design fork surfaced during brainstorming.

| | **Compute-on-read** | **Stamp-on-write (chosen)** |
|---|---|---|
| Aging derived | Every query: `ArrivedAt < now-90d` | Nightly job sets `IsAging` column |
| Read cost | Recomputed each request | Indexed boolean filter — cheap |
| Filtering by aging | Computed predicate, harder to index | Trivial `WHERE IsAging = 1` |
| Summary count | Aggregate scan per request | Pre-stamped, cheap count |
| Staleness | Always exact | Up to 24h stale between runs |
| Demonstrates | Simplicity | Background scheduling, observability, a real "supply" workflow |

**Lean:** Stamp-on-write. The 24h staleness is acceptable for a managerial dashboard (aging is a slow-moving signal), and it lets the solution showcase Hangfire scheduling, a manual trigger, and an aging-stamp metric — all of which map to the evaluation framework's "foresight" and "observability" criteria. A manual trigger endpoint neutralizes the staleness/cold-start downside for demos.

---

## 3. Constraint Mapping

What's fixed, what's flexible, what's out.

**Hard constraints (from brief)**
- Backend only; persistent DB; RESTful.
- Must satisfy all three Scenario B requirements.
- Must ship tests for core business logic.
- Must document AI collaboration.
- Build for scalability, performance, reliability, maintainability, observability.

**Self-imposed constraints (design maturity signals)**
- Clean Architecture, dependencies pointing inward.
- CQRS via MediatR; validation in the pipeline, not the handlers.
- Append-only audit log (no UPDATE/DELETE path at the interface).
- Multi-tenant scoping by `DealershipId` from day one.

**Explicitly out of scope**
- Authentication/authorization (brief says client is mocked; all endpoints public for the assessment).
- Front-end UI (Swagger UI is the stand-in client).
- Real-time push / SignalR (dashboard is poll-on-refresh).
- Editing vehicle master data (inventory ingestion is via a seed endpoint only).

---

## 4. Six Thinking Hats (compressed)

- **White (facts):** Three requirements, backend, tests, observability, AI narrative. .NET ecosystem assumed (CTBW is a .NET shop).
- **Red (gut):** The append-only log "feels" right for an automotive/compliance domain. Stamp-on-write "feels" like the grown-up answer.
- **Black (caution):** Cold-start aging = all false → bad demo. Mitigate with manual trigger. Background job on an empty/first-run DB must not crash startup. LocalDB won't run on Linux/WSL — needs a cross-platform DB story (Docker).
- **Yellow (optimism):** Stamp-on-write + Hangfire showcases real supply-chain thinking and gives free observability hooks (job metrics, run history).
- **Green (creativity):** Surface `AgingStockCount` in *every* paginated response so the dashboard headline needs no second call. Add a per-request `X-Correlation-ID` so a manager's session is traceable end to end.
- **Blue (process):** Lock decisions → write spec → implement in verifiable phases → test before declaring done.

---

## 5. Promoted Decisions (feed into Stage 2)

These survived brainstorming and become requirements:

1. Vehicle is the inventory unit, keyed by VIN, scoped by `DealershipId`.
2. Filtering: make, model, year, aging status, with mandatory pagination.
3. Aging = `ArrivedAt` older than a **named, configurable** 90-day threshold.
4. Aging is **stamped** by a scheduled background job, **plus** a manual trigger endpoint.
5. Every inventory response carries an `AgingStockCount` summary.
6. Actions are an **append-only** log of controlled-vocabulary events, never mutated.
7. Observability is first-class: structured logs, metrics, traces, health, job dashboard.
8. Cross-platform persistence (Docker SQL Server) so the build runs outside Windows.

---

## 6. Open Questions Carried Forward

- Should the configurable cron / threshold live in `appsettings` only, or also be runtime-overridable? *(Resolved later: appsettings is sufficient for the assessment.)*
- Do we need optimistic concurrency on the `Vehicle.IsAging` stamp? *(Resolved: single writer = the job; not needed.)*
- Is `NoActionRequired` a real action or the absence of one? *(Kept as an explicit action so "we looked and chose to do nothing" is auditable.)*
