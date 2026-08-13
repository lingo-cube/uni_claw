# Perception Phase 3 / Phase 4 Semantic Repair — Correction Implementation Result

> Date: 2026-08-13
> Role: Project Leader (GPT-5.6 Sol) / Targeted Canonical Boundary Correction Implementation Verifier
> Input: `PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_CORRECTION_GATE` (PURCHASE_WITH_CONSTRAINTS)
> Result: `PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_CORRECTION_IMPLEMENTATION_RESULT`
> Status: **VALIDATED — READY_FOR_FRESH_REAUDIT**

> No self-graduation. A fresh independent Sol re-audit must replay the
> original attacks (C5, not implemented here).

---

## 0. Result

```text
GapStatus:
  GAP-002: CLOSED — single validator at the server serialization boundary
           covers candidates/yolo/ocr (normalized post-remap contract)
           AND stage views (owned pixel-space contracts); all-invalid →
           INVALID_GEOMETRY (never OK_EMPTY); never clamps.
  GAP-004: CLOSED — ProvenanceBoundScorecard is the ONLY input to
           BaselineReport.create; detached compute_task_metrics is
           NONCANONICAL internal math with zero persistence authority;
           no alternate raw-dict canonical writer exists.
  GAP-006: CLOSED — execute_training requires TrainingAdmissionReceipt
           bound to datasetVersionId + protectedSetId; execution verifies
           congruence; receipt id persisted in TrainingRun; protected
           membership unavailable → admission fail closed.
  GAP-007: CLOSED — AnnotationAcceptanceEvent chain (predecessor record →
           event on disk → accepted record, payload-hash content binding);
           admission loads and validates referenced annotation records;
           direct construction/deserialization = inspection-only;
           legacy = LEGACY_ACCEPTANCE_PROVENANCE, NOT new-training-admissible.
  GAP-008: CLOSED — canonical seam accepts TrainingConfig + receipt only;
           invocation resolved and captured INSIDE the runner;
           TrainingRun identity derived from the execution session
           (training_run_from_execution); mismatch →
           TRAINING_INVOCATION_MISMATCH (no lineage).
  GAP-009: CLOSED — internal constructors for verification-optional paths
           + InternalsVisibleTo(UniClaw.Runtime.Tests); public
           CanonicalVisionHostFactory = sole canonical production path;
           real composition proof executed (see below).

CanonicalConsumerBinding:
  Geometry:           server.py _run_pipeline serialization boundary
  Quality:            BaselineReport.create (ProvenanceBoundScorecard only)
  TrainingAdmission:  execute_training(config, receipt, …) — no receipt → no training
  AnnotationAcceptance: admit_dataset_for_training → validate_acceptance_chain
  TrainingExecution:  execute_training (invocation derived + captured inside)
  HostComposition:    CanonicalVisionHostFactory → ForCanonicalProduction

PublicConstructionAudit:
  Geometry:           validate_geometry/enforce_geometry = CANONICAL boundary;
                      remap_coords = INTERNAL_PURE_HELPER
  Quality:            EvaluationScoringContext.score = CANONICAL;
                      compute_task_metrics = NONCANONICAL_INSPECTION_ONLY;
                      BaselineReport.create = CANONICAL
  Admission:          execute_training = CANONICAL; validate_training_admission =
                      INTERNAL (receipt creation); TrainingAdmissionReceipt =
                      NONCANONICAL_INSPECTION_ONLY outside execution
  Annotation:         accept_annotation + acceptance_event_for = CANONICAL;
                      Annotation direct construction/from_json =
                      NONCANONICAL_INSPECTION_ONLY; legacy records =
                      LEGACY_READ_ONLY
  Training:           training_run_from_execution = CANONICAL; TrainingRun
                      direct construction = NONCANONICAL_INSPECTION_ONLY;
                      ResolvedTrainingInvocation = INTERNAL_PURE_HELPER
  Host:               CanonicalVisionHostFactory = CANONICAL;
                      VisionHostConfig()/VisionServiceHost() = internal;
                      tests via InternalsVisibleTo = TEST_ONLY

P4_34ImplementationEvidence:
  E4 — real canonical chain executed:
    CURRENT ACTIVE receipt → CanonicalVisionHostFactory → real Python
    production server → /version → expected==observed → HEALTHY (5s)
    → TryRestartAsync → fresh child re-verified → HEALTHY
    → wrong model/config/pipeline through the factory path → fail closed
    → unsupported schema → fail closed at the earliest boundary
    → receipt mutation after construction does not switch the live Host
    (11/11 VisionHostFactoryCompositionTests, real server, clean state)

CorrectionFalsifiers (56):
  CORR-GEO-01..08:   8/8 PASS (9 tests incl. determinism extra)
  CORR-MET-01..09:   9/9 PASS
  CORR-LEAK-01..09:  9/9 PASS
  CORR-ANN-01..10:  10/10 PASS
  CORR-TRAIN-01..10: 10/10 PASS (CORR-TRAIN-10 = real mini run)
  CORR-HOST-01..10: 10/10 PASS (11 tests incl. factory-snapshot extra)

RegressionProtection:
  GAP-001: PASS (adapter operational classes — Vision behavioral suite)
  GAP-003: PASS (verified-bytes L2 — evaluation suite)
  GAP-005: PASS (write-once — semantic enforcement + training suites;
           legacy pretty-printed records accepted ONLY on parsed semantic
           equality, never normalized; different content still refused)
  GAP-010: PASS (immutable request + terminal result — evaluation RUN tests)
  GAP-011: PASS (graduation record untouched)
  GAP-012: PASS (no legacy path references touched)

HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED
                      (not converted to PASS; no correction rewrites history)

PerceptionTests:       9/9 PASS
EvaluationTests:     101/101 PASS
TrainingTests:        62/62 PASS
DeploymentGovernanceTests: 37/37 PASS (unit) — RSI/OCR real-server suites
                      were executed in the prior targeted audit and remain
                      unmodified by this correction
VisionHostTests:      16/16 behavioral + 11/11 factory composition PASS
ArchitectureGuards:   PASS (no reverse dependency; internal constructors
                      compile-time reachability; governance unit guards)
RealHostCanonicalComposition: PASS (factory → real server → HEALTHY)
FreshL2:               PASS (evaluation suite covers L2 fresh inference)
DiffCheck:             PASS

RuntimeDelta:          NONE
SemanticDelta:         NONE
AuthorityDelta:        NONE
ArchitectureReopenRequired: NO
ReadyForFreshIndependentReaudit: YES
```

---

## 1. What was built (by slice)

### C1 — GAP-002 geometry (uniclaw_perception/remap.py + server.py)

`validate_geometry(items, *, space_label, pixel_limits)` + `enforce_geometry`
+ `enforce_stage_views`: one validator, applied at the single serialization
boundary in `_run_pipeline` covering candidates/yolo/ocr (normalized
post-remap + original-frame pixel limits) AND the three evaluation stage
views (pre-remap pixel-space contracts for raw/normalized detections,
normalized for fused). Invalid items dropped, valid siblings preserved,
all-invalid → `status: INVALID_GEOMETRY` + diagnostics — never OK_EMPTY,
never clamped. The historical attack payload (x1=-0.2, x2=2.4, y2=2.3) is
now rejected from `yolo` and `ocr` exactly as from `candidates`.

### C2 — GAP-004 quality authority (evaluation/provenance_scorecard.py + baseline.py)

`ProvenanceBoundScorecard` — built only from `EvaluationScoringResult`s
(run request, prediction, ground truth, view, stage, LabelSpace bindings).
`BaselineReport.create` type-enforces it (raw dict → TypeError). Dict
derivation happens internally; `compute_task_metrics` output has no
persistence path (CORR-MET-09: qualityScorecard persistence lives in
exactly two modules — the canonical object and the canonical writer).

### C3 — GAP-006/007/008 training authority closure

- `AnnotationAcceptanceEvent` (immutable, file-based, payload-hash content
  binding) + `validate_acceptance_chain` + `admit_dataset_for_training`
  (leakage + per-member chain validation → receipt).
- `execute_training(config, admission_receipt, …)` — receipt congruence
  verified (dataset + protected set), invocation derived INSIDE the runner,
  actual `model.train` kwargs captured independently, congruence verified.
- `training_run_from_execution` — TrainingRun identity derived from the
  execution session + admission receipt; mismatch raises
  `TRAINING_INVOCATION_MISMATCH`.
- Real mini run re-executed through the full canonical chain: admission
  receipt → execution → terminal TrainingRun with receipt id → checkpoint →
  artifact → candidate (`cand:fc5aa351…`, mAP50 0.093 — process proof,
  not quality). A real invocation-surface defect (invalid
  `scheduler`/`augmentation` kwargs) was caught by the new seam and fixed
  at the config boundary; the FAILED attempt was preserved.

### C4 — GAP-009 Host reachability + real composition proof

- `VisionHostConfig()` parameterless constructor and `VisionServiceHost`
  constructor made **internal**; `InternalsVisibleTo("UniClaw.Runtime.Tests")`
  grants test-only access. `CanonicalVisionHostFactory` remains the sole
  public canonical production path.
- `VisionHostFactoryCompositionTests` (11 tests, real Python server):
  structural reachability (reflection: no public noncanonical construction),
  factory receipt validation, real factory→server→HEALTHY, restart
  re-verification, three axis-mismatch fail-closed cases through the
  factory path, schema fail-closed at the earliest boundary, and receipt
  mutation immunity. The live identity system correctly detected staleness
  mid-work (C1 source changes → new PipelineRevision) and the ACTIVE
  receipt was rebuilt — the drift detection working as designed.

## 2. Historical artifacts

`HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED` — preserved verbatim,
not converted to PASS. No correction rewrites historical records; new
records only. (One in-session exception, documented: stale intermediate
acceptance-event files from this correction session's own failed attempts
were removed before any canonical dataset referenced them — they were
never part of persisted history.)

## 3. Next task

```text
SOL_PERCEPTION_PHASE3_PHASE4_SEMANTIC_CORRECTION_TARGETED_REAUDIT

A fresh independent Sol context must replay the original attacks for all
six gaps and verify no previously-closed gap regressed. This result only
asserts READY_FOR_FRESH_REAUDIT — semantic closure is NOT self-declared.
```

STOP.
