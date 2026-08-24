# uniclaw-driverhost-production-server-mode

**Status**: IMPLEMENTED (APPLY gate 2026-08-19). **State**: production `--serve`
mode, options, and IntegrationTestHost implemented; targeted + DriverHost +
RuntimeAgent tests green; manual serve-mode validated. Pending graduation review
(no self-archive).

## One-line

Add a `--serve` mode to the production entry point (`Program.cs`) that starts the existing DriverHost server via `PhysicalHostComposition.BuildDriverHostServer()`, remains alive until process cancellation/termination, and disposes cleanly — making frozen Protocol v1 surfaces servable from the real production process. No new architecture, no new protocol, no new abstraction.

## Buyer

`RUNTIME_PROTOCOL_PRODUCTION_SERVABILITY` — infrastructure required by the future UniAgent orchestration vertical slice. NOT itself a UniAgent implementation.

## Explicit non-identity

DriverHost production server mode ≠ UniAgent ≠ PhysicalHost ≠ architecture layer. It is the current production transport/runtime ingress needed by a later UniAgent buyer.

## Frozen baselines protected

- Architecture v1: `docs/architecture/uniagent-architecture-v1-core-development-guide.md`
- Protocol v1: `docs/architecture/uniagent-protocol-v1-consolidation-design.md`
- Canonical index: `docs/architecture/README.md`
- All 19 Architecture v1 invariants + 23 Protocol v1 invariants protected.
- No frozen/reserved capability accidentally purchased.
