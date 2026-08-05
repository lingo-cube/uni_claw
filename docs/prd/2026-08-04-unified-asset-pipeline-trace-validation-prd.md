# Unified Asset Pipeline & Trace-Based Validation PRD

> Date: 2026-08-04
> Status: draft
> Scope: `src/UniClaw.Core/` + `src/UniClaw.Host/` + `src/UniClaw.TraceTool/` + `src/UniClaw.LocalVisionProvider/` + tests
> Sources: merged from `docs/superpowers/specs/2026-08-04-unified-asset-pipeline-design.md` + `docs/superpowers/specs/2026-08-04-trace-based-validation-design.md` (review fixes E1-E4 applied inline)

## 1. Motivation

| # | Problem | Symptom |
|---|---------|---------|
| M1 | Three write paths coexist (StepAssetSink async single-item / writeGate sync / trace sync append) with no unified submission semantics | Artifacts scattered across run root and trace/, no classification |
| M2 | Pipeline implementation (StepAssetSink) lives in Host; Core has only the trace model | Mechanism placement wrong — pipeline is a generic facility |
| M3 | Validation embedded in Host (`ScenarioCompletionVerifier` ~310 lines, synchronous successCriteria judging) | Validation logic grows with scenarios; depends on in-memory objects, cannot be reviewed post-hoc |
| M4 | Run layout has no version declaration | Layout evolution silently breaks old analyzers |

**Goals**:
1. **Unified submission semantics** — screenshots/analysis/evidence are all **trace information**: producers submit uniformly (reference event + bytes), no new channel.
2. **Responsibility layering** — pipeline **common implementation in Core**; Host only composes (which backend / where to store) + produces metadata; analyzers assemble queries from config.
3. **Information/physical separation** — trace event stream (sync append) vs asset bytes (pipeline batched async to `assets/{runId}/`).
4. **File storage V2** with explicit versioning (old tools refuse loudly; new tools dual-read).
5. **Validation out of Host** — TraceTool rule engine (deterministic verdict) + agent interpretation.

**Non-goals**: async trace event stream; object-store/event-stream backends (interface only); V1 run migration (read-only compatible); enumerate rule migration (MVP = locate only).

## 2. Architecture

### 2.1 Responsibility layering

| Role | Responsibility | Explicitly NOT |
|---|---|---|
| **Core** (model + common impl) | `ITracePipeline` common implementation (Channel + batched writer + flush + DrainAsync); trace information model (events + references); `IAssetStore` / query interfaces; composition model; V2 layout model | Asset semantics, analysis, runtime assembly |
| **Host** | Composition assembly (backend key + location + runId injection) + metadata (manifest/result/issues/criteria) + safety decision handling (trace events, no persistence) + producer wiring (hook/decorator/provider) | Pipeline implementation (**StepAssetSink removed**), asset management, trace read/write mechanics |
| **TraceTool** | Config-driven query assembly + rule engine analysis (verify/diagnose) | Backend details, storage mechanics |
| **trace-analyzer agent** | Attribution & conclusion interpretation (never judges success) | — |

### 2.2 Information model: assets are trace information

```
trace information (Core model)
├─ Event stream: span / execution / state_transition / ai.* / ai.evidence (reference events) — light, JSONL sync append
└─ Assets: screenshot/evidence bytes (filename carries spanId; reference = primary channel)
     ↑ bytes physically separated from the event stream (can't coexist): sync append vs pipeline batched to assets/{runId}/
```

- **Reference = primary channel**: submitting bytes also writes a reference event (`ai.evidence`: evidence_path / evidence_type / byte_count) into the event stream — trace is the index; post-processing reads assets by reference.
- **E1 fix**: `evidence_path` in the reference event is a **relative path** (`vision-evidence-{stepSpanId}[-{seq}].json`) — producers never know runId (injected at assembly); readers resolve the full path from run context (`assets/{runId}/{relativePath}`).
- **Physical separation**: event stream `trace/{runId}/trace.jsonl` (sync append); bytes `assets/{runId}/…` (pipeline batched async).

### 2.3 Data flow

```
Run-time:
  Producer (hook / decorator / provider)
    ├─ reference event → TraceRecorder sync append (trace/{runId}/trace.jsonl)
    └─ bytes Submit(AssetSubmission{category, bytes, relativePath}) → ITracePipeline common impl
          └─ batched writer (50ms/64 items) → IAssetStore.Write(runId, relativePath, bytes)
                └─ FileAssetStore (staging atomic write + writeGate) → assets/{runId}/…
  Failure: IPipelineFailureSink.OnWriteFailed (Core iface) → Host subscription:
           per-failure issueSink entry (asset_write_failed, path + exception);
           after DrainAsync Host reads PipelineStats and writes/extended the
           assets.sink_failure summary trace event (failed/accepted/dropped counts).
           No manifest writeback — counters live in the event/log domain, manifest stays a one-shot metadata snapshot.
  finalize: DrainAsync → result.json (status="pending_verification" + engine facts) + criteria.json (verificationCriteria snapshot)

Post-run (TraceTool, in-test serial):
  config → query assembly (file queries: ITraceEventQuery + IAssetQuery → TraceQueries)
  RunEvidenceLoader: manifest.schemaVersion dispatch V1/V2 → read analysis.jsonl last row / result.json / criteria
  VerifyEngine: LocateOneItemRule → verdict/evidence/artifactPaths
  writeback result.json (atomic, only verify fields) + issues.jsonl (failure trace)
  Test asserts verdict; agent reads verdict + assets → attribution
```

### 2.4 Pipeline common implementation & composition

- **Interface**: `ITracePipeline` (Submit(AssetSubmission) / DrainAsync); `AssetSubmission` (category / bytes / relativePath); classification model (record_type extended with `asset.*`).
- **Common implementation (Core)**: bounded Channel 256 + SingleReader writer + **batched flush (50ms/64 items)** + idempotent DrainAsync — current StepAssetSink logic **moves into Core**; Host's StepAssetSink is deleted.
- **E3 (made explicit)**: the writer persists via the **`IAssetStore` interface** (Core) — the pipeline depends on the interface, not on any implementation. Host assembly supplies `FileAssetStore`.
- **E2 (P4 landing)**: `IPipelineFailureSink` (Core interface, `OnWriteFailed(AssetSubmission, Exception)`) — Core pipeline emits; **Host subscribes at assembly** to write issueSink entry (asset_write_failed). Counters are read post-DrainAsync via `PipelineStats` and written into the **trace event domain** (extended `assets.sink_failure` summary event) — never into manifest (manifest is a one-shot metadata snapshot, no writeback). No Core→Host coupling.
- **Composition = config**: each entry **owns its own config source — no cross-over** (mirrors the L3-internal vs L2 boundary in integration-config.md §9.3: one namespace per layer, test link never flows through CLI env fallback):
  - Write side (test link): integration.config `storage` section (backend key; location **reuses `emulator.outputRoot`** — no duplicate field, single truth).
  - Write side (direct `uniclaw` run): CLI env fallback (`UNICLAW_ASSET_BACKEND`, existing `UNICLAW_OUTPUT`, `UNICLAW_EVIDENCE_STORAGE`).
  - Read side (TraceTool): **CLI params are the config** — position arg explicit and required; backend default deliberately **not fixed** (normally specified per use); assembly function shape retained so a future `--backend`/`--config` only swaps the assembly source.

### 2.5 Queries & config-driven assembly

- Interfaces in Core: `ITraceEventQuery` (aligned with existing `ITraceQuery` read side), `IAssetQuery` (**read-only facet** — `Read(relativePath)` + `Exists(relativePath)`, **no Write**), `TraceQueries` aggregate (analyzer injection surface = `ITraceEventQuery` + `IAssetQuery`). **Analyzers never hold write capability**: `IAssetStore` (full interface incl. Write) is only exposed to the write-side pipeline and implementations; `FileAssetStore` implements both interfaces (same object, different facets per consumer).
- `IAssetQuery` is per-run assembled: runId injected at construction; reference **relative** paths resolved to full paths internally — analyzer code never touches runId/path composition.
- File query implementations + assembly live in **TraceTool** (config-driven; V1/V2 dispatch self-contained; layout model referenced from Core) — analyzers inject `TraceQueries`; swapping backend/composition does not change analyzer code.
- **Assembly sources (mirrors write-side precedence, D-204)**: explicit CLI params **override**; **run metadata (manifest) serves as assembly reference/defaults** — Host-produced facts (scenarioId/mode/taskId/providerId/model) feed the read-side assembly (e.g. `--task-id` omitted → take manifest.taskId; mode → rule-set selection once enumerate rules migrate). Metadata is a reference, never a truth source: explicit params always win; missing manifest fields fall back to defaults, never fail.

## 3. File Storage V2 — layout & versioning

```
{outputRoot}/{scope}/{scenarioId}/{runId}/      ← run root (runId == traceId, HostCommands.cs:692)
├── manifest.json                               ← top-level schemaVersion: "2" (V2 declaration, old-tool recognition point)
├── result.json / issues.jsonl / plan.json / scenario.snapshot.json / criteria.json
│                                               ← Host metadata (run root, V1 position unchanged)
├── trace/{runId}/trace.jsonl                   ← event-stream space (sync append incl. reference events; bucketed by runId)
├── trace/{runId}/run.log                       ← trace-correlated logging (text diagnostics, stream-append, NOT pipeline assets; layout increment per 2026-08-04-trace-correlated-logging-prd.md §4.6)
└── assets/{runId}/                             ← asset space (pipeline batched; first level = runId, symmetric with trace/)
    ├── steps/{n:D4}/before|after.png/xml       ← screenshots by span tree (engine.step dirs; moved from V1 run root)
    ├── steps/{n:D4}/analysis.json              ← step analysis (moved in)
    ├── analysis.jsonl                          ← analysis snapshots (moved in)
    └── vision-evidence-{stepSpanId}[-{seq}].json ← NEW: analysis raw evidence (config-gated)
```

> **safety decision 不落盘**：safety 决策全字段已由 TraceSafetyDecisionSink 写入 trace（`safety.*` 事件）——V1 的 `safety-decisions.jsonl` + `steps/{n}/safety-decision.json` 落盘**移除**（零读取方，trace 覆盖；信息不够补 trace 字段，不恢复落盘）。

> **runId at two levels**: run root = run directory (metadata carrier); `trace/` and `assets/` are **backend storage spaces** — first-level key = runId (stable storage key; unchanged if backend switches to object storage).

**V2 breaking changes** (why the version bump): `steps/` + `analysis.jsonl` move under `assets/`; asset space bucketed by runId; `vision-evidence-*` added (gated); `criteria.json` added (verification consumer); `safety-decisions.jsonl` + `steps/{n}/safety-decision.json` **removed** (trace covers, zero readers).

**Version mechanics**:
- Declaration: `RunAssetVocabulary.SchemaVersion` "1" → "2"; manifest top-level `"schemaVersion": "2"`.
- Old tools: manifest shows `"2"` unsupported → **loud refusal** ("unsupported run layout version 2 — upgrade the analyzer"), never silent misreads; `"1"` follows legacy path.
- New tools: dispatch by schemaVersion — "1" → V1 parser (existing code path preserved), "2" → V2 parser (assets/-aware), unknown → loud error.
- Dual parsers coexist; writers emit V2 only. trace.jsonl line format is **decoupled** from layout version.

## 4. Producers & artifacts (who writes to the pipeline)

**Rule: trigger point = production point (accountability); no central collector; submission = writing trace information (reference event + bytes).**

| Artifact | Producer (code point) | Trigger | Path (V2) | Write mode |
|---|---|---|---|---|
| Screenshots before/after.png+xml | RunAssetHook.OnBefore/AfterStepAsync (Submit) | each step start/end | `assets/{runId}/steps/{n:D4}/` | pipeline batched async |
| Step analysis.json | RunAssetHook (step-level) | step context | `assets/{runId}/steps/{n:D4}/` | pipeline batched async |
| analysis.jsonl | AnalysisWritingDecorator (Submit after analyze) | each page analysis | `assets/{runId}/analysis.jsonl` | pipeline batched async |
| vision-evidence.json | LocalVisionProvider.CompleteVisionAsync (Submit before response parse + sync ai.evidence reference event) | each vision response | `assets/{runId}/vision-evidence-{stepSpanId}[-{seq}].json` | pipeline batched (gated, default off); reference sync append |
| safety decision (trace event) | SafetyGate → TraceSafetyDecisionSink（现状已存在，全字段） | each decision | `trace/{runId}/trace.jsonl`（`safety.*` 事件） | sync append（**落盘移除**：jsonl + 步级 json 不存；信息不够补 trace 字段） |
| issues.jsonl | HostCommands.cs:866 | failure/exception | run root | writeGate sync (Host metadata) |
| manifest.json | RunAssets.StartAsync (BuildManifest) | run start (staging) | run root | writeGate sync (Host metadata) |
| result.json | RunAssets.FinalizeAsync | run end (P3 final state) | run root | writeGate sync (Host metadata) |
| trace.jsonl (incl. reference events) | TraceRecorder (StartSpan/EndSpan/RecordEvent) | span lifecycle | `trace/{runId}/` | sync append |

**Pipeline guarantees (P1-P6)**: unified submission (high-frequency artifacts only; low-frequency reliability artifacts stay on sync writeGate; trace event stream stays sync append) · zero main-path latency (TryWrite, dropped counted) · graceful shutdown (DrainAsync idempotent; **result.json final state ⇒ all bytes on disk**) · failure observability (IPipelineFailureSink → per-failure issue; `PipelineStats` post-drain → summary trace event; counters never touch manifest) · classification routing + composition · batched flush (50ms/64 items).

## 5. Trace-Based Validation

### 5.1 Run modes (one rule engine, three triggers)

| Mode | Command | Scenario |
|---|---|---|
| One-shot | `trace verify --run <dir>` | in-test serial assertion (status unrestricted — manual re-verify channel) |
| Batch re-verify | `trace verify --dir <root> [--status pending] [--task-id <id>]` | CI cron / missed runs (idempotent: pending only, re-read status before writeback) |
| Watch | `trace watch --run-id <id> --dir <root> [--interval 5s]` | watch one specific run: locate run (leaf dir name == runId; >1 match → error asking for explicit path) → poll until result.json shows `pending_verification` (P3: final state ⇒ assets complete) → auto-verify → print verdict → exit with verify's exit code |

### 5.2 verify contract

```bash
$ trace verify --run <dir> [--format json]
# exit: 0 = verified · 1 = not_verified · 2 = usage/dir error · 3 = evidence missing
# stdout single doc (schemaVersion "1"):
{
  "runId": "...", "status": "failure",
  "verdict": { "cause": "target_page_identity_not_verified", "confidence": "high",
               "failingStep": 12, "summary": "Post-action identity 'Settings' != expected 'About device'..." },
  "evidence": [ { "type": "final_identity", "step": 12, "description": "analysis.jsonl last row identity='Settings'" }, ... ],
  "artifactPaths": { "screenshotPaths": ["assets/{runId}/steps/0004/after.png"], "tracePath": "trace/{runId}/trace.jsonl" }
}
```

- Rule engine: `VerifyEngine.VerifyAsync(TraceRun)` → `IVerificationRule` list; MVP = `LocateOneItemRule` (D-201 identity-fallback semantics ported verbatim: last analysis.jsonl row Items match expectedPageIdentities; targetActionExecuted = completionReason==target_found && successful action).
- Writeback: atomic result.json update (tmp+move, verify fields only; status → success/failure aligned with `RunAssetVocabulary.ResultStatuses`); failure appends issues.jsonl. **Writeback only when status == `pending_verification`** — `verify --run` on a non-pending run still computes and prints the verdict but does **not** write back (final state never overwritten).
- Evidence-missing gate: no last analysis.jsonl row → `evidence_missing` (exit 3); attribution reads issues.jsonl (`asset_write_failed`) or the `assets.sink_failure` trace event to distinguish pipeline failure vs run no-output.

## 6. Change list

**Core**
1. `ITracePipeline` (Submit/DrainAsync) + `AssetSubmission` (category/bytes/relativePath) + classification model (record_type + `asset.*`) + **common implementation** (StepAssetSink logic moved in: bounded Channel + batched flush + idempotent DrainAsync).
2. `IPipelineFailureSink` (P4 failure notification interface) + `PipelineStats` (Accepted/Dropped/WriteFailures, post-drain read).
3. `IAssetStore` (Write/Read/Exists/List, key = `{runId}/{relativePath}`). Event side reuses existing ITraceStorage/FileTraceStorage.
4. Query interfaces: `ITraceEventQuery` + `IAssetQuery` (read-only facet: Read/Exists, no Write; per-run runId injection) + `TraceQueries` aggregate (analyzer surface exposes read only).
5. Information model: asset reference event contract (ai.evidence: **relative** evidence_path / evidence_type / byte_count) + TraceFields 45→48 keys + `TraceSpanFields.AiEvidence` profile (Basic: path/type; Extended: byte_count) + SpanFieldLevelsTests coverage update.
6. V2 layout model (constants + pure path functions); `RunAssetVocabulary.SchemaVersion` "1"→"2", manifest top-level bump.

**Host**
7. **Remove StepAssetSink**; assemble Core pipeline (backend `file` + location + runId injection); post-drain: read `PipelineStats` → write/extend `assets.sink_failure` summary trace event (metadata: failed/accepted/**dropped** counts; existing HostCommands.cs:882 check point reused); subscribe `IPipelineFailureSink` → issueSink (`asset_write_failed`).
8. `FileAssetStore` (staging atomic write + writeGate). *Note (E4): extract `AssetStagingWriter` (tmp+move) shared with RunAssets to relieve RunAssets' growing responsibilities.*
9. V2 layout migration: producers submit relativePath (runId injected → `assets/{runId}/…`); steps/, analysis.jsonl move into asset space.
10. Metadata V2 (manifest asset list/references); config: integration.config `storage` section (backend key; location reuses `emulator.outputRoot`) + `providers.local.evidenceStorage` gate (enabled, default false; extension: spanTypes). Entry-point boundary: test link injects L1→L3 explicit options (never CLI env fallback); direct runs use `UNICLAW_ASSET_BACKEND` (default file) + existing `UNICLAW_OUTPUT` + `UNICLAW_EVIDENCE_STORAGE` (default off).
10b. **Remove `RunAssetSafetyDecisionSink` file persistence** (safety-decisions.jsonl + steps/{n}/safety-decision.json) — safety decisions live in trace only (TraceSafetyDecisionSink already writes full fields); manifest drops the safetyDecimals asset-list entry. If a consumer later needs a field, extend the trace event, never restore file persistence.
11. Run end writes result.json: `status="pending_verification"` + engine facts; **verificationCriteria snapshot → `criteria.json`** (separate file, V2 consumer reads it); delete ScenarioCompletionVerifier locate branch (~60 lines → TraceTool); enumerate branch untouched.
12. P3.1 fix: hook exceptions (BeginStepAsync/capture failure) no longer silently swallowed by FireAsync Log-and-Continue — issueSink trace entry.
13. LocalVisionProvider: inject `ITracePipeline?` + `ITraceContextProvider` + evidenceStorage gate (null/off → complete no-op): before response parse (L89) build relative path `vision-evidence-{stepSpanId}-{seq}.json` → `pipeline.Submit(...)` + sync `RecordEventAsync("ai.evidence", parent=stepSpanId, attrs={evidence_path(relative), evidence_type, byte_count})`. spanId via `EngineStepSpanContext.CurrentSpanId`; per-step seq guards ai.call retry overwrite.

**TraceTool**
14. Read-entry version dispatch: manifest.schemaVersion → "1" V1 parser / "2" V2 parser / unknown → loud error (exit code + stderr).
15. File query implementations + config-driven assembly (config → TraceQueries); analyzers inject `TraceQueries` (backend/composition swap doesn't change analyzer code). MVP: **CLI params are the config** — position arg explicit/required; backend default not fixed (normally specified per use); assembly function shape retained (future `--backend`/`--config` swaps only the assembly source).
16. `RunEvidenceLoader` (run dir → VerificationInput rebuild; DI `IAssetQuery` (per-run runId injection — the analyzer read facet; same read path the analyzers get); schemaVersion dispatch before reads; exposes manifest metadata to assembly — taskId/mode/scenarioId/providerId as defaults, explicit CLI overrides).
17. `VerifyEngine` + `LocateOneItemRule` (rule port).
18. Commands: `verify --run` (status unrestricted) / `verify --dir [--status pending] [--task-id]` (pending-only, re-read before writeback) / `watch --run-id <id> --dir <root> [--interval]` (locate by leaf dir name == runId, poll for pending_verification, auto-verify, exit with verify code).
19. Unit tests: rule layer (success / identity-fallback / not_verified / evidence_missing) + idempotency + temp-run-dir construction.

**Tests**
20. RunScenarioAsync tail: invoke verify CLI → parse verdict → assert → failure merges verdict summary into test failure info.

**agent**
21. trace-analyzer.md L4: add verify/watch/batch commands + responsibility statement (success = C# rules; agent = attribution/interpretation) + missing interactive subcommand.

**Implementation notes**: result.json issueFingerprints uses `IsDefaultOrEmpty` (NRE trap, lessons-documented); Channel needs no package (net10 built-in, Host has no explicit reference).

## 7. Acceptance

| Item | Method |
|---|---|
| Pipeline batching | Unit: 100 Submit → writes << 100 (aggregation); after DrainAsync 100 all on disk |
| Graceful shutdown | Unit: mid-run DrainAsync → remaining buffer flushed; final result.json ⇒ assets complete |
| Composition & assembly | Unit: config (backend key + location) → Host route correct + TraceTool queries assembled; swap (mock backend) → analyzer unchanged |
| Medium abstraction | Unit: mock backend injected → query interface protocol unchanged |
| V2 layout | Integration: run → directory assertions (`assets/{runId}/steps/`, `assets/{runId}/analysis.jsonl`, schemaVersion "2") |
| Dual-version read | Unit: V1-constructed run → V1 parse passes; V2 run → V2 parse passes |
| Old-tool refusal | Unit: unsupported version → loud error (no silent) |
| Rule correctness | VerifyEngine unit tests (success/fallback/failure/evidence-missing) |
| Failure counters | Unit: TryWrite-full → dropped counts; write exception → WriteFailures + issue entry; post-drain summary event carries all counts |
| E2E link | LocateOneItem: run → verify → **success** |
| Idempotency | Batch/scheduled/serial interleaving never double-verifies (unit) |
| Contract | Exit codes + JSON schemaVersion aligned with existing CLI contract |
| Regression | Full unit suite green (V1 parser preserved; Host changes don't break enumerate/mock paths) |

## 8. Boundaries (non-goals)

Async trace event stream · object-store/event-stream implementations · V1 run migration/rewrite (read-only compatible) · enumerate rule migration · storage abstraction layer beyond interfaces (extension points: reference-as-key + RunEvidenceLoader swap) · watch uses polling (no FileSystemWatcher) · agent never judges success.
