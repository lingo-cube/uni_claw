## Context

The visible locate acceptance run currently performs a remote `AnalyzeCurrentPageAsync` after the target click solely to obtain a destination identity. The accepted evidence already contains the authoritative Android surface: a stabilized `after.png` and UIAutomator XML whose trusted toolbar resource identifies `About emulated device`. The extra model request adds latency and can report a path that conflicts with the actual device state.

Run assets and trace are also produced inside the engine lifecycle. The analyzer needs trace immediately, while durable JSONL and screenshot/XML writes need only be complete before final result publication. The current synchronous durable mirror proves correctness but unnecessarily couples file latency to traversal latency.

Constraints:

- Core `ITraceRecorder`, `ITraceStorage`, and locked interface/enum counts remain unchanged.
- Host owns provider selection, Device composition, run assets, deterministic success semantics, and asynchronous orchestration.
- Safety evidence required before a real action and the authoritative `result.json` remain write-through barriers.
- A successful result must never reference evidence that has not been durably flushed.
- The change blocks subsequent enumerate/stability work and is prioritized P0/P1.

## Goals / Non-Goals

**Goals:**

- Eliminate the post-target remote visual call from locate completion.
- Produce one correlated deterministic final-evidence object containing stabilized screenshot, UIAutomator XML, trusted title identity, fingerprint, timestamp, run ID, and step number.
- Move eligible step assets and durable trace writes off the traversal critical path through a bounded, ordered Host pipeline.
- Preserve immediate in-memory trace queries for `VerificationAnalyzer`.
- Guarantee lossless drain/flush before terminal result publication and honest failure classification when persistence fails.
- Prove the behavior with external-call-count, ordering, backpressure, cancellation, writer-failure, and emulator evidence.

**Non-Goals:**

- Removing the visual model from initial page understanding, target discovery, or other scopes that explicitly test vision.
- Changing Core trace interfaces, record schemas, locked enums, FSM states, or traversal transitions.
- Making safety-before-action persistence eventual.
- Adding distributed queues, databases, cross-process recovery, uploads, or retention/deletion policy.
- Optimizing enumerate behavior in this change.

## Decisions

### D1 — Locate completion uses a dedicated deterministic evidence collector

Host introduces a small post-action collector that uses only `IObservableScreenStateProvider` and `IScreenCapture`. After the target action succeeds it waits within a bounded stabilization budget, obtains a parseable UIAutomator hierarchy, requires a non-generic title from a trusted Settings toolbar/title resource, captures the final screenshot, and returns an immutable `PostActionEvidence` value.

`ScenarioCompletionVerifier` matches the normalized deterministic identity against the scenario's expected identities/aliases and references the correlated screenshot/XML paths. No `IPageAnalyzer` or provider method is invoked after the target action.

Rationale: UIAutomator toolbar identity is the platform-owned deterministic signal, while the screenshot is the human-auditable proof. A second model judgment is redundant and less stable.

Alternatives considered:

- Keep the final model call and use UIAutomator only as fallback — rejected because it preserves latency and variance.
- Accept from screenshot alone — rejected because a bitmap is evidence but not a deterministic machine-verification mechanism.
- Reuse the immediate hook screenshot — rejected because Android navigation can render after `OnAfterStep` returns.

### D2 — Stabilization is bounded and deterministic

The collector polls UIAutomator at a short configurable interval until it observes a trusted non-generic title with a stable hierarchy/title observation, or until the scenario reset/verification timeout expires. It then captures the final screenshot and retains the final XML. Failure to obtain a trusted title or screenshot is a verification/device failure; there is no visual-model fallback.

Rationale: a fixed sleep is simple but device-load dependent. Bounded polling reduces false negatives without adding provider latency.

### D3 — Host uses a bounded single-writer evidence pipeline

Host introduces a run-scoped `IRunEvidencePipeline` backed by a bounded channel. Producers submit immutable envelopes with a monotonically increasing run sequence. A single background consumer writes them in accepted order. Normal submission completes after the item is accepted, not after disk persistence; when capacity is exhausted, submission awaits capacity and applies backpressure.

Eligible payloads include screenshots, UI XML, normalized analysis, verification envelopes, issues, and durable trace records. Payload bytes are copied or ownership-transferred at submission so later producer mutation cannot change persisted evidence. Text/JSON follows the existing redaction policy.

Rationale: one writer preserves causal ordering and avoids per-record task fan-out. A bounded queue prevents unbounded screenshot memory growth. `DropOldest`, `DropNewest`, and fire-and-forget writes are prohibited.

Alternatives considered:

- Unbounded channel — rejected because screenshots can exhaust memory during long runs.
- One task per write — rejected because ordering, failure aggregation, and shutdown become nondeterministic.
- Make Core `ITraceStorage` async — rejected because it changes a locked architectural seam and is unnecessary for Host orchestration.

### D4 — Trace has an immediate read model and an asynchronous durable mirror

A Host `ITraceRecorder` implementation/wrapper updates `InMemoryTraceStorage` synchronously before submitting the corresponding durable record to the evidence pipeline. `VerificationAnalyzer` therefore sees every accepted record immediately, while `FileTraceStorage` work occurs on the single writer.

`StartSessionAsync` establishes both the in-memory session and durable directory before traversal. `EndSessionAsync` submits the terminal session update but does not replace the final run flush barrier.

Rationale: this preserves current analyzer semantics and existing Core interfaces while removing filesystem append latency from the normal trace hot path.

### D5 — Critical durability barriers remain explicit

The following remain synchronous/write-through or are awaited to durable acknowledgement:

- run directory, manifest, scenario snapshot, policy/plan inputs at run start;
- safety decision and step plan required before sending a real device action;
- pipeline completion/flush before terminal result publication;
- authoritative `result.json`, written only after flush succeeds or written best-effort with a reporting-failure status when flush fails.

The terminal sequence is:

1. stop accepting new engine evidence;
2. drain the queue in sequence;
3. surface any writer error;
4. verify referenced evidence exists;
5. write the terminal `result.json` synchronously.

Rationale: asynchronous submission is a latency optimization, not permission to weaken causal safety or result honesty.

### D6 — Writer failure is sticky and prevents success

The first durable-writer exception is stored as the pipeline terminal fault. All subsequent submissions and flush return that fault. Host classifies the run as `trace/reporting failure`; it must not emit `success`, even if the device action and page verification succeeded. A best-effort synchronous fallback result records the fault without exposing secrets.

Cancellation stops scheduling new work but drains already accepted evidence under a bounded shutdown token. Timeout or drain failure is recorded as incomplete evidence in the cancelled/failure result. The worker is always awaited; no background task may survive the run.

### D7 — Performance is proven structurally, then measured on the emulator

Unit tests use a controllable slow writer to prove that traversal-side submission returns after queue acceptance while durable completion waits for `FlushAsync`. Integration evidence records provider call count, queue high-water mark, backpressure count, flush duration, and total scenario duration. No environment-sensitive millisecond threshold is a unit-test gate; the external locate run must show zero post-target provider calls and complete durable assets.

## Risks / Trade-offs

- [Risk] Queue saturation reintroduces latency through backpressure. → Mitigation: bounded capacity is configurable and metrics expose high-water/backpressure; lossless behavior has priority over speed.
- [Risk] Screenshot/XML can refer to slightly different frames. → Mitigation: require stable UI identity first, then capture the final screenshot immediately and persist shared correlation metadata.
- [Risk] A durable writer fails after the device action succeeded. → Mitigation: sticky reporting failure prevents success and best-effort result finalization preserves diagnosis.
- [Risk] Cancellation races with queued writes. → Mitigation: stop producers, drain accepted items with a dedicated bounded shutdown token, then await the worker.
- [Risk] Updating memory before durable submission creates a temporary durability gap. → Mitigation: terminal success is forbidden until the durable queue flushes; analyzer output is provisional until that barrier.
- [Risk] Existing asset APIs mix synchronous file methods with async signatures. → Mitigation: adapt them behind the Host pipeline incrementally; do not modify Core storage contracts.

## Migration Plan

1. Land the deterministic `PostActionEvidence` collector and replace final locate model verification; keep current synchronous persistence.
2. Prove zero post-target provider calls and run `scenario-locate` once on the visible emulator.
3. Introduce the bounded evidence pipeline with fake-writer tests, initially behind Host composition only.
4. Route eligible run assets through the pipeline while retaining critical write-through barriers.
5. Replace synchronous durable trace mirroring with immediate-memory + asynchronous-file recording.
6. Add flush/fault/cancellation metrics and terminal result gating.
7. Re-run focused tests, non-integration baseline, build/architecture guards, then the explicit locate scope. Only after this change passes may enumerate/stability work resume.

Rollback is staged: deterministic verification can remain even if asynchronous persistence is temporarily disabled; Host can compose the existing synchronous writer without changing Core or asset schemas.

## Open Questions

- Select the initial queue capacity from measured screenshot/XML sizes; default recommendation is 32 envelopes with configuration override.
- Decide whether issue JSONL stays write-through with safety decisions or joins the async queue with terminal flush. The conservative initial implementation keeps safety write-through and permits issues in the ordered queue.
- Decide whether stabilization requires two equal trusted titles or two equal hierarchy fingerprints; the implementation should prefer title stability and retain the final hierarchy fingerprint as evidence.
