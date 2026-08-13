# Perception Phase 3 / Phase 4 Semantic Audit Repair Gate

> Date: 2026-08-13  
> Role: Project Leader / Semantic Enforcement Repair Gate  
> Input: `SOL_PERCEPTION_PHASE3_PHASE4_SEMANTIC_ENFORCEMENT_AUDIT_RESULT`  
> Result: `PERCEPTION_PHASE3_PHASE4_SEMANTIC_AUDIT_REPAIR_GATE_RESULT`  
> Decision: **PURCHASE_WITH_CONSTRAINTS**  
> Implementation: **NOT AUTHORIZED IN THIS GATE**

## 0. Repository reconciliation

The attached gate contract names `docs/audits/perception-phase3-phase4-semantic-gap-register.md`; repository truth contains the authoritative file [perception-phase3-phase4-gap-register.md](../audits/perception-phase3-phase4-gap-register.md). No duplicate gap register is created.

The authoritative audit found 55 rules: E4=9, E3=18, E2=18, E1=10, E0=0; 12 BYPASSABLE, one UNPROVEN, zero CONTRADICTED; S0=0, S1=9, S2=1, S3=2. Current evidence still supports `RuntimeDelta=NONE`, `SemanticDelta=NONE`, `AuthorityDelta=NONE`, and `ArchitectureReopenRequired=NO`.

## 1. Gate decision

Purchase five ordered repair slices plus a targeted independent re-audit. The repairs make already-frozen rules mandatory at their existing ownership boundaries; they do not add semantic authority, a release lifecycle, or a platform subsystem.

```text
Decision: PURCHASE_WITH_CONSTRAINTS

Order:
  P4-R1 Evidence integrity
  P4-R2 Write-once persistence
  P4-R3 Dataset / annotation / training enforcement
  P4-R4 Operational fail-closed + canonical Host composition
  P4-R5 Documentation truth
  P4-R6 Targeted Sol re-audit

No implementation is authorized by this artifact.
```

Dependencies preserve the audit order:

- R1 first establishes valid evidence identity, canonical scoring, and truthful terminal runs.
- R2 then protects those terminal/history artifacts against replacement.
- R3 can reuse R2 for accepted annotations, admitted datasets, TrainingRuns, and lineage.
- R4 adds operational diagnostics and proves the canonical Host composition without changing Runtime decisions.
- R5 records the actual executable pipeline and canonical launch path.
- R6 attacks every original bypass after all repairs are present.

## 2. Cross-gap root causes and minimum shared purchases

### 2.1 Caller-discipline is not enforcement

GAP-004, GAP-006, GAP-007, and GAP-008 share the failure pattern, not an implementation abstraction. Each has a different canonical authority boundary—evaluation scoring, dataset admission, review acceptance, and training execution—and therefore receives a small domain-specific gate. A generic validation framework is forbidden.

### 2.2 Domain immutability is not persistence immutability

GAP-005 is a shared persistence root cause. Purchase one small repository-owned Python function, `write_once_json` (name may vary mechanically), used only by canonical semantic-history writers. It provides collision-safe, atomic, byte-canonical persistence; it is not an artifact service/repository framework.

GAP-003 is not solved by write-once persistence: an asset may move legitimately, so the actual bytes used for L2 must still be revalidated at execution.

### 2.3 Safe fallback needs a separate operational fact

GAP-001 must preserve the semantic result `[]` while emitting structured operational facts through the existing `RuntimeObservability` ActivitySource. Runtime business code must not consume those facts.

### 2.4 A guarded factory without a composition root is not production enforcement

GAP-009 purchases a minimal composition factory in `UniClaw.Vision.Host` that loads the already-approved CURRENT ACTIVE deployment receipt and is the canonical route to `VisionHostConfig.ForCanonicalProduction`. It does not choose a model/config/pipeline; the receipt is authoritative input and Host only compares.

## 3. Gap dispositions

### GAP-002 — Invalid coordinates

- **Frozen rule:** bounds represent the full original screenshot, normalized to `[0,1]^2`, top-left; invalid geometry is not normal Observation evidence.
- **Bypass:** `remap_coords` and schema serialization accept NaN/infinity, negative/out-of-range, zero/negative/reversed rectangles.
- **Canonical enforcement:** one post-remap/pre-response `validate_evidence_geometry` boundary (name may vary) owned by `uniclaw_perception.schema` and invoked by `server._run_pipeline`. It validates already-remapped production `candidates`, `yolo`, and `ocr` before response serialization. Evaluation stage views remain explicitly non-production and retain their own stage coordinate space; their `Detection/OcrToken.to_json(width,height)` serialization must nevertheless reject non-finite/reversed/out-of-stage-range boxes as defense in depth. The existing C# `ElementBounds.IsValid` remains final defense in depth.
- **Invalid-state policy:** reject the invalid element at serialization/admission, preserve independently valid siblings, and emit a structured `INVALID_GEOMETRY` diagnostic with element/stage identity. This matches corruption locality. A single invalid detector/OCR/fused element does not prove the entire frame corrupt.
- **Geometry rule:** all values finite; `0 <= x1 < x2 <= 1`; `0 <= y1 < y2 <= 1`; source width/height positive. No silent clamping and no epsilon tolerance is purchased because repository evidence shows no rounding-only boundary failure requiring one.
- **Fail closed:** invalid element does not appear in `candidates`, `yolo`, `ocr`, or stage views as valid evidence. An all-invalid response is semantically empty but operationally `INVALID_GEOMETRY`, never `NO_ELEMENTS`.
- **Proof:** COORD-01..07, including mixed valid/invalid siblings and proof that the evaluation-only view cannot reintroduce rejected production geometry as canonical Observation evidence.
- **Delta:** production evidence schema gains only a backward-compatible diagnostics member in metadata/response; no Runtime semantic contract, authority, or ownership change.

### GAP-003 — Asset byte revalidation

- **Frozen rule:** AssetId is exact source-byte identity.
- **Bypass:** manifest load trusts `content_hash`; L2 reads `source_path` later without checking those bytes.
- **Canonical enforcement:** `run_fresh_inference` at the L2 execution boundary, before model/config load and before Prediction construction; `load_asset_manifest` also rejects internal manifest `assetId != content_hash` as defense in depth.
- **Invalid-state policy:** parsed legacy/external manifests may be inspected, but execution admission recomputes SHA-256 over the actual bytes and requires equality to claimed asset ID.
- **Fail closed:** `ASSET_CONTENT_IDENTITY_MISMATCH` as Evaluation infrastructure/evidence-integrity failure; no inference, Prediction, metric, score, or manifest rewrite.
- **Proof:** ASSET-01..05.
- **Delta:** none to public evidence semantics; no identity algorithm change.

### GAP-004 — Metric provenance binding

- **Frozen rule:** canonical score evidence binds the exact Run, Prediction, GroundTruth, deployment identity, stage, and label space.
- **Bypass:** detached candidate arrays and defaulted stage/space can be scored against unrelated GT.
- **Canonical enforcement:** a bounded immutable `EvaluationScoringContext` and canonical `score_evaluation(context)` entrypoint in the evaluation package. It consumes the run request/result identity, full `Prediction`, `GroundTruth`, and a typed Prediction view selected from that Prediction; it validates before calling internal metric math.
- **Invalid-state policy:** provenance mismatch is represented as a canonical non-scorable/rejected scoring result, never a metric value and never model failure.
- **Required checks:** GT asset equals Prediction asset; Prediction run equals the request ID embedded by the terminal Run result; Prediction deployment hash equals Run deployment identity hash. Stage/space come from a closed mapping of the selected stored view (`rawModelDetections`, `normalizedDetections`, `fusedEvidence`/canonical candidates), not caller-supplied detached labels; they must be compatible with GT. `compute_task_metrics` becomes private/internal pure math or clearly non-authoritative and cannot persist canonical results.
- **Canonical result provenance:** result records Run ID, Prediction asset/run/deployment identity, GroundTruth asset/version/source, prediction stage/space, compatibility verdict, and task results.
- **Fail closed:** mismatch produces `PROVENANCE_MISMATCH` / `NOT_SCORABLE`; no canonical scored result.
- **Proof:** MET-01..06.
- **Delta:** additive evaluation evidence provenance only; no generic evaluation framework or release authority.

### GAP-010 — EvaluationRun lifecycle

- **Frozen rule:** COMPLETED is terminal execution truth, not intent.
- **Bypass:** `EvaluationRun.create` creates and saves COMPLETED before asset execution.
- **Canonical enforcement:** split immutable `EvaluationRunRequest` from terminal immutable `EvaluationRunResult`; the historical `EvaluationRun` loader remains available for old artifacts but new canonical execution persists no terminal Run before execution.
- **Identity decision:** request ID remains the deterministic hash of suite/deployment/backend/evaluator/environment/scope and is the ID Predictions reference. Terminal result has a distinct result ID containing request ID plus terminal outcome and stable per-asset outcome references; history metadata remains non-identity.
- **Outcome decision:** `COMPLETED` only when all requested assets reached a valid terminal evaluation result with no unresolved infrastructure/provenance failure; `PARTIAL` when some completed and some lack evidence without infrastructure corruption; `INSUFFICIENT_EVIDENCE` for honest non-infrastructure evidence absence; `INFRASTRUCTURE_FAILURE` for asset/identity/pipeline/process/provenance failures.
- **Crash behavior:** no canonical terminal result is written. An optional noncanonical operational attempt record may remain, but cannot claim COMPLETED or quality evidence.
- **Fail closed:** stage/provenance rejection or identity failure cannot yield COMPLETED quality Run.
- **History:** prior Run JSON is loaded as legacy historical evidence and never rewritten/reclassified.
- **Proof:** RUN-01..06.
- **Delta:** bounded evaluation lifecycle type split; no mutable Run, workflow engine, or Runtime delta.

### GAP-005 — Append-only semantic history

- **Frozen rule:** canonical semantic-history artifacts are append-only/immutable.
- **Bypass:** direct `Path.write_text` overwrites ID-named files; Baseline exposes `overwrite=True`.
- **Canonical enforcement:** a single `write_once_json(path, canonical_payload)` primitive in the perception platform's shared identity/persistence utility; all canonical semantic-history save functions use it.
- **Persistence semantics:** serialize canonical UTF-8 bytes; create target atomically/exclusively; byte-identical existing content is idempotent success; different bytes at the same semantic path are rejected; concurrent writers cannot silently replace or partially expose content. Implementation may use same-directory temporary file plus exclusive link/create or an equivalent platform-safe mechanism; never replace an existing canonical file.
- **Included authoritative artifacts:** EvaluationAsset manifest, GroundTruth, EvaluationSuite, terminal EvaluationRunResult, Prediction, Baseline, Annotation, DatasetVersion, TrainingConfig, all persisted TrainingRun state/terminal records, ModelArtifact metadata, Candidate, authoritative lineage report, ModelManifest, ConfigManifest, PipelineRevision, and DeploymentIdentity/Candidate. Each writer targets the record's own canonical semantic ID (for example `ModelManifest.manifest_id`, not merely a convenient model filename); legacy locations remain readable and untouched.
- **Excluded operational/derived files:** CLI output, generated YOLO labels/data YAML, framework run files/images, caches, and the replaceable CURRENT ACTIVE operational receipt. The receipt remains deployment composition input, not historical artifact identity.
- **Model bytes:** `materialize_model_artifact` also verifies existing content-addressed bytes match ModelId; it never replaces differing bytes.
- **Fail closed:** persistence collision raises a typed integrity error and leaves the previous artifact byte-identical.
- **Proof:** IMM-01..08 across representative artifacts and concurrency.
- **Delta:** one bounded persistence primitive; no database, service, registry, or event sourcing.

### GAP-006 — Leakage admission

- **Frozen rule:** protected evaluation assets cannot enter training; exact-content and known capture-group leakage are rejected.
- **Bypass:** `check_leakage` is optional and findings do not block save/training.
- **Canonical enforcement:** `DatasetVersion.admit_for_training(...)` (name may vary) validates an untrusted parsed/draft DatasetVersion against explicit role inputs and returns an immutable `ValidatedDatasetVersion`/admission receipt consumed by canonical training. `save_dataset` persists history but does not itself make a dataset executable.
- **Invalid-state policy:** direct/legacy manifests remain inspectable as untrusted history; no canonical TrainingRun can consume them without admission.
- **Inputs:** explicit protected evaluation AssetIds; exact content IDs; CaptureGroup where present. Missing CaptureGroup remains unknown and is never fabricated or treated as proof of independence.
- **Defense in depth:** training runner independently requires the validated admission receipt tied to DatasetVersion ID and protected-set identity.
- **Fail closed:** any L1, known L2, or protected-role finding rejects training admission/execution; no TrainingRun/model lineage.
- **Proof:** LEAK-01..06.
- **Delta:** additive training admission evidence; no dataset service or policy engine.

### GAP-007 — Annotation acceptance authority

- **Frozen rule:** suggestion/prediction is not accepted annotation truth; acceptance is an explicit immutable authority event.
- **Bypass:** constructor/factory/deserializer can create `MODEL_ASSISTED + ACCEPTED` without a review event.
- **Canonical enforcement:** all newly ACCEPTED annotations, regardless of source, require immutable `AcceptanceProvenance` containing non-empty review event ID, reviewer/authority identity, and predecessor Annotation ID. `reviewedAt` may be persisted as history metadata but does not replace authority identity.
- **Construction rule:** normal creation yields DRAFT/REVIEWED only. `accept_annotation` is the sole canonical transition and returns a new identity. Direct ACCEPTED construction or deserialization without complete provenance is invalid for dataset admission.
- **History:** legacy accepted annotations lacking the new structured record can be read as `LEGACY_ACCEPTANCE_PROVENANCE` and remain historical, but cannot be silently rewritten. Existing repository accepted annotations already contain predecessor and provenance text and must be validated/migrated by explicit compatibility reading, not mutation.
- **Fail closed:** invalid accepted record cannot be admitted to a training-executable DatasetVersion.
- **Proof:** ANN-01..06.
- **Delta:** bounded annotation provenance fields and admission validation; no annotation workflow system.

### GAP-008 — Training invocation congruence

- **Frozen rule:** TrainingConfig describes actual execution.
- **Bypass:** Ultralytics kwargs are hardcoded independently from the recorded config.
- **Canonical enforcement:** one pure `resolve_ultralytics_invocation(TrainingConfig, dataset_view, output_location)` translator; canonical runner calls `model.train(**resolved.arguments)` only. No independent behavior-affecting kwargs are accepted.
- **Identity decision:** TrainingConfigId remains the sole configuration identity. `ResolvedTrainingInvocation` is execution evidence, not a competing config; it records effective framework arguments and a canonical hash for congruence/audit.
- **Admission checks:** unresolved required config values, unsupported overrides, or mismatch between recorded TrainingConfig and resolved actual invocation fail before training. TrainingRun records TrainingConfigId plus resolved invocation facts/hash.
- **Fail closed:** mismatch cannot produce a valid completed TrainingRun, Checkpoint→ModelArtifact lineage, or Candidate.
- **Proof:** TRAIN-01..07 using a captured framework call, not the same config helper for expected values.
- **Delta:** bounded translation/evidence type inside training; no Planner/config framework.

### GAP-001 — Empty evidence versus infrastructure failure

- **Frozen rule:** unsafe/stale evidence is never substituted; operational failure remains distinguishable without semantic authority.
- **Bypass:** HTTP failure and truthful empty analysis collapse to `[]` with no structured operational fact.
- **Canonical enforcement:** `LocalVisionPerceptionSource.AnalyzeAsync` remains the semantic `ImmutableArray` contract and emits tags/events on the existing parent Environment observation Activity: `perception.outcome`, `perception.failure_class`, and a stable event name. Add a bounded internal diagnostic classifier, not a public Runtime semantic result.
- **Classes:** `OK_EMPTY`, `TIMEOUT`, `INFRASTRUCTURE_FAILURE`, `SCHEMA_FAILURE`, `MALFORMED_RESPONSE`, `INVALID_GEOMETRY`; unexpected transport/parser exceptions are classified, traced, and converted to empty semantic evidence. Cancellation remains cancellation and is rethrown.
- **Success semantics:** a valid 200 response with zero candidates emits `OK_EMPTY`; failures emit their class and return `[]`. No cached/stale response exists or is introduced.
- **Authority:** Trace/Harness may collect and assert the fact; Agent/Container/Traversal never branch on it.
- **Fail closed:** semantic result remains empty/UNKNOWN-safe, while operational evidence is non-empty and classed.
- **Proof:** FAIL-01..07 plus listener-failure isolation.
- **Delta:** Adapter observability only; Runtime semantic contracts unchanged.

### GAP-009 — Canonical Host composition root

- **Frozen rule:** canonical production Host always supplies the approved expected DeploymentIdentity and fails closed against observed `/version`.
- **Bypass:** guarded factory exists but no production composition root uses it; public direct config can omit identity.
- **Canonical enforcement:** add one minimal `CanonicalVisionHostFactory` (name may vary) in `UniClaw.Vision.Host`. It consumes the repository CURRENT ACTIVE deployment receipt path supplied by the caller, validates all four axes and required schema, creates `ExpectedDeploymentIdentity`, and calls `VisionHostConfig.ForCanonicalProduction` then `VisionServiceHost`.
- **Authority:** the factory does not discover, select, compare candidates, promote, activate, or rewrite the receipt. It materializes an already-authoritative receipt into Host expectations.
- **Reachability:** canonical production documentation/bootstrap exposes only this factory. Direct `VisionHostConfig` remains an explicit legacy/test seam and is marked noncanonical; an architecture guard rejects direct construction outside Vision.Host tests/approved legacy fixtures.
- **Fail closed:** absent/incomplete/malformed receipt, expected/observed model/config/pipeline/deployment mismatch, or missing required schema prevents HEALTHY.
- **Restart:** every restart reuses expected receipt facts and re-observes `/version`.
- **Proof:** HOST-01..08 including a real current-identity server path.
- **Delta:** one Host composition seam and guard; no Runtime→Host dependency and no release authority.

### GAP-011 — Pipeline description

- **Repair:** update the Phase 3 graduation record with an additive “current precise executable description” rather than rewriting the historical simplified freeze:

```text
Decode → Preprocess → Raw YOLO → Label normalization → OCR
→ Fusion / unmatched-OCR promotion / reclassification
→ Coordinate remap → Scroll-hint / metadata derivation → Serialization
```

The exact ordering must follow code: current `server.py` remaps before adding metadata/scroll hints, so documentation must not invert these stages. Evaluation-only raw/normalized/fused stage-view capture is explicitly `EVALUATION OBSERVABILITY`, returned out-of-band and not a production evidence-schema change.

### GAP-012 — Legacy launch documentation

- **Repair:** update current source XML/help (`LocalVisionPerceptionSource` and `benchmark_raw.py`) to canonical `platforms/perception/uniclaw_perception` / `uniclaw_perception.server:app` paths. Historical decisions remain historical.

## 4. Canonical enforcement sites

| Concern | Canonical site | Defense in depth / consumer |
|---|---|---|
| Geometry validity | Python schema/evidence serialization after remap | C# `ElementBounds.IsValid` |
| Asset byte identity | L2 runner immediately before inference | Manifest internal-ID check |
| Metric provenance | Canonical evaluation scoring context/entrypoint | Pure metric math internal only |
| Run lifecycle | Terminal evaluation result materialization after execution | Write-once persistence |
| Immutable history | Shared perception-platform `write_once_json` | Per-artifact canonical serializers |
| Leakage | Dataset training admission + required training receipt | Runner rechecks receipt binding |
| Annotation acceptance | Explicit acceptance constructor with provenance | Dataset admission validation |
| Training congruence | TrainingConfig→resolved invocation translator | TrainingRun records resolved facts |
| Failure diagnostics | Adapter Activity span/event | Harness trace recorder/assertions |
| Host identity | Canonical Host composition factory | Startup/restart identity comparison |

## 5. Bypassable-rule disposition

All 12 audit BYPASSABLE rules are explained and purchased; none is accepted as an authoritative pure-helper exception:

| Rule | Disposition |
|---|---|
| P3-11 | GAP-002 / R1 geometry serialization enforcement |
| P4-01 | GAP-003 / R1 execution-time byte verification |
| P4-04 | GAP-004 / R1 canonical scoring provenance |
| P4-05 | GAP-004 / R1 explicit stage/label provenance |
| P4-14 | GAP-005 + GAP-010 / R1 terminal lifecycle and R2 write-once history |
| P4-15 | GAP-005 / R2 DatasetVersion persistence |
| P4-17 | GAP-006 / R3 mandatory leakage admission |
| P4-18 | GAP-007 / R3 acceptance authority |
| P4-19 | GAP-008 / R3 invocation congruence |
| P4-21 | GAP-005 / R2 failed TrainingRun preservation |
| P4-23 | GAP-005 / R2 ModelArtifact metadata/bytes collision guard |
| P4-37 | GAP-005 / R2 legacy history preservation |

Low-level metric math may remain internally callable, but its output is noncanonical until wrapped by the R1 scoring context. This does not waive P4-04/P4-05; it removes its ability to cross the persistence/evidence boundary.

## 6. Unproven rule disposition

```text
Rule: P4-34
Reason: ForCanonicalProduction and mismatch rejection are test-proven, but no
        real production composition root uses the factory; direct nullable
        ExpectedIdentity construction remains reachable.
Repair: GAP-009 / P4-R4 purchases the canonical composition root, reachability
        guard, real matching identity proof, mismatch proofs, and restart proof.
Expected post-repair: E4_RUNTIME_FAIL_CLOSED_AND_FALSIFIED.
Deferral: NONE after R4; repair closure cannot retain anonymous UNPROVEN=1.
```

## 7. Real identity round-trip disposition

Current failure category is **TEST_INFRA_TIMEOUT**, not `UNKNOWN`:

- `test_runtime_snapshot.ServerProc._http` sends HTTP/1.1 without `Connection: close` and reads until peer close or a 20-second timeout.
- Uvicorn keeps the connection alive; each health/version request therefore waits for timeout.
- failed early UDS connections can bypass `s.close`, producing the observed socket `ResourceWarning`.
- restart cases make multiple requests, exceeding the execution environment's silent-output window.

R4 is authorized to repair the test fixture transport only: close the socket in `finally` and either send `Connection: close` or parse `Content-Length`. It must then produce a terminal real `/version` result. Preferred acceptance is PASS. If the environment later blocks real Python execution, the result must be a reproducible named environmental blocker plus an independent proof that no production deadlock exists; `UNKNOWN` is not acceptable.

## 8. Expected post-repair enforcement

| Gap | Target |
|---|---|
| GAP-002 | E4 |
| GAP-003 | E4 |
| GAP-004 | E4 |
| GAP-005 | E3 plus direct collision/concurrency falsifiers |
| GAP-006 | E4 |
| GAP-007 | E3, preferably E4 direct-construction/deserialization falsifier |
| GAP-008 | E4 |
| GAP-001 | E3 operational distinction; Runtime safety remains existing E3/E4 |
| GAP-009 | E4 |
| GAP-010 | E4 |
| GAP-011 | Documentation aligned with executable truth |
| GAP-012 | No stale current operational reference |

Closure requires: no remaining S1; no unexplained BYPASSABLE; P4-34 resolved to E4 or an explicit named blocker; all historical artifacts byte-identical; no new Runtime dependency or release authority.

### Delta audit

```text
RuntimeDelta:   NONE
SemanticDelta:  NONE
AuthorityDelta: NONE
OwnershipDelta: NONE
DependencyDelta: NONE

PublicEvidenceSchemaDelta:
  BACKWARD_COMPATIBLE_OPERATIONAL_DIAGNOSTICS_ONLY

EvaluationArtifactDelta:
  ADDITIVE_PROVENANCE_AND_TRUTHFUL_LIFECYCLE
```

The added diagnostic/provenance fields expose already-required truth and do not change what counts as a world element, semantic Goal evidence, action authority, model quality, or release approval.

## 9. Recommended implementation slices

### P4-R1 — Evidence integrity

Scope: GAP-002, GAP-003, GAP-004, GAP-010. Add geometry rejection/diagnostics, L2 asset hash guard, provenance-bound scoring, immutable request/terminal run split, and COORD/ASSET/MET/RUN falsifiers. Do not introduce write-once helper here beyond the minimum needed to keep tests isolated; R2 owns its shared rollout.

### P4-R2 — Write-once persistence

Scope: GAP-005. Add one atomic collision-safe JSON primitive; migrate the enumerated canonical semantic-history writers; remove Baseline's overwrite escape; verify model-byte collision. Preserve operational/derived output behavior.

### P4-R3 — Dataset / annotation / training enforcement

Scope: GAP-006, GAP-007, GAP-008. Add mandatory dataset admission receipt, accepted-annotation provenance, config-derived Ultralytics invocation and recorded execution facts; reuse R2 persistence.

### P4-R4 — Operational fail-closed and canonical Host composition

Scope: GAP-001, GAP-009. Add Adapter Activity diagnostics, canonical Host factory/guard, real Host proofs, and repair the runtime-snapshot test transport timeout. No Runtime semantic branching.

### P4-R5 — Documentation truth

Scope: GAP-011, GAP-012. Reconcile precise pipeline semantics and current launch paths; do not rewrite historical artifacts.

### P4-R6 — Targeted Sol re-audit

Re-run original attacks for GAP-001..010, update P3-10/P3-14 matrix evidence, adjudicate all 12 prior BYPASSABLE rows and P4-34, verify history hashes, fresh L2, real Host identity, full suites, guards, and diff hygiene. Implementation tests alone cannot close the audit.

### Bounded implementation file budget

The following is the maximum expected production/governance surface. Mechanical file names may be consolidated, but implementation may not add a subsystem or cross the listed module boundaries without returning to Sol.

| Slice | Existing files expected to change | New bounded artifact allowed |
|---|---|---|
| P4-R1 | `uniclaw_perception/schema.py`, `server.py`, `evaluation/asset.py`, `runner_l2.py`, `prediction.py`, `metrics.py`, `run.py`, `first_baseline.py`, `training/candidate_eval.py` | At most one evaluation scoring-context module; Run request/result may remain in `run.py` |
| P4-R2 | Existing canonical `save_*` modules enumerated in §3 GAP-005 | One small shared Python immutable-write utility under `platforms/perception/`; no service/class hierarchy |
| P4-R3 | `training/dataset.py`, `annotation.py`, `training_config.py`, `training_run.py`, `mini.py`, and only necessary lineage/candidate consumers | At most one small training invocation translator module; admission/provenance records stay with their owning domain modules |
| P4-R4 | `LocalVisionPerceptionSource.cs`, `VisionServiceHost.cs`, governance runtime-snapshot test fixture | At most one canonical Host composition file in `UniClaw.Vision.Host`; no new executable project unless Sol first proves the library composition cannot satisfy HOST-01..08 |
| P4-R5 | Phase 3 graduation precision note, adapter XML/current CLI help, audit matrix/gap closure evidence | None |

Tests may be added only under the existing perception, evaluation, training, governance, Vision, observability, and architecture test locations. Historical artifacts are test inputs, not rewrite targets.

## 10. Required falsifiers

The implementation purchase requires exactly the contract suites:

- COORD-01..07
- ASSET-01..05
- MET-01..06
- RUN-01..06
- IMM-01..08
- LEAK-01..06
- ANN-01..06
- TRAIN-01..07
- FAIL-01..07
- HOST-01..08
- REP-01..08

Additionally, the implementation must run all current Perception, VisionHost, Evaluation, Training, DeploymentGovernance, Golden Replay, full .NET, Architecture Guard, consistency, canonical fresh L2, real `/version`, and `git diff --check` validations.

## 11. Frozen constraints

Not purchased:

- Runtime semantic contract or Runtime dependency changes
- Agent, Container, Traversal, Environment authority/ownership changes
- ReleasePolicy, EvaluationProfile, Candidate-vs-ACTIVE feature work
- promotion, activation, rollback, specialist routing, automatic deployment/retraining
- ModelRegistry, artifact service, database, event sourcing
- generic validation/workflow/provider framework
- history rewrite/backfill
- geometry clamping or fabricated missing metadata
- model/config/pipeline selection authority in Host

Historical EvaluationRuns, Baselines, TrainingRuns, DatasetVersions, Annotations, and legacy partial-config evidence remain byte-identical historical truth. New validators may parse them with an explicit legacy/untrusted stance; they may not silently mutate or upgrade them.

## 12. Final result

```text
PERCEPTION_PHASE3_PHASE4_SEMANTIC_AUDIT_REPAIR_GATE_RESULT

Decision: PURCHASE_WITH_CONSTRAINTS
ArchitectureReopenRequired: NO

RuntimeDelta: NONE
SemanticDelta: NONE
AuthorityDelta: NONE

NextTask:
  IMPLEMENT_PERCEPTION_PHASE3_PHASE4_SEMANTIC_AUDIT_REPAIRS

NO_CANDIDATE_COMPARISON_YET
NO_EVALUATION_PROFILE_YET
NO_RELEASE_POLICY
NO_PROMOTION
NO_ACTIVE_MUTATION
```

STOP.
