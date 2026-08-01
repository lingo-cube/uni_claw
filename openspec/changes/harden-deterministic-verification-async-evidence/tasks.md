> **Priority gate:** P0 and P1 in this change SHALL be applied before resuming
> `scenario-enumerate`, legacy-runner deletion, or emulator stability gates in
> the active `runner-through-engine` and `deliver-safe-android-settings-test-loop`
> changes.

## 1. P0 Baseline and Semantic Impact Audit

- [ ] 1.1 Run the current non-integration baseline and record the exact command, pass/skip counts, duration, and existing warnings before changing implementation.
- [ ] 1.2 Use the configured C# semantic-navigation service to resolve definitions and references for `HostCompositionFactory.RunScenarioAsync`, `ScenarioCompletionVerifier`, `RunAssetHook`, `UiAutomatorPageAnalysis`, `ITraceRecorder`, `RunAssetSession`, and the current durable trace mirror; record the implementation touchpoints and confirm no partial declarations were missed.
- [ ] 1.3 Add or identify a deterministic provider-call-count test seam that distinguishes model calls before the target action from any call attempted after it.
- [ ] 1.4 Confirm the accepted locate fixture exposes a trusted UIAutomator toolbar/title resource and capture the exact resource suffixes and expected aliases used by the deterministic verifier.

## 2. P0 Provider-Free Locate Completion

- [ ] 2.1 Add an immutable `PostActionEvidence` Host record carrying run ID, step number, screenshot bytes/path, UI XML/path, trusted identity, hierarchy fingerprint, and timestamp without changing locked Core records or enums.
- [ ] 2.2 Implement a post-action evidence collector over `IObservableScreenStateProvider` + `IScreenCapture` that performs bounded stabilization and never depends on `IPageAnalyzer` or a model provider.
- [ ] 2.3 Restrict final identity extraction to configured trusted Settings toolbar/title resource suffixes; reject generic `Settings`, arbitrary content text, empty titles, malformed XML, and untrusted resource IDs.
- [ ] 2.4 Implement cancellation and timeout handling for stabilization, retaining the last diagnostic screenshot/XML when available and classifying failure without model fallback.
- [ ] 2.5 Refactor `ScenarioCompletionVerifier` locate verification to consume `PostActionEvidence`, require target action success + trusted alias match, and emit correlated deterministic success evidence.
- [ ] 2.6 Rewire `HostCompositionFactory.RunScenarioAsync` so the final locate gate calls the deterministic collector, performs zero post-target `AnalyzeCurrentPageAsync`/provider calls, and refreshes the final step's `after.png`/`after.xml` from the accepted evidence.
- [ ] 2.7 Persist a final verification envelope containing the derived identity, match result, resource evidence, fingerprint, and correlation fields; ensure `result.json` references only the stabilized screenshot/XML and trace paths.
- [ ] 2.8 Add focused tests for alias match, mismatched title, generic-title rejection, untrusted resource rejection, malformed/missing XML, screenshot failure, stabilization timeout, and cancellation.
- [ ] 2.9 Add a composition-level test proving the provider call count does not increase after the successful target action while `result.json` still reports the deterministic identity and evidence paths.
- [ ] 2.10 Run `scenario-locate` once on the visible fixed emulator with recording enabled; retain the run ID, stabilized screenshot/XML, provider call timeline, trace, and successful xUnit result before starting P1 integration.

## 3. P1 Bounded Asynchronous Evidence Pipeline

- [ ] 3.1 Define Host-owned `IRunEvidencePipeline`, immutable evidence envelope/work-item types, configuration (capacity and shutdown budget), and telemetry (accepted count, maximum depth, backpressure count, flush duration, writer fault) without modifying Core trace interfaces.
- [ ] 3.2 Implement a bounded single-reader channel that assigns monotonic per-run sequences, preserves accepted order, awaits capacity on saturation, and prohibits all drop modes and fire-and-forget writes.
- [ ] 3.3 Enforce immutable payload ownership by copying or exclusively transferring screenshot/XML/JSON buffers at submission; apply existing asset redaction before durable text/JSON persistence.
- [ ] 3.4 Route eligible `RunAssetHook` before/after screenshots, UI XML, analysis, verification, and issue assets through the pipeline while preserving step causal sequence and correlation fields.
- [ ] 3.5 Keep manifest, scenario snapshot, plan inputs, pre-action step-plan/safety decision, and terminal `result.json` on explicit durable barriers; prove the ADB executor is not called before required safety evidence is durable.
- [ ] 3.6 Replace the synchronous durable trace mirror on the scenario path with a Host trace recorder/wrapper that updates `InMemoryTraceStorage` immediately and submits the matching `FileTraceStorage` record asynchronously.
- [ ] 3.7 Implement trace session lifecycle so the durable directory/session start exists before traversal, session end is queued in order, and analyzer reads continue from the immediate in-memory model.
- [ ] 3.8 Implement terminal completion order: stop new submissions → drain accepted envelopes → surface writer fault → verify referenced assets → synchronously finalize `result.json`.
- [ ] 3.9 Make the first writer exception sticky across later submission/flush calls; classify the run as trace/reporting failure and write a redacted best-effort fallback result without claiming success.
- [ ] 3.10 Implement cancellation shutdown that stops producers, drains accepted evidence with a bounded independent token, awaits the writer, reports drain timeout honestly, and leaves no worker task alive.
- [ ] 3.11 Add deterministic slow-writer tests proving unsaturated submission returns before disk completion, `FlushAsync` waits, full-queue backpressure blocks, and no item is dropped or reordered.
- [ ] 3.12 Add payload ownership/redaction tests plus writer-failure tests covering queued work, sticky fault propagation, fallback result, and zero false success.
- [ ] 3.13 Add cancellation tests for healthy drain, drain timeout, cancellation during backpressure, and no orphan background writer.
- [ ] 3.14 Add trace tests proving immediate analyzer visibility, ordered durable JSONL/session output, durable flush before success, and consistency between in-memory record counts, JSONL lines, and result references.
- [ ] 3.15 Add run-asset tests proving final screenshot/XML and all step files are readable only after flush, two runs remain isolated, and finalization cannot race pending writes.

## 4. P1 Integration, Performance Evidence, and Failure Drills

- [ ] 4.1 Add pipeline telemetry to run diagnostics without adding `SpanType` or changing any locked enum/interface count.
- [ ] 4.2 Run focused Host tests for deterministic verification, assets, trace, safety-before-action, analyzer, cancellation, and command finalization; record exact results.
- [ ] 4.3 Drill a slow durable writer and verify traversal progresses while capacity remains, backpressure activates at capacity, flush dominates only terminal latency, and all evidence remains ordered.
- [ ] 4.4 Drill screenshot/XML write failure, trace append failure, queue writer failure, flush timeout, and Ctrl+C; verify each produces the specified recoverable assets and non-success classification.
- [ ] 4.5 Re-run `scenario-locate` on the fixed visible emulator and retain a detailed before/after latency report including total provider calls, zero post-target provider calls, queue high-water mark, backpressure count, flush duration, trace line count, and asset count.
- [ ] 4.6 Inspect every final locate artifact and recording, verifying the trusted title, stabilized screenshot, result evidence paths, safety decisions, FSM trace, and durable JSONL agree before unblocking downstream emulator tasks.

## 5. P2 Documentation and Final Validation

- [ ] 5.1 Produce the mandatory Tier 1/2/3 documentation sync checklist; confirm Tier 1 locked enums/interfaces are unchanged, then update Host/Observability layer docs and system-orchestration pattern for the deterministic verifier and asynchronous durability lifecycle.
- [ ] 5.2 Update `docs/testing/integration-tests.md` with the provider-free final locate gate, queue/flush diagnostics, failure triage, and explicit rule that this scope runs only for affected Host/ADB/trace changes.
- [ ] 5.3 Run `python openspec/hooks/doc_sync_hook.py` and resolve all Tier 1/2/3 sync findings; defer Tier 4 decisions and canonical spec synchronization to archive.
- [ ] 5.4 Run the full non-integration suite, `dotnet build src/UniClaw.Core.sln`, architecture guards, and targeted Host tests with zero new errors; record exact pass/skip/warning counts.
- [ ] 5.5 Run `openspec validate harden-deterministic-verification-async-evidence`, inspect `git diff --check`, and attach final command results plus accepted run/video IDs before marking apply complete.

## Design Docs

> Derived from the proposal Impact section. Implementation agents must read
> these documents before starting the corresponding task group.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Host/Commands/` | `docs/system/layers/host.md` + `docs/system/patterns/system-orchestration.md` |
| `src/UniClaw.Host/Hooks/` and `src/UniClaw.Host/Artifacts/` | `docs/system/layers/host.md` + `docs/testing/integration-tests.md` |
| `src/UniClaw.Host/Observability/` | `docs/system/layers/observability.md` + `docs/system/layers/host.md` |
| `src/UniClaw.Host/Verification/` | `docs/system/layers/host.md` + `docs/system/patterns/system-orchestration.md` |
| `tests/UniClaw.Host.Tests/` | `docs/system/layers/simulation.md` + `docs/system/layers/simulation-baseline.md` + `docs/testing/integration-tests.md` |
