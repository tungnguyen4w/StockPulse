# Stage 3 — Product Requirements Document (PRD)

**Product:** StockPulse — Intelligent Inventory Dashboard
**Scenario:** CTBW Technical Assessment — Scenario B (Domain: Supply)
**Upstream:** `02-requirements.md` · **Downstream:** `04-trd.md`
**Status:** Refined / Baselined (Refine gate passed — open questions resolved as stated below)
**Author:** Product, with AI collaboration (see `README.md` → AI Collaboration Narrative)

---

## 1. Overview

StockPulse gives dealership managers a real-time, filterable view of their vehicle stock, automatically surfaces vehicles that have been sitting too long ("aging stock"), and lets managers record what they intend to do about each aging vehicle. The product targets the **Supply** domain: keeping inventory healthy by turning a passive stock list into an actionable workflow.

This PRD covers the **backend service** that powers the dashboard. The client is mocked (Swagger UI / cURL) per the assessment's chosen-layer rule.

---

## 2. Problem Statement

Dealership managers today lack a fast, trustworthy answer to one question: *"Which of my vehicles are turning into dead capital, and what am I doing about them?"* Stock that lingers past ~90 days ties up floor-plan financing, depreciates, and signals mispricing — but it hides inside a flat inventory list. Managers need the aging signal computed for them, displayed prominently, and paired with a durable record of the decisions they make in response.

---

## 3. Goals & Non-Goals

### 3.1 Goals
- **G1** — Give managers a filterable, paginated view of inventory that answers "what do I have?" in one call.
- **G2** — Automatically and continuously identify aging stock so no one has to compute it by hand.
- **G3** — Make the aging signal *prominent* — a headline count, not a buried flag.
- **G4** — Let managers record proposed actions on aging vehicles as a durable, auditable history.
- **G5** — Demonstrate production-grade qualities: scalability, reliability, observability, testability.

### 3.2 Non-Goals
- **NG1** — No authentication/authorization in this iteration (assessment scope).
- **NG2** — No front-end UI beyond Swagger UI.
- **NG3** — No editing/deleting of vehicles or logged actions.
- **NG4** — No real-time push; the dashboard is refresh-on-demand.
- **NG5** — No multi-dealership management console (single-tenant view, multi-tenant data model).

---

## 4. Personas

| Persona | Goals | Frustrations today |
|---|---|---|
| **Maria, Dealership Manager** | See stock at a glance; spot aging vehicles fast; record a plan per car. | Aging is invisible in a flat list; her "plans" live in spreadsheets and her memory. |
| **Sam, Operations/Demo Admin** | Load inventory; force an aging recalculation on demand. | No way to trigger recompute without waiting for the nightly batch. |
| **Priya, Platform/SRE** | Trust that the service is healthy and debuggable. | Black-box services with no logs, metrics, or traces. |

---

## 5. User Stories & Acceptance Criteria

Each story is traced to the requirement IDs from `02-requirements.md`.

### Epic A — Inventory Visualization *(traces CR1)*

- **US-A1** — *As Maria, I want a paginated vehicle list so I can browse stock without overload.*
  AC: returns ≤ `pageSize` items with paging metadata. → **FR-1, FR-8, NFR-2**
- **US-A2** — *As Maria, I want to filter by make/model/year so I can find a segment quickly.*
  AC: each filter narrows results; filters combine with AND; make is case-insensitive. → **FR-2, FR-3, FR-4, FR-6**
- **US-A3** — *As Maria, I want to filter specifically by aging status so I can focus on problem stock.*
  AC: `isAging=true/false` partitions the list correctly. → **FR-5**
- **US-A4** — *As Maria, I want to open one vehicle's detail so I can see its specifics.*
  AC: returns the vehicle or `404`. → **FR-7**

### Epic B — Aging Stock Identification *(traces CR2)*

- **US-B1** — *As Maria, I want aging computed for me so I never eyeball arrival dates.*
  AC: vehicles past the threshold are flagged automatically. → **FR-9, FR-10**
- **US-B2** — *As the system, aging must refresh on a schedule so the signal stays current.*
  AC: a cron job stamps newly-aged vehicles; cron is config-driven. → **FR-11, FR-12, NFR-3**
- **US-B3** — *As Sam, I want to trigger the aging recompute on demand so demos and corrections don't wait for the batch.*
  AC: trigger endpoint returns `202` + job id; job runs promptly. → **FR-13**
- **US-B4** — *As Maria, I want the total aging count shown prominently so it's the headline, not a footnote.*
  AC: every list response includes dealership-wide `agingStockCount`, independent of filter/page. → **FR-14, G3**
- **US-B5** — *As the system, re-running aging must not double-count.*
  AC: a second run with no new arrivals stamps zero. → **FR-15**

### Epic C — Actionable Insights *(traces CR3)*

- **US-C1** — *As Maria, I want to log a proposed action on a vehicle so my decision is recorded.*
  AC: valid action type → `201` + persisted, server-timestamped entry. → **FR-16, FR-18**
- **US-C2** — *As the business, actions must come from a controlled vocabulary so reporting is consistent.*
  AC: only the five allowed types accepted; unknown → `400`. → **FR-17, NFR-12**
- **US-C3** — *As compliance, the action history must be tamper-evident.*
  AC: no update/delete path exists; history is append-only. → **FR-19**
- **US-C4** — *As Maria, I want the full chronological history per vehicle so I can see the decision trail.*
  AC: returns all entries oldest→newest; new entries append. → **FR-20**
- **US-C5** — *As Maria, logging against a missing vehicle should fail safely.*
  AC: `404`, nothing persisted. → **FR-21**

### Epic D — Platform & Trust *(traces NFRs)*

- **US-D1** — *As Priya, I want structured, correlated logs so I can trace a manager's session.* → **NFR-5**
- **US-D2** — *As Priya, I want metrics and traces so I can see latency and behavior.* → **NFR-6, NFR-7**
- **US-D3** — *As Priya, I want a health endpoint so monitoring can alert on outages.* → **NFR-8**
- **US-D4** — *As Sam, I want to seed inventory and have the DB self-migrate so setup is one step.* → **FR-22, FR-24**

---

## 6. Functional Scope Summary

| Capability | In scope | Endpoint(s) |
|---|---|---|
| List & filter inventory | ✅ | `GET /api/inventory` |
| Vehicle detail | ✅ | `GET /api/inventory/{id}` |
| Aging auto-identification | ✅ | (background job) |
| Manual aging trigger | ✅ | `POST /api/admin/jobs/trigger-aging-stamp` |
| Aging headline count | ✅ | (field on list response) |
| Log action | ✅ | `POST /api/inventory/{id}/actions` |
| Action history | ✅ | `GET /api/inventory/{id}/actions` |
| Seed inventory | ✅ | `POST /api/admin/seed` |
| Health / metrics | ✅ | `GET /health`, `GET /metrics` |
| Auth, UI, edit/delete | ❌ | — (Non-Goals) |

---

## 7. UX / API Contract Principles

Since the client is mocked, the **API contract is the UX**. Principles:

- **One call for the headline.** The aging count rides on every list response (no second round-trip) — directly serves G3.
- **Predictable REST.** Resource-oriented paths, standard verbs, standard status codes (`200/201/202/400/404`).
- **Controlled vocabularies over free text.** Action types are an enum, serialized as strings for readability.
- **Fail loud, fail early.** Invalid input rejected at the boundary with a clear message, before business logic.
- **Self-describing.** Swagger UI is the official interactive client; every endpoint is documented there.

---

## 8. Success Metrics

| Metric | Target | How measured |
|---|---|---|
| All Scenario B core requirements demonstrably met | 100% (CR1–CR3) | Golden-path walkthrough in README |
| Core business logic test coverage | All handlers + edge cases | `dotnet test` green |
| Aging identification correctness | 0 false negatives at 91d, 0 false positives at 89d | Unit tests on threshold boundary |
| Filtered query latency | Indexed, no full scans | Query plan / index review (NFR-1) |
| Observability completeness | Logs + metrics + traces + health all live | Manual verification against the 4 backends |
| Cold-start safety | Startup succeeds on empty DB | First-run smoke test (NFR-4) |

---

## 9. Release Scope (single milestone)

**M1 — Assessment Deliverable**
- Epics A, B, C, D complete.
- Tests for all Application-layer handlers.
- System Design Document + README with AI Collaboration Narrative.
- Docker-based run path (cross-platform) + observability stack.

There is no phased rollout; the assessment is a single coherent deliverable. Future-facing ideas (auth, UI, real-time, multi-dealership) are recorded as Non-Goals to show deliberate scoping, not omission.

---

## 10. Risks & Mitigations

| Risk | Impact | Mitigation | Trace |
|---|---|---|---|
| Cold-start: aging all-false on fresh DB ruins demo | High | Manual trigger endpoint + seed endpoint | FR-13, FR-22 |
| Background job crashes startup on empty DB | High | Idempotent job, registered via DI after storage init | FR-15, NFR-4 |
| LocalDB won't run on Linux/WSL graders | Med | Docker SQL Server path, documented | NFR-11 |
| Staleness of aging flag (≤24h) | Low | Acceptable for managerial cadence; manual trigger covers urgency | A-4 |
| Unbounded list response at scale | Med | Mandatory pagination, `pageSize` cap 100 | NFR-2 |

---

## 11. Resolved at Refine Gate

1. **`agingStockCount` semantics — RESOLVED:** Always reflects the **whole dealership**, independent of the active filter. It is a headline KPI, not a filtered subtotal. (Matches implemented behavior: `CountAgingAsync(dealershipId)`.)
2. **Admin-gating the manual trigger — RESOLVED:** Deferred together with authentication (NG1). Stays public for the assessment.
3. **Per-vehicle "days in stock" — RESOLVED:** Not added in M1. The `isAging` boolean satisfies CR2; a computed `daysInStock` is recorded as a future enhancement, not a requirement.
