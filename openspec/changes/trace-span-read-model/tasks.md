## 1. Sol Contract Gate

- [x] 1.1 **Owner: Sol** — Re-read the proposal, both delta specs, and design; record the apply-time `PROJECT_CONTEXT_RESOLUTION`; stop if implementation would add a wire operation, Runtime dependency, inferred lookup, or semantic authority.
- [x] 1.2 **Owner: Sol** — Freeze the exact query/result/cursor/filter vocabulary and the typed persisted-read outcome vocabulary before production edits; require cursor binding to `runId` + finalized `TraceRunId` + filter fingerprint.
- [x] 1.3 **Owner: Sol** — Define failing contract tests proving explicit identity, typed absence, deterministic order, cursor mismatch, filter rejection, span-outcome/Runtime-result separation, and the authority firewall.

## 2. Luna Worker A — In-Process Trace/Span Read Model

- [x] 2.1 **Owner: Luna / Files: `src/UniClaw.Runtime.DriverHost/` read-model contracts only** — Add immutable trace-summary, span-envelope, cursor, typed filter, page, and discriminated query-result records without changing existing Runtime or wire DTOs.
- [x] 2.2 **Owner: Luna / Files: DriverHost observability projection/store only** — Project finalized spans in deterministic `(StartOffsetNs, SpanId)` order, assign one-based read sequences, bind cursors, and implement exact conjunctive filters.
- [x] 2.3 **Owner: Luna / Files: DriverHost read-only observability surface only** — Expose summary and paged-span queries for one explicit registered `runId`; return typed unavailable/mismatch results while keeping the closed `IUniClawControlSurface` method set and transport dispatch unchanged.
- [x] 2.4 **Owner: Luna / Files: corresponding DriverHost unit tests only** — Implement the Sol-approved query tests, including repeated reads, page exhaustion, equal-offset tie breaking, filter preservation, unknown run, pre-finalization absence, and immutable source proof.
- [x] 2.5 **Owner: Sol review gate** — Independently review Worker A's diff for query determinism, raw-object leakage, wire-surface changes, semantic inference, and any path from a read to Runtime mutation; reject or request changes before integration.

## 3. Luna Worker B — Persisted Capture Reader

- [x] 3.1 **Owner: Luna / Files: `src/UniClaw.Runtime.Harness/Capture/` read contracts only** — Add a separate reader contract with discriminated found/not-found/trace-absent/compatibility/mismatch/validation results and typed validation issues.
- [x] 3.2 **Owner: Luna / Files: Harness capture filesystem read path only** — Implement explicit safe `CaptureSessionId` lookup, published-directory checks, immutable bundle reconstruction, optional exact `TraceRunId` matching, and no scanning or repair.
- [x] 3.3 **Owner: Luna / Files: Harness-internal validation only** — Extract or reuse pure schema/identity/order/artifact/checksum/TraceRun validation without weakening the existing append-only staging/save lifecycle or claiming JSON cryptographic integrity.
- [x] 3.4 **Owner: Luna / Files: corresponding Harness unit tests only** — Cover valid trace-attached and trace-absent captures, artifact byte reconstruction, unsafe IDs, staging/symlink escape, malformed JSON, unsupported schema, ID mismatch, record disorder, missing/unknown checksum entries, missing artifact, hash/byte mismatch, and invalid trace hierarchy.
- [x] 3.5 **Owner: Sol review gate** — Independently review Worker B's diff for canonical-root containment, indirection handling, whole-read fail-closed behavior, compatibility honesty, publication immutability, and Runtime/Harness ownership.

## 4. Luna Worker C — Mechanical Guards and Regression Evidence

- [x] 4.1 **Owner: Luna / Files: architecture tests only** — Add guards proving Runtime/Agent/Container/Traversal/Recovery/Environment do not reference the read model/reader and the DriverHost wire method/DTO set is byte-for-behavior unchanged.
- [x] 4.2 **Owner: Luna** — Run targeted query/reader tests, existing observability and trace-capture tests, DriverHost wire/control tests, Architecture Guards, and deterministic SETTINGS-TREE/OpenWorld/RuntimeAgent Phase 1–4 regressions; preserve unrelated device limitations as explicit limitations.
- [x] 4.3 **Owner: Luna** — Run `dotnet build src/UniClaw.Runtime.sln`, `scripts/check-consistency.sh`, `openspec change validate trace-span-read-model --strict`, and `git diff --check`; report exact counts and unrelated failures without editing around them.

## 5. Sol Final Verification

- [x] 5.1 **Owner: Sol** — Inspect production diff independently and prove `Agent.RunOpenWorldAsync`, lifecycle/FSM/Traversal/GoalEvidence/Recovery authority, activity emission, capture save semantics, and all current Protocol v1 wire operations are unchanged.
- [x] 5.2 **Owner: Sol** — Re-run high-risk targeted tests and guards, classify every remaining failure as product regression or environment/unrelated limitation, and issue PASS / NEEDS_CHANGE / FAIL for implementation completion.
- [x] 5.3 **Owner: Sol** — Update task receipts only after evidence exists; do not graduate or archive this change without a separate explicit graduation decision.

## Apply Receipt — 2026-08-22

- **Implementation decision:** PASS for `trace-span-read-model`; no Strategy/Runtime planning authority, DeviceAction, FSM, GoalEvidence, Recovery, Run lifecycle, Scenario selection, MultiRun, or wire-protocol path was introduced.
- **Surface boundary:** Trace summary/span paging is available only through `IReadOnlyObservability` / `DriverHostObservability`; the closed `IUniClawControlSurface` and Protocol v1 dispatch/DTO sets remain unchanged.
- **Persisted boundary:** `ITraceCaptureReader` reads one explicit published `CaptureSessionId`, validates the whole capture fail-closed, preserves optional caller-owned external parent context, and performs no scan, repair, replay, catalog, Runtime, or persistence mutation.
- **Build:** `dotnet build src/UniClaw.Runtime.sln --no-restore` — PASS, 0 warnings, 0 errors.
- **Targeted capability tests:** read model + persisted reader + architecture guards — PASS, 80/80; persisted reader alone — PASS, 58/58.
- **Existing support regressions:** observability conformance + trace-capture foundation + DriverHost wire/server + plugin/control guards — PASS, 55/55.
- **Deterministic non-device regressions:** SETTINGS/OpenWorld/RuntimeAgent Phase 1–4 selector — PASS, 241/241.
- **Repository checks:** `scripts/check-consistency.sh`, `openspec validate trace-span-read-model --strict`, `openspec validate --changes --strict` (15 active changes), and `git diff --check` — PASS.
- **Full-suite classification:** 1818 passed / 9 failed. Seven failures require unavailable `emulator-5554` / `emulator-5556`; one concurrent unrelated `RunExecutionCoordinatorTests.SameDeviceExclusivity_SecondConcurrentRejected_ReleasedAfterTerminal` failure; one concurrent unrelated architecture guard flags `DeveloperOptions` in `SemanticEvidence.cs`. None references this change's read model or reader.
- **Concurrent-worktree note:** `Agent.cs` and `Agent.OpenWorld.cs` changed during apply for an unrelated pre-terminal reasoning seam. The Trace change does not own those files; its architecture guard proves Runtime has no dependency on the new read model/reader. No concurrent authority file was reverted or claimed as this change's output.
- **Lifecycle:** apply complete only. Graduation and archive remain explicitly deferred to a separate leader decision.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime.DriverHost/` | `docs/architecture/uniagent-protocol-v1-consolidation-design.md` + this change's `design.md` |
| `src/UniClaw.Runtime.Harness/` | `docs/decisions/trace-capture-scenario-catalog-architecture-gate.md` + `openspec/changes/archive/2026-08-16-runtime-observability-trace-foundation/design.md` + this change's `design.md` |
| `tests/UniClaw.Runtime.Tests/` | repository `AGENTS.md` build/test contract + both capability specs in this change |
| `openspec/specs/` | `openspec/changes/trace-span-read-model/proposal.md` + this change's two delta specs |
