# Trace Capture and Scenario Catalog Foundation

> Status: PROPOSED
> Architecture Gate: APPROVED (trace-capture-scenario-catalog-architecture-gate.md)
> Baseline: 726fb9a (golden semantic run graduated)

## Summary

Establish Harness-owned TraceCaptureSession, persistence store, ScenarioCatalog, and catalog-driven replay regression. Zero Runtime semantic delta.

## Motivation

Current gaps:
- `PhysicalEnvironment` retains only in-memory histories — no durable capture
- Golden replay constructs minimized Observations in C# with ad-hoc trace JSON
- No canonical Scenario manifest or catalog lookup
- No production trace capture path exists

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
