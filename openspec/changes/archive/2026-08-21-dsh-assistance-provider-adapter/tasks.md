# Tasks: dsh-assistance-provider-adapter

> System of record. IMPLEMENTED (APPLY gate executed 2026-08-17).
> **State**: cross-process assistance provider adapter, deterministic consumer,
> and regression/E2E coverage implemented; pending graduation review (no
> self-archive).

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — Verified source baseline (seam implemented + zero providers; plugin
      ctx.llm seam precedent; one-direction connection; 9-method wire table;
      contract Plane 3)
- [x] Slice 2 — Cross-process direction decision (pending queue + poll + resolve
      over existing DSH→DriverHost direction; reverse-listener option rejected;
      event-push option rejected)
- [x] Slice 3 — Wire design (assistance.pending request digest; assistance.resolve
      echo/world-version validation + whitelist normalization; boundedness:
      capacity 8 + timeout 30s)
- [x] Slice 4 — DriverHost-side provider (AssistanceWireProvider over the pending
      registry; composition-root injection; null = zero regression)
- [x] Slice 5 — Plugin-side assistance service (bounded poll + ctx.llm advice
      generation + resolve; never writes Runtime state)
- [x] Slice 6 — Authority/isolation clauses + test plan T1–T9 (apply)
- [x] Validation — openspec validate --strict, check-consistency.sh, seam +
      contract + mother-doc cross-check

## Implementation plan (APPLY gate — EXECUTED 2026-08-17; model-free)

- [x] A1 — DriverHost: `AssistancePendingRegistry` (capacity 8 COMPOSITION_POLICY;
      lifecycle PENDING→RESOLVED/EXPIRED; requestId/worldVersion/whitelist
      validation; atomic consume+remove) + `AssistanceWireProvider : IAssistanceProvider`
      (bounded await, default timeout 30s COMPOSITION_POLICY, fail-closed) +
      wire methods `assistance.pending` / `assistance.resolve` (additive;
      `UniClawAssistanceWire` codec; `IAssistanceWireSurface` on the server)
- [x] A2 — Composition: `PhysicalHostComposition.BuildRuntimeGraph` /
      `CreateAndroidRunGraphFactory` accept `IAssistanceProvider?` (null = existing
      null-provider immediate fail-closed); `BuildDriverHostServer` wires a SHARED
      registry across wire provider + wire surface; Runtime.Agent unchanged
- [x] A3 — Plugin `AssistanceBridge` (provider-agnostic): poll + normalize +
      consumer port + translate + resolve; duplicate requestId suppression;
      reconnect-safe; ZERO llm/model references (static guard passes)
- [x] A3b — `DeterministicAssistanceConsumer` (model-free, replaceable; whitelist
      normalization; fixture mapping Settings → re-observe)
- [x] A4 — Tests: provider/registry lifecycle (10), bridge/consumer (8 node),
      cross-process E2E (1: real server + provider + node bridge + deterministic
      consumer → consult → re-observe → SAME goal → completed), seam regression

## Implementation evidence

- [x] `AssistanceWireProviderTests` 10/10: correlated resolve; mismatched
      requestId reject; stale worldVersion reject; invalid recommendation reject;
      abandon (null advice); timeout fail-closed; cancellation fail-closed;
      capacity overflow fail-closed; duplicate/late resolve cannot resurrect;
      repeated pending poll harmless
- [x] `assistance-bridge.test.mjs` 8/8: full poll→consumer→resolve flow; duplicate
      suppression; reconnect-safe; consumer port replaceable (stub); deterministic
      mapping; whitelist suppression; STATIC GUARD zero llm/model deps
- [x] `DriverHostAssistanceE2ETests` 1/1: real cross-process path
      (Runtime.Agent → ConsultAsync → WireProvider → pending → node bridge →
      deterministic consumer → resolve → re-observe → fresh world → SAME goal →
      Completed; MODEL-FREE)
- [x] `AssistanceSeamTests` 7/7 regression (null provider zero regression)
- [x] Availability semantics: provider absent → immediate fail-closed (seam null
      path); provider present but harness/consumer unavailable → bounded timeout
      (30s COMPOSITION_POLICY) → null → fail-closed, never hangs, never fabricates
- [x] Wire compatibility: frozen 9 methods untouched; 2 additive methods (F16
      node guard updated to frozen 8 + run.start + assistance.pending/resolve)

## Falsifier mapping (gate §Proposal)

- [x] F1 — no reverse connection (poll/resolve over existing direction)
- [x] F2 — LLM/model confined to the plugin AND only as an optional consumer
      implementation (bridge provider-agnostic; DriverHost/Runtime zero model refs)
- [x] F3 — resolve writes only the pending reply (never Kernel state)
- [x] F4 — advice ≠ truth/authorization/goal-completion
- [x] F5 — bounded consult (capacity + timeout as COMPOSITION_POLICY;
      overflow/timeout ⇒ fail-closed)
- [x] F6 — frozen wire semantics preserved (additive only)
- [x] F7 — Runtime seam untouched (already implemented; this gate changes nothing there)
- [x] F8 — no new RuntimeEvent kinds/emitters
- [x] F9 — wire request is capability-gap context, not a model prompt
- [x] F10 — all repository-reality claims verified at source
- [x] F11 — adapter is not the intelligence decision layer (bridge owns no
      semantic policy/routing/recovery; intelligence selection = Harness consumer)
- [x] F12 — first APPLY is model-free (fake/deterministic consumer; real model
      consumer attached independently later)
