## Why

The accepted visible locate run proved that the target click can be verified deterministically from a stabilized screenshot plus UIAutomator toolbar identity; invoking the remote visual model again after the click adds minutes of latency and introduces a false-negative variance point without adding required information. The same run also showed that synchronous per-record trace and asset persistence sits on the traversal path, so evidence durability must be preserved while moving eligible I/O off the critical path.

This is a **high-priority prerequisite** for further emulator enumeration and stability work: P0 removes the redundant post-action model call, and P1 introduces an ordered, bounded asynchronous evidence pipeline with explicit durability barriers.

## What Changes

- **P0 — deterministic post-action verification:** after a successful locate target action, wait for Android stabilization, capture the final screenshot and UIAutomator hierarchy once, derive the destination identity from deterministic toolbar/title resources, and verify it against scenario aliases. Do not call the remote visual provider for this final acceptance step.
- Preserve `after.png`, `after.xml`, the derived identity, and their correlation fields as the success evidence referenced by `result.json` and trace.
- Treat missing screenshot, failed UIAutomator refresh/parse, missing trusted title, or identity mismatch as explicit verification failure; never fall back to a model call or overstate success.
- **P1 — asynchronous evidence submission:** add a Host-owned bounded, run-scoped submission pipeline for eligible screenshots, XML, normalized analysis, trace records, and non-critical step assets. Producers enqueue immutable payloads; one ordered writer persists them outside the traversal critical path.
- Keep safety-before-action durability, run creation, and authoritative final result as synchronous/write-through barriers. Before success, failure, or cancellation finalization, drain and flush all accepted evidence.
- Apply backpressure when the queue is full; never silently drop trace, safety, verification, or screenshot evidence. Propagate writer failure as trace/reporting failure and prevent a successful result.
- Preserve the existing Core `ITraceRecorder` / `ITraceStorage` contracts and locked enum/interface counts; asynchronous orchestration remains in Host.
- Add latency, ordering, backpressure, cancellation-drain, writer-failure, and zero-post-click-model-call acceptance tests plus one explicit emulator locate verification.

## Capabilities

### New Capabilities

- `deterministic-post-action-verification`: Defines provider-free locate completion verification from a stabilized screenshot and trusted UIAutomator page identity.
- `asynchronous-run-evidence`: Defines bounded, ordered, lossless asynchronous submission for Host run assets and durable trace, including durability barriers and failure semantics.

### Modified Capabilities

<!-- No canonical Core capability changes. The Host composes existing page, ADB,
     trace-storage, and scenario seams without changing their public contracts. -->

## Impact

- `src/UniClaw.Host/Commands/HostCommands.cs`: replace the final locate `AnalyzeCurrentPageAsync` call with deterministic post-action capture/UIAutomator verification; compose and flush the run evidence pipeline.
- `src/UniClaw.Host/Hooks/RunAssetHook.cs` and `src/UniClaw.Host/Artifacts/`: submit immutable step assets asynchronously while retaining stable after-action evidence and finalization barriers.
- `src/UniClaw.Host/Observability/`: replace synchronous durable trace mirroring on the traversal path with a Host-owned asynchronous durable mirror while keeping the in-memory read model immediate.
- `src/UniClaw.Host/Verification/ScenarioCompletionVerifier.cs`: consume a trusted deterministic final identity/evidence object rather than a remote-model `PageAnalysis` for locate completion.
- `tests/UniClaw.Host.Tests/`: add deterministic verifier, queue ordering/backpressure/flush/failure/cancellation, trace durability, and external-call-count tests.
- `docs/system/layers/host.md`, `docs/system/layers/observability.md`, `docs/system/patterns/system-orchestration.md`, and `docs/testing/integration-tests.md`: document the final-verification boundary and asynchronous durability lifecycle.
- No Core → Host/Device/provider reverse dependency, no locked enum change, and no `ITraceRecorder`/`ITraceStorage` surface change.
