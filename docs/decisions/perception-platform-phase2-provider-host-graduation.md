# Perception Platform Phase 2 — Provider Host Graduation

> Date: 2026-08-12
> Status: GRADUATED
> Baseline: 8b2541c (H5+H6 final behavioral closure)
> Regression: 857/857

## Decision: GRADUATED

The Perception Platform Phase 2 Provider Host is graduated as frozen production infrastructure.

## Frozen Ownership

`VisionServiceHost` is the sole mutable owner of Python Vision service lifecycle state. No competing lifecycle ownership exists in Runtime, Adapters, Harness, or Python service.

## Frozen Authority

Host MAY: validate prerequisites, launch/stop Python, monitor process, poll health, negotiate schema, record deployment facts, restart within budget, manage UDS path.

Host MUST NOT: retry DeviceAction, trigger Agent Recovery, replan, mutate Container/GoalEvidence, select Capability, make semantic decisions, determine Runtime completion.

## Frozen Runtime Boundary

`UniClaw.Runtime` does NOT depend on `UniClaw.Vision.Host`. IEnvironment, ObservedElement, and all semantic contracts unchanged. Runtime remains ignorant of Python, YOLO, RapidOCR, UDS, model files, config files, and Vision deployment lifecycle.

## Frozen Failure Semantics

Vision unavailable → Adapter returns [] → Runtime UNKNOWN → no fabricated evidence. Operational failure remains separate from semantic failure. All H2-H17 falsifiers support this boundary.

## Frozen Process Topology

SINGLE_PROCESS, SINGLE_WORKER. States: Cold → Warming → Healthy / Unhealthy / Crashed → Shutdown. Readiness explicit, shutdown idempotent, restart bounded (3/60s sliding window), socket ownership-safe, cross-Host isolation proven.

## Frozen Schema Compatibility

Adapter/service compatibility = supportedSchemas intersection. modelId and configHash are operational identity/drift facts, NOT semantic compatibility gates. No coupled Runtime/Vision release requirement.

## Frozen Model Identity

modelId = full 64-character SHA-256 of model artifact content. Content-addressed, path-independent, filename-independent. P4 proves same content → same identity, changed content → different identity.

## Config Status

configHash = SHA-256(label-mapping.json) — truthful but PARTIAL. Deferred to Phase 4: PerceptionConfigManifest, configId, effective configuration identity.

## Python Service Status

Phase 2 intentionally did NOT migrate, refactor, or modify Python service internals. Only approved delta: UNICLAW_VISION_SOCKET env var. Phase 3 owns migration/refactor.

## Test Evidence

All 19 falsifiers (H1-H18 + P4) have executable behavioral evidence. 37 Host tests, 8 Python tests, 857 full regression, architecture guards pass, golden replay compatible. Live physical validation: NOT_EXECUTABLE (no device).

## Deferred

- Phase 3: Python service migration, layout normalization, server.py bounded refactor
- Phase 4: configId, ModelRegistry, training/dataset provenance, promotion gates, rollback

## Frozen Guard Rules

- Runtime → Vision.Host: FORBIDDEN
- Vision.Host → Runtime semantic internals: FORBIDDEN
- Runtime → Python/YOLO/OCR/model/config: FORBIDDEN
