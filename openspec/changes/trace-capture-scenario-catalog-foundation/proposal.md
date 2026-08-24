# Trace Capture and Scenario Catalog Foundation

> Status: IMPLEMENTED_PENDING_VALIDATION_CLEARANCE
> Architecture Gate: APPROVED (trace-capture-scenario-catalog-architecture-gate.md)
> Baseline: 726fb9a (golden semantic run graduated)

## Why

Establish Harness-owned TraceCaptureSession, persistence store, ScenarioCatalog, and catalog-driven replay regression. Zero Runtime semantic delta.

Current gaps:
- `PhysicalEnvironment` retains only in-memory histories — no durable capture
- Golden replay constructs minimized Observations in C# with ad-hoc trace JSON
- No canonical Scenario manifest or catalog lookup
- No production trace capture path exists

## What Changes

## Scope

- TC-00: OpenSpec (this change)
- TC-01: Extract reusable Harness contracts into `UniClaw.Runtime.Harness`
- TC-02: In-memory TraceCaptureSession lifecycle
- TC-03: Append-only filesystem persistence via ITraceCaptureStore
- TC-04: Physical artifact attachment (screenshots, perception, Observation)
- TC-05: Immutable ScenarioCatalog with reference validation
- TC-06: Golden run migration into canonical Scenario representation
- TC-07: Closeout and graduation

## Exclusions

- No Agent/Container/Traversal changes
- No Provider framework, registry, or plugin system
- No Planner, Brain, VLM, or intent routing
- No automatic trace-to-expected-behavior conversion
- No remote/cloud trace service

## Capabilities

### New Capabilities

- `trace-capture-lifecycle`: Harness-owned, failure-isolated capture of ordered observations, actions, results, artifacts, and Runtime trace snapshots.
- `trace-capture-persistence`: Append-only atomic local persistence for immutable capture bundles.
- `scenario-catalog`: Immutable explicit Scenario lookup with schema, provenance, hash, and reference validation.
- `catalog-driven-replay-regression`: Explicit catalog replay through the existing Runtime environment boundary with fail-closed behavior assertions.

### Modified Capabilities

- None. The Runtime semantic model, Agent Trace, GoalEvidence, and `IEnvironment` contract remain unchanged.

## Authorization

Planning artifacts are authorized by the approved architecture gate. Human Apply authorization for TC-01 through the validation portion of TC-07 was granted on 2026-08-22. Graduation decision and archive remain separately gated.
