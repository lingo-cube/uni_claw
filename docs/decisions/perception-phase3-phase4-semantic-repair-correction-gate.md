# Perception Phase 3 / Phase 4 Semantic Repair — Correction Gate

> Date: 2026-08-13
> Role: Project Leader (GPT-5.6 Sol) / Targeted Canonical Boundary Correction Gate
> Input: `perception-phase3-phase4-semantic-repair-targeted-reaudit-result.md` (REPAIR_INCOMPLETE, 6 surviving S1)
> Result: `PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_CORRECTION_GATE_RESULT`
> Decision: **PURCHASE_WITH_CONSTRAINTS**
> Implementation: **NOT AUTHORIZED IN THIS GATE**

---

## 0. Decision

```text
ArchitectureReopenRequired: NO
RemainingS1: GAP-002, GAP-004, GAP-006, GAP-007, GAP-008, GAP-009
HistoricalArtifactCheck: WAIVED (no correction rewrites history; new
                          records only — waiver stands, not converted to PASS)
```

The six gaps share one pattern: **the validated object exists, but the
authoritative consumer accepts a raw / caller-supplied alternative.** Each
correction therefore names the exact four-part enforcement chain below.
No generic `Validated<T>` framework. Each domain repairs at its own
authority boundary.

---

## 1. GAP-002 — Geometry must cover every serialized view

### Four-part analysis

```text
AUTHORITATIVE OUTPUT
  Every serialized collection that carries geometry across the
  production/evaluation evidence boundary:
    • production response: candidates, yolo, ocr   ← canonical production
      evidence, normalized post-remap contract [0,1] (finite, 0<=x1<x2<=1,
      0<=y1<y2<=1)
    • evaluation stage views (capture_stage_views=True):
        rawModelDetections    ← pre-remap PIXEL space (proc-image coords;
                                finite, ordered, within proc image bounds)
        normalizedDetections  ← pre-remap PIXEL space (same contract)
        fusedEvidence         ← post-remap normalized [0,1]
  `yolo`/`ocr` are canonical production evidence (schema v1 fields) —
  they carry the normalized contract. Stage views carry their OWN
  explicitly owned pixel-space contract. No unchecked geometry anywhere.

LAST TRUST BOUNDARY
  Single post-processing point in server.py `_run_pipeline` where all
  collections are assembled before serialization (post-remap for the
  response collections; per-view for stage views).

REQUIRED INPUT
  One validator: validate_geometry(items, *, contract) where contract ∈
  {NORMALIZED_PRODUCT, PROC_PIXEL, ...} — explicit per collection. Result:
  (valid_items, invalid_count, all_invalid). Never clamps.

BYPASS REMOVAL
  remap.py currently validates candidates only (re-audit line: candidates
  are filtered at lines 106-119; yolo/ocr untouched). Correction moves
  collection validation to the single serialization boundary in server.py
  covering ALL collections. Invalid item → dropped, valid siblings
  preserved; all-invalid collection → semantic empty + operational
  INVALID_GEOMETRY status (NOT OK_EMPTY). No alternate serialization path.
```

### Coordinate semantics decision (explicit)

`yolo` and `ocr` in the production response are **canonical production
evidence** (consumed by the adapter and recorded in Prediction artifacts) —
they are validated with the normalized post-remap contract. Evaluation
stage views are **evaluation observability** with their own pixel-space
contracts; they are validated against those contracts at the stage-view
assembly point. Both boundaries share the same validator, different
contracts — never the wrong semantics.

### Required falsifiers

```text
CORR-GEO-01 candidate invalid → rejected
CORR-GEO-02 YOLO invalid → rejected
CORR-GEO-03 OCR invalid → rejected
CORR-GEO-04 mixed siblings preserve only valid elements
CORR-GEO-05 all invalid → INVALID_GEOMETRY, not OK_EMPTY
CORR-GEO-06 no NaN/Infinity/reversed geometry survives
CORR-GEO-07 no silent clamp
CORR-GEO-08 production/evaluation stage coordinate semantics remain explicit
```

---

## 2. GAP-004 — Canonical quality artifact must require provenance-bound score evidence

### Four-part analysis

```text
AUTHORITATIVE OUTPUT
  BaselineReport and any canonical quality persistence.

LAST TRUST BOUNDARY
  BaselineReport.create() — currently accepts a caller-supplied
  `quality_scorecard: dict[str, Any]` (baseline.py lines 32/49).

REQUIRED INPUT
  ProvenanceBoundScorecard — an immutable scoring-evidence object built
  ONLY from the provenance-bound path:
    EvaluationRunRequest (requestId)
    → terminal EvaluationRunResult
    → Prediction (assetId, requestId, deploymentHash, stored view)
    → GroundTruth (assetId, stage, labelSpace)
    → EvaluationScoringContext.score() (compatibility verdict + task results)
  No caller-supplied detached stage/space/deployment claims. No raw dict.

BYPASS REMOVAL
  BaselineReport.create signature changes to require ProvenanceBoundScorecard
  (breaking the raw-dict path). Any dict needed for rendering is DERIVED
  internally from the canonical object. Detached compute_task_metrics stays
  as NONCANONICAL internal math: legal to call, result has NO persistence
  authority, and no canonical writer accepts its raw output. No alternate
  canonical scorecard-dictionary writer may exist (CORR-MET-09 audit).
```

### Public construction disposition (this domain)

| Surface | Disposition |
|---|---|
| `EvaluationScoringContext.score()` | CANONICAL (provenance-bound scoring) |
| `compute_task_metrics(...)` | INTERNAL_PURE_HELPER (NONCANONICAL) |
| `BaselineReport.create(...)` | CANONICAL (requires ProvenanceBoundScorecard) |
| internal `_render_scorecard(...)` | INTERNAL_PURE_HELPER (dict derivation) |

### Required falsifiers

```text
CORR-MET-01 detached metric math remains noncanonical
CORR-MET-02 detached result cannot create BaselineReport
CORR-MET-03 wrong Prediction asset rejected
CORR-MET-04 wrong Run/request rejected
CORR-MET-05 wrong deployment rejected
CORR-MET-06 wrong stage rejected
CORR-MET-07 wrong LabelSpace rejected
CORR-MET-08 canonical quality persistence requires provenance-bound score
CORR-MET-09 no alternate canonical scorecard dictionary writer exists
```

---

## 3. GAP-006 — Training execution must require exact admission receipt

### Four-part analysis

```text
AUTHORITATIVE OUTPUT
  Executed training + terminal TrainingRun (lineage eligibility).

LAST TRUST BOUNDARY
  Canonical training execution seam. Current state: mini.py creates a
  receipt with an EMPTY protected set (line 152) and discards it;
  execution requires no receipt.

REQUIRED INPUT
  TrainingAdmissionReceipt — immutable, bound to:
    datasetVersionId (content-addressed membership snapshot)
    protectedSetId (content hash of the protected evaluation membership)
    policyVersion (leakage rule version)
    admission findings (L-1/L-2 results)
  No receipt → NO TRAINING. Receipt created with an empty protected set
  when the authoritative protected membership exists is NOT a valid
  receipt; if authoritative protected membership is UNAVAILABLE →
  admission NOT established → fail closed.

BYPASS REMOVAL
  Canonical execution signature:
    execute_training(config, admission_receipt, location_ctx)
  Execution verifies receipt.datasetVersionId == dataset actually
  materialized/executed AND receipt.protectedSetId == protected set
  required by this composition. TrainingRun persists
  trainingAdmissionReceiptId. Legacy historical runs remain historical
  (no retrofitting). No alternate canonical runner without admission.
```

### Required falsifiers

```text
CORR-LEAK-01 no receipt → training rejected
CORR-LEAK-02 wrong dataset receipt → rejected
CORR-LEAK-03 protectedSet A receipt used for B → rejected
CORR-LEAK-04 protected AssetId → rejected
CORR-LEAK-05 known CaptureGroup leakage → rejected
CORR-LEAK-06 missing CaptureGroup remains UNKNOWN
CORR-LEAK-07 receipt recorded in TrainingRun
CORR-LEAK-08 execution cannot discard/replace receipt
CORR-LEAK-09 alternate canonical runner cannot bypass admission
```

---

## 4. GAP-007 — Acceptance provenance must be a real chain

### Four-part analysis

```text
AUTHORITATIVE OUTPUT
  ACCEPTED Annotation admitted into training (Dataset admission).

LAST TRUST BOUNDARY
  Dataset admission validation — currently does not load or validate
  referenced annotation records (forged MODEL_ASSISTED + ACCEPTED with
  invented non-empty strings was admitted).

REQUIRED INPUT
  AnnotationAcceptanceEvent — repository-native, file-based immutable
  event (no workflow system / service / database):
    reviewEventId
    predecessorAnnotationId
    acceptedAnnotationId (content binding)
    reviewer/authority identity
    decision = ACCEPT
    stage/LabelSpace/asset lineage binding
  Canonical accept_annotation() creates BOTH the event AND the new
  ACCEPTED Annotation as immutable history.

BYPASS REMOVAL
  Admission loads and validates the chain:
    predecessor exists; event exists; event binds predecessor→accepted;
    decision == ACCEPT; stage/LabelSpace/asset lineage compatible;
    accepted annotation is an allowed continuation of predecessor.
  Direct dataclass construction / deserialization → inspection-only,
  NOT training-admissible. status==ACCEPTED alone and non-empty
  provenance strings alone grant nothing.
```

### Legacy disposition (explicit decision)

```text
LegacyAcceptedAnnotationTrainingDisposition:
  LEGACY_ACCEPTANCE_PROVENANCE
  — readable, inspectable historical truth;
  — NOT admissible into NEW canonical training (no verifiable chain);
  — never silently rewritten.
```

### Required falsifiers

```text
CORR-ANN-01 direct ACCEPTED dataclass construction not training-admissible
CORR-ANN-02 invented reviewEventId rejected
CORR-ANN-03 nonexistent predecessor rejected
CORR-ANN-04 mismatched predecessor rejected
CORR-ANN-05 valid explicit acceptance chain admitted
CORR-ANN-06 predecessor remains unchanged
CORR-ANN-07 acceptance creates new Annotation identity
CORR-ANN-08 dataset admission loads referenced Annotation truth
CORR-ANN-09 deserializer cannot grant training authority
CORR-ANN-10 forged MODEL_ASSISTED accepted record rejected
```

---

## 5. GAP-008 — Training execution must start from TrainingConfig

### Four-part analysis

```text
AUTHORITATIVE OUTPUT
  Executed training + TrainingRun lineage eligibility
  (Checkpoint→ModelArtifact→Candidate).

LAST TRUST BOUNDARY
  Canonical training execution seam. Current state: mini.py passes
  cfg.resolved_invocation(...) (caller-resolved invocation); TrainingRun
  accepts arbitrary trainingConfigId / invocation args / invocation hash
  as declarative truth (epochs=999 replay succeeded).

REQUIRED INPUT
  TrainingConfig + Validated Dataset Admission + non-behavior
  execution location/context. NOT caller-supplied
  ResolvedTrainingInvocation.

BYPASS REMOVAL
  Canonical runner internal order:
    load/validate exact TrainingConfig
    → resolve invocation INSIDE the runner
    → capture actual framework-bound arguments (independent evidence)
    → execute model.train(...)
    → TrainingRun derives configId + invocation evidence from the
      execution session (never from caller declarations)
    → congruence verification
    → only then terminal lineage eligibility.
  Mismatch → TRAINING_INVOCATION_MISMATCH:
  no completed lineage, no ModelArtifact, no Candidate.
  ResolvedTrainingInvocation may remain as INTERNAL immutable
  execution-evidence type — never caller authority.
```

### Public construction disposition (this domain)

| Surface | Disposition |
|---|---|
| `TrainingConfig.resolved_invocation(...)` | INTERNAL_PURE_HELPER (runner-internal resolution only) |
| `ResolvedTrainingInvocation` | INTERNAL_PURE_HELPER (execution-evidence record) |
| canonical `execute_training(config, admission, ctx)` | CANONICAL (only execution seam) |
| `TrainingRun(...)` direct construction | NONCANONICAL_INSPECTION_ONLY (canonical creation only via runner evidence) |
| `save_training_run` | CANONICAL for terminal records; RUNNING writes remain forbidden (existing rule) |

### Required falsifiers

```text
CORR-TRAIN-01 epochs override impossible
CORR-TRAIN-02 imgsz override impossible
CORR-TRAIN-03 seed override impossible
CORR-TRAIN-04 caller-created ResolvedInvocation cannot drive canonical training
CORR-TRAIN-05 actual model.train kwargs captured independently
CORR-TRAIN-06 TrainingRun derives config identity from actual loaded config
CORR-TRAIN-07 arbitrary invocation hash cannot be recorded as truth
CORR-TRAIN-08 mismatch blocks Checkpoint→ModelArtifact lineage
CORR-TRAIN-09 alternate canonical runner cannot bypass translator
CORR-TRAIN-10 intended mini training path still works
```

---

## 6. GAP-009 — Factory must be the canonical production reachability boundary

### Four-part analysis

```text
AUTHORITATIVE OUTPUT
  Production Host composition (the real running service).

LAST TRUST BOUNDARY
  VisionHostConfig / VisionServiceHost construction. Current state:
  constructors remain public verification-optional seams; HOST-08 scans
  only inside the Host project and excludes the direct-construction files.

REQUIRED INPUT
  CanonicalVisionHostFactory path:
    CURRENT ACTIVE receipt (governance/artifacts current-active identity)
    → ExpectedDeploymentIdentity snapshot
    → VisionHostConfig.ForCanonicalProduction
    → VisionServiceHost with mandatory verification.

BYPASS REMOVAL (A — structural reachability)
  • VisionHostConfig's default (verification-optional) constructor and
    VisionServiceHost's public constructor become INTERNAL.
  • Approved test-only seam: InternalsVisibleTo("UniClaw.Runtime.Tests")
    so behavioral/legacy tests keep direct construction; production
    assemblies cannot.
  • Architecture guard covers ALL production projects (not one directory):
    no production project may reference the internal constructors —
    compile-time proof, not grep.
  • ForCanonicalProduction remains PUBLIC — the only canonical production
    construction path.

BYPASS REMOVAL (B — real composition proof)
  The proof must execute the actual canonical chain:
    CURRENT ACTIVE receipt → factory → expected snapshot → Host →
    real Python production server → /version → expected==observed →
    HEALTHY; restart with captured expectation → fresh /version → HEALTHY;
    then the four mismatches (wrong model/config/pipeline/schema) through
    the SAME factory-created Host path → fail closed.
  Python runtime-snapshot tests + Host predicate unit tests are NOT
  sufficient as a combined substitute (explicit re-audit finding).
```

### Required falsifiers

```text
CORR-HOST-01 all production projects blocked from direct noncanonical construction
CORR-HOST-02 canonical factory uses CURRENT ACTIVE receipt
CORR-HOST-03 real factory-created Host reaches HEALTHY
CORR-HOST-04 restart re-verifies real child
CORR-HOST-05 wrong model fail closed
CORR-HOST-06 wrong config fail closed
CORR-HOST-07 wrong pipeline fail closed
CORR-HOST-08 unsupported schema fail closed
CORR-HOST-09 receipt mutation after construction does not silently switch current Host
CORR-HOST-10 P4-34 reaches E4 evidence
```

---

## 7. Cross-domain public construction audit (summary)

| Domain | CANONICAL | NONCANONICAL_INSPECTION_ONLY | INTERNAL_PURE_HELPER | LEGACY_READ_ONLY | TEST_ONLY |
|---|---|---|---|---|---|
| Geometry (GAP-002) | server serialization boundary | — | validate_geometry | — | — |
| Quality (GAP-004) | BaselineReport.create (+ProvenanceBoundScorecard), EvaluationScoringContext.score | — | compute_task_metrics | historical baselines | — |
| Admission (GAP-006) | execute_training(config, receipt, ctx) | TrainingAdmissionReceipt.load | validate_training_admission | historical runs | — |
| Annotation (GAP-007) | accept_annotation (event+record), admission chain validation | Annotation/deserializer | — | LEGACY_ACCEPTANCE_PROVENANCE | — |
| Training (GAP-008) | canonical runner | TrainingRun loader | resolved_invocation, ResolvedTrainingInvocation | historical runs | — |
| Host (GAP-009) | CanonicalVisionHostFactory + ForCanonicalProduction | — | — | — | InternalsVisibleTo(UniClaw.Runtime.Tests) |

No ambiguous authoritative public bypass remains in any domain after
correction.

---

## 8. Aggregate decisions

```text
PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_CORRECTION_GATE_RESULT

Decision:                     PURCHASE_WITH_CONSTRAINTS
ArchitectureReopenRequired:   NO
RemainingS1:
  GAP-002 / GAP-004 / GAP-006 / GAP-007 / GAP-008 / GAP-009

GAP002Correction:
  single validator at the server serialization boundary covering
  candidates/yolo/ocr (normalized post-remap contract) AND stage views
  (their owned pixel-space contracts); drop invalid items, preserve valid
  siblings, all-invalid → INVALID_GEOMETRY (never OK_EMPTY); never clamp.

GAP004Correction:
  ProvenanceBoundScorecard as the ONLY input to BaselineReport.create;
  dict derivation internal; detached compute_task_metrics = NONCANONICAL
  internal math with zero persistence authority; no alternate raw-dict
  canonical writer.

GAP006Correction:
  execute_training requires TrainingAdmissionReceipt bound to
  datasetVersionId + protectedSetId + policyVersion; execution verifies
  congruence; receipt id persisted in TrainingRun; unavailable protected
  membership → admission NOT established → fail closed.

GAP007Correction:
  AnnotationAcceptanceEvent (immutable, file-based) created by canonical
  accept_annotation; admission loads and validates predecessor→event→
  accepted chain; direct construction/deserialization = inspection-only;
  legacy = LEGACY_ACCEPTANCE_PROVENANCE, NOT new-training-admissible.

GAP008Correction:
  canonical seam accepts TrainingConfig + admission only; invocation
  resolved and captured INSIDE the runner; TrainingRun derives config/
  invocation identity from execution-session evidence; mismatch →
  TRAINING_INVOCATION_MISMATCH (no lineage/artifact/candidate).

GAP009Correction:
  internal constructors for verification-optional paths +
  InternalsVisibleTo(tests); public factory = sole canonical production
  path; guard spans ALL production projects; real factory→ACTIVE-receipt→
  server→/version→HEALTHY + restart + 4 mismatch fail-closed proofs.

CanonicalConsumerBindings:
  serialization boundary (geo) / BaselineReport.create (quality) /
  execute_training (admission + config) / admission chain validation
  (annotation) / CanonicalVisionHostFactory (host)

PublicConstructionDisposition:       see §7 table
AnnotationReviewEventDecision:       PURCHASE AnnotationAcceptanceEvent
LegacyAcceptedAnnotationTrainingDisposition:
                                     LEGACY_ACCEPTANCE_PROVENANCE — readable,
                                     NOT admissible into new training
TrainingAdmissionReceiptBinding:     datasetVersionId + protectedSetId +
                                     policyVersion + findings; persisted in
                                     TrainingRun
TrainingExecutionSignature:          execute_training(config, admission_receipt,
                                     location_ctx)
BaselineCanonicalPersistenceDecision:
                                     BaselineReport.create requires
                                     ProvenanceBoundScorecard; internal dict
                                     derivation only
HostConstructorReachabilityDecision: internal constructors +
                                     InternalsVisibleTo(UniClaw.Runtime.Tests);
                                     factory-only canonical path
P4_34ClosurePlan:                    C4 slice (structural reachability +
                                     real composition proof) → E4

HistoricalArtifactCheck:             WAIVED (no correction rewrites history;
                                     new records only)

RequiredCorrectionFalsifiers:
  CORR-GEO-01..08   (8)
  CORR-MET-01..09   (9)
  CORR-LEAK-01..09  (9)
  CORR-ANN-01..10   (10)
  CORR-TRAIN-01..10 (10)
  CORR-HOST-01..10  (10)
  Total: 56 correction falsifiers.

RegressionProtection:
  GAP-001, GAP-003, GAP-005, GAP-010, GAP-011, GAP-012 —
  already-closed gaps must keep passing; a correction that breaks one is
  a regression, not a trade-off.

RuntimeDelta:    NONE
SemanticDelta:   NONE
AuthorityDelta:  NONE

RecommendedImplementationSlices:
  C1  Geometry complete response-boundary enforcement      (GAP-002)
  C2  Canonical provenance-bound quality persistence       (GAP-004)
  C3  Training authority closure                           (GAP-006+007+008)
  C4  Canonical Host reachability + real composition proof (GAP-009)
  C5  Fresh independent correction re-audit (fresh Sol context)

NextTask:
  IMPLEMENT_PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_CORRECTIONS

NO_PROVIDER_STATE_DISCOVERY_YET
NO_AGENT_NEXT_PHASE_YET
NO_MODEL_TRAINING_ROADMAP
NO_CANDIDATE_COMPARISON
NO_RELEASE_POLICY
```

STOP.
