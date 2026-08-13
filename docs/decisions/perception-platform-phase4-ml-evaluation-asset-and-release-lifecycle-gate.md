# Perception Platform Phase 4 — ML Evaluation Asset & Release Lifecycle Architecture Gate

> Date: 2026-08-12
> Role: Project Leader (Opus) / Architecture · Reality-Model · Governance Gate
> Mode: `ARCHITECTURE_DESIGN_ONLY`
> Result: `PERCEPTION_PLATFORM_PHASE_4_ML_EVALUATION_ASSET_AND_RELEASE_LIFECYCLE_GATE_RESULT`
> Decision: **PURCHASE_WITH_CONSTRAINTS**
> Implementation authority: **NOT GRANTED**

---

## 0. Precondition check

```text
PHASE_3_GRADUATION
  = NOT_GRADUATED

Phase 3 implementation status: VALIDATED, not graduated.
Deferred graduation evidence:
  • RE1–RE3 live-model equivalence (requires emulator/vision env)  — NOT_EXECUTABLE
  • Host H1–H18 regression                                          — NOT_EXECUTABLE
  • Full Runtime regression with migrated perception                — NOT_EXECUTABLE
  • P3-16 legacy path removal (tools/local_vision/)                 — DEFERRED

PREREQUISITE_GAP
  = EXPLICIT

Phase 4 implementation authorization: WITHHELD until Phase 3 graduation.
Architecture analysis: PROCEEDS (this gate).
```

This gate designs the Phase 4 lifecycle. It does not authorize building it.
Constraint C1 freezes: Phase 4 implementation may not begin until Phase 3
graduation completes (`PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_3_GRADUATION_REVIEW`).

### Current frozen facts carried into this gate

- `uniclaw_perception` = canonical Python production package (Phase 3).
- `VisionServiceHost` = sole Python service lifecycle owner (Phase 2).
- Runtime depends on neither Vision.Host nor Python implementation.
- Perception produces evidence only; no semantic authority.
- `modelId` = full SHA-256 of exact artifact (64 hex chars, frozen — current `health.py`).
- `modelName` = stable family identity (parent directory name), distinct from `modelId`.
- `configHash` = SHA-256(label-mapping.json), truthful but PARTIAL.
- Supported schema: `uniclaw.localVisionEvidence.v1`.
- Perception unavailable → `[]` → UNKNOWN. No fabricated evidence.
- FailureEpisode remains a Harness artifact only.
- Existing reality assets: 2 recorded screenshots (uni-claw Harness corpus),
  3 evidence JSON fixtures (perception tests/fixtures/), Runtime Scenario
  catalog (SC-P1-002, SC-P2-003, SC-P3-001), Simulation/Replay harnesses.

---

## 1. Asset taxonomy (P4-G1)

Nine orthogonal dimensions. No single flattened enum. Each asset carries one
classification per dimension; `UNKNOWN` is an honest value everywhere.

```text
A. Provenance       SYNTHETIC | REALITY_SEEDED | RECORDED_REALITY | LIVE_CAPTURE
                    (historical provenance preserved; never silently promoted)
B. CorpusRole       GOLDEN | REGRESSION | CHALLENGE | HOLDOUT | CALIBRATION | PERFORMANCE
C. SystemFamily     ANDROID_AOSP | SAMSUNG_ONEUI | OPPO_COLOROS | OTHER | UNKNOWN
                    (only families with current evidence; no speculative vendor
                     taxonomy — expand on admission evidence)
D. ScenarioDomain   SETTINGS | PERMISSION | DIALOG | APP_CONTENT | NAVIGATION |
                    SYSTEM_UI | INPUT | UNKNOWN
E. PerceptionTask   ELEMENT_DETECTION | OCR | SWITCH_STATE | BOUNDS |
                    LABEL_CLASSIFICATION | FUSION | PAGE_STRUCTURE
F. ComponentClass   SWITCH | BUTTON | TEXT | INPUT | CHEVRON | DIALOG | ICON |
                    LIST_ITEM | SCROLL_CONTAINER | UNKNOWN
G. Difficulty       NORMAL | HARD | ADVERSARIAL
H. Criticality      CRITICAL | IMPORTANT | NORMAL
I. Theme (tags)     light, dark, low_contrast, compressed, small_target,
                    occluded, scrolled, dpi_variant   (open tag set)
```

Classification falsifiers CF-01…CF-05 (see §22) pin the orthogonality:
a RECORDED_REALITY + REGRESSION + ONEUI + SWITCH_STATE + dark + CRITICAL asset
is legal and needs no combined enum.

## 2. Asset identity (P4-G2)

```text
AssetId            = "sha256:{ContentHash}"     (content-addressed)
ContentHash        = SHA-256(canonical asset bytes)   — identity anchor
SchemaVersion      = taxonomy/manifest schema version
Provenance         = original provenance, immutable
SourceRelation     = explicit link to originating Scenario / CaptureSession /
                     Frame / FailureEpisode / run — may be absent
GroundTruthVersion = ground-truth document version the asset is bound to

IDENTITY  = { ContentHash, SchemaVersion }      → immutable, move-invariant
METADATA  = everything else                     → may evolve with classification
```

Moving an asset never changes identity. Filename, directory, timestamp play
no role. Classification metadata is versioned separately from identity.

## 3. Scenario vs Asset (P4-G3)

```text
Scenario         = expected behavior / task contract (e.g. SC-P3-001 timeout,
                   SC-P2-003 recovery verification)
EvaluationAsset  = evidence/input used to evaluate some aspect of a Scenario

Relationship: many-to-many explicit graph.
Scenario --references--> EvaluationAsset (per execution level)
```

One Scenario may be exercised by synthetic frames, recorded screenshots,
replay observations, emulator captures, or real-device captures — five
execution levels, NOT five duplicated Scenarios. The Scenario contract is
written once; assets are bound to it through the evidence graph (§14).

## 4. Execution evidence ladder (P4-G4)

```text
L0 COMPONENT / SYNTHETIC     — unit tests, synthetic images, mocked backends
L1 RUNTIME REPLAY            — recorded observations replayed through Runtime
L2 RECORDED IMAGE INFERENCE  — stored real screenshot + fresh model inference
L3 EMULATOR                  — controlled Android emulator runs
L4 REAL DEVICE               — physical device capture
```

| Level | Input requirement | Proves | Cost | Determinism | CI |
|---|---|---|---|---|---|
| L0 | code + synthetic fixtures | code correctness, coordinate math, fusion unit behavior | CHEAP | exact | PR_FAST |
| L1 | replay manifests | Runtime semantics unchanged | CHEAP | exact | PR_FAST |
| L2 | recorded screenshots + model | **current-model perception accuracy** (primary ML regression workhorse) | LOW | high (CPU inference) | NIGHTLY / MODEL_CANDIDATE |
| L3 | emulator + ADB | end-to-end perception + interaction, gap vs L2 | MEDIUM | high | MODEL_CANDIDATE / RELEASE_CANDIDATE |
| L4 | physical device | real-world fidelity, emulator/real gap, hardware perf | EXPENSIVE | medium | MANUAL_REALITY |

A higher layer never replaces lower layers. L2 is the volume workhorse for
model/config evaluation; L3/L4 are gating and gap-detection layers.

## 5. Execution backend separation (P4-G5)

```text
EvaluationCase  (what is evaluated — asset set + metric contract + gates)
        +
ExecutionBackend (where — Simulation | Replay | RecordedImage | Emulator | RealDevice)
        ↓
EvaluationRun   (immutable record of one case × one backend × one deployment candidate)
```

Backends are concrete named implementations, not a plugin registry. A case
hard-codes a backend only when the environment itself is the condition under
test (e.g. "emulator/real gap detection" case). No framework purchase until
file-based manifests prove insufficient (P4-G79).

## 6. Cross-layer asset reuse (P4-G6)

Content-addressed storage makes reuse structural: the same screenshot bytes
are stored once; each consuming system references the same `AssetId`.

```text
real screenshot (sha256:X)
  ├── Perception ML evaluation  → L2 EvaluationCase reference
  ├── Runtime Replay            → Scenario asset binding (same bytes)
  ├── FailureEpisode            → Harness correlation reference
  └── Regression Asset          → CorpusRole=REGRESSION membership (manifest ref)
```

Relationships are explicit manifest references, never duplicated bytes.
Dedup is hash-based at first (P4-G8); no ML-based perceptual dedup yet.

## 7. Asset admission (P4-G7)

```text
CandidateAsset (emulator run, real-device run, FailureEpisode, manual capture,
                synthetic generation, training error, model regression,
                new device/system coverage)
        ↓ 10-point admission review
AdmissionDecision → ADMIT | REJECT | DEFER
        ↓ ADMIT
CanonicalAsset (content-addressed, classified, manifest-bound)
```

Admission checklist (all evaluated; a NO on 1–4 blocks admission):

1. Ground truth exists and is reviewable?
2. Ground truth review quality adequate (annotation status, §12)?
3. Duplicate / near-duplicate of an existing asset (dedup gate, §8)?
4. New coverage: task / family / difficulty / component not already represented?
5. New failure mode captured?
6. Regression value: expected to be stable and discriminative?
7. Privacy/security suitability (device data, no personal content)?
8. Provenance known?
9. Classification complete enough (no forced UNKNOWN on critical dimensions)?
10. Reproduction possible (source frame/capture retained or referenced)?

No automatic permanent retention of every runtime frame. Unadmitted frames
may keep metadata-only references where policy permits (§8).

## 8. Asset retention / dedup (P4-G8)

```text
DedupStrategy: exact-hash first.

1. EXACT DUPLICATE:  SHA-256 equal → same AssetId → no new canonical bytes.
                     Membership manifests may add references (suite binding).
2. PERCEPTUAL NEAR-DUPLICATE: DEFERRED until hash/metadata dedup proves
   insufficient. When needed: frame-embedding or perceptual-hash candidate
   triage with human admission review, never automatic silent merge.
3. CANONICAL REPRESENTATIVE: for near-duplicate groups, one admitted
   canonical asset; others are metadata references to it.
4. FAILURE-SPECIFIC EXCEPTION: a distinct FailureEpisode link may justify
   retaining a near-duplicate as its own RegressionAsset (explicit admission
   decision, not automatic).
5. METADATA-ONLY RETENTION: frames may retain classification + source links
   without image bytes when bytes are re-obtainable from source capture.
6. ARCHIVAL: canonical assets are immutable; removal requires explicit
   deprecation record, never silent deletion.
```

500 identical emulator Wi-Fi runs → 1 canonical asset + 499 hash-equal
references. Zero new bytes.

## 9. Failure → Asset loop (P4-G9)

```text
FailureEpisode (non-authoritative Harness artifact)
        ↓ triage, NOT automatic truth
RegressionAssetCandidate
        ↓ admission (§7)
RegressionAsset  (preserves link: source FailureEpisodeId / TraceRunId /
                  FrameId where present; missing links stay missing)
```

A FailureEpisode never becomes a truth label by itself. Admission determines
sufficiency; the episode's own layers (direct fact / correlation /
classification / hypothesis) stay distinguishable inside the link. The
FailureEpisode model remains `HARNESS_ARTIFACT_ONLY` — this gate adds a
consumer edge, not authority.

## 10. Regression corpus increment (P4-G10)

```text
Frozen: a confirmed real perception defect may become a permanent RegressionAsset.
Once admitted, future deployment candidates must not silently regress on it.

Rules:
  • FIXED EXPECTED OUTCOME: regression cases carry the corrected ground truth;
    the defect that created them is recorded as history.
  • CORRECTED GROUND TRUTH: annotation revisions are versioned; a regression
    case binds a GroundTruthVersion.
  • SUPERSEDED CASES: deprecated by explicit decision with reason; the
    deprecation is itself versioned.
  • DUPLICATE FAILURES: dedup via §8; the newest distinct failure mode wins.
  • CHANGED PRODUCT REQUIREMENTS: explicit scope change decision; never an
    implicit edit.

NEVER edit expected result merely because a new model fails it.
```

## 11. Golden corpus (P4-G11)

Golden Set: stable, manually reviewed, representative, versioned, critical
baseline coverage. Admission requires manual review + stability agreement +
coverage purpose. Golden is not the failure dump — Regression is.

Upgrade Regression → Golden: allowed only by explicit review decision when the
case has proven stable and representative over multiple evaluations; upgrades
are rare, versioned events.

## 12. Challenge corpus (P4-G12)

Deliberate difficulty: dark theme, low contrast, compression, blur, small
targets, overlapping text, dialogs, occlusion, partial viewport, unusual DPI,
large/small resolution. Challenge assets test boundaries and do NOT
automatically become training data. Training membership requires DatasetVersion
admission with split rules (§15), never implicit.

## 13. Holdout corpus (P4-G13)

```text
HoldoutSet: protected against overfitting to Golden/Regression/Challenge.

Rules:
  • NOT used for training.
  • NOT used for routine threshold tuning.
  • Limited visibility: one place of storage, admission-controlled.
  • Evaluated only at promotion/release time.
  • Versioned; membership changes are explicit.
  • LEAKAGE into training/validation = release-blocking evidence-integrity
    violation (§15).

Small-team realism: a directory with an access rule + an honest checklist,
not enterprise secrecy theater. The leakage check is mechanical (hash/session
comparison), which is cheap and sufficient at this stage.
```

## 14. Dataset vs Evaluation corpus (P4-G14)

```text
TrainingDataset != EvaluationCorpus   (frozen)

An image may relate to both, but role and version membership are explicit:
  • EvaluationCorpus membership: suite manifest reference (CorpusRole).
  • TrainingDataset membership: DatasetVersion asset list (§16).
No rule auto-places regression assets into training. Admission + split/leakage
rules apply separately.
```

## 15. Data leakage (P4-G15)

Leakage checks between Training / Validation / Golden / Regression / Holdout:

1. Exact content hash duplicates across splits → ERROR.
2. Same capture/session origin across splits → ERROR (even different crops).
3. Near-duplicate frames (perceptual hash) → WARNING for review; policy may
   escalate.
4. Holdout leakage → **release-blocking**, regardless of severity.

Mechanical hash checks run on every DatasetVersion and suite promotion.

## 16. Annotation model (P4-G16)

```text
AnnotationRecord:
  annotationId, schemaVersion, annotator (human | model:X | consensus),
  sourceImageHash / FrameRef, reviewStatus (unreviewed | reviewed | challenged
  | corrected), modificationHistory[], groundTruthVersion, confidence (optional)

MODEL_PREDICTION vs GROUND_TRUTH_ANNOTATION: separated types.
Model output must never become truth by being copied into an annotation
record. Copying a prediction into ground truth requires a human review event
on the record.
```

## 17. DatasetVersion (P4-G17)

```text
DatasetVersion (immutable):
  datasetVersionId    (content-addressed manifest hash)
  semanticLabel       ("settings-chinese-rom/2026-08-01") — human label, coexists
  assetRefs[]         (exact admitted AssetIds)
  annotationRefs[]    (exact annotation versions)
  taxonomyCoverage    (per-dimension counts + gaps)
  creationDecision    (admission decision reference)
  predecessorVersion  (optional)
  splitInfo           (train/val split membership, leakage-checked)

Directory name/date is NOT identity. Identity = manifest content hash.
```

## 18. TrainingRun (P4-G18)

```text
TrainingRun (immutable record):
  trainingRunId, modelName, baseModel/initialization, datasetVersion,
  trainingConfigId, trainingCodeRevision, frameworkVersion, seed (where
  meaningful), hardwareProfile (where meaningful), startedAt, terminalOutcome,
  producedCheckpoints[]  (checkpointName + artifactHash each)

A TrainingRun may produce multiple checkpoints. best.pt is a checkpoint role,
never an identity.
```

## 19. Checkpoint vs model artifact (P4-G19)

```text
modelName      = stable family/product identity ("android_ui_detection_yolov8")
modelVersion   = governance version (semantic label assigned at promotion)
modelId        = full SHA-256 of exact artifact — authoritative identity
checkpointName = training-run checkpoint role (best / last / epoch_N)

"best" MUST NOT become modelName. checkpointName MUST NOT affect modelId.
Current production state: modelName = android_ui_detection_yolov8,
modelId = full SHA-256(best.pt), modelVersion = UNKNOWN (governance label
absent — honest), checkpointName = best (informational only).
```

## 20. ModelManifest (P4-G20)

```text
ModelManifest (immutable per modelId):
  schemaVersion, modelName, modelVersion (nullable — UNKNOWN until assigned),
  modelId (required), modelFormat, modelFamily, classVocabulary,
  trainingRunId (nullable), datasetVersion (nullable), trainingConfigId
  (nullable), evaluationStatus, predecessorModelId (nullable), provenance

Missing fields stay absent/UNKNOWN. Current model: only modelName + modelId
+ modelFormat are truthfully fillable; the rest is UNKNOWN until training/
dataset provenance exists. Never fabricate.
```

## 21. Model lifecycle (P4-G21)

```text
States: CANDIDATE → VALIDATED → PROMOTED → ACTIVE → RETIRED
                                        ↘ REJECTED (from any pre-ACTIVE state)

ROLLED_BACK = EVENT, not state.
  ACTIVE V2 → (deployment event: rollback) → V2 → RETIRED(reason=rolled_back),
  V1 → ACTIVE again. The artifact itself was never invalidated.

PROMOTED != ACTIVE (frozen):
  PROMOTED = passed evaluation gates, approved for deployment eligibility.
  ACTIVE   = actually deployed as the current production deployment.
  A PROMOTED model may be active, queued, or rolled-back-to.
```

## 22. Classification falsifiers (P4-G83)

```text
CF-01  One asset may be RECORDED_REALITY + REGRESSION + ONEUI + SWITCH_STATE
       + dark + CRITICAL without a flattened combined enum.
CF-02  CorpusRole and Provenance are independent dimensions.
CF-03  SystemFamily and EvaluationProfile are independent.
CF-04  One asset reused by Runtime Replay and ML Evaluation with explicit
       relationships (content-addressed, §6).
CF-05  Specialist profile scope cannot alter an asset's intrinsic
       classification.
```

## 23. Config inventory & manifest (P4-G22, P4-G23)

Effective perception-affecting configuration (from Phase 3 audit):

```text
PERCEPTION CONFIG (evidence-affecting):
  label-mapping.json (mappings, spatial, detection confidence)
  YOLO imgsz=640, device=cpu
  OCR backend/mode, text_score=0.5, OCR parallel workers
  fusion constants (max_ocr_distance_ratio 0.055, chevron tolerance 40px,
  interactive label set)
  preprocessing (maxWidth 720, cropTop/Bottom 0.0625)
  coordinate normalization revision (v1)

DEPLOYMENT CONFIG (paths, sockets, Python binary, restart budget)
HOST OPERATIONAL CONFIG (health timeouts, poll intervals)
TRAINING CONFIG (future)
```

```text
PerceptionConfigManifest (proposed, not activated):
  canonical serialization: sorted keys, no whitespace, UTF-8 JSON
  fields: schemaVersion, labelMappingVersion/hash, yoloParams, ocrParams,
          fusionParams, preprocessingParams, coordinateNormalizationRevision,
          modelReference (modelId), pipelineVersion
  configId = SHA-256(canonical serialization)

  • Includes everything that can materially change evidence.
  • Excludes machine/process details (restart timeout is NOT perception config).
  • Identifies referenced label/config assets by hash.
  • configHash (SHA-256 of label-mapping.json) remains HISTORICAL/compat
    metadata; it is never reinterpreted as configId.
```

## 24. Config lifecycle (P4-G24)

```text
DRAFT → VALIDATED → PROMOTED → ACTIVE → RETIRED | REJECTED

A config change capable of changing perception evidence must pass the
relevant Evaluation Suite. "Only config changed" is NOT an evaluation bypass
(CL-12). Fusion constant extraction in Phase 3 did not change effective
behavior — it only relocated ownership.
```

## 25. Deployment identity (P4-G25, P4-G26)

```text
PerceptionDeploymentCandidate (immutable, release unit — NOT model alone):
  serviceVersion, schemaVersion, modelId, configId, evaluationProfileId,
  ocrBackend, pipelineRevision

PerceptionDeploymentIdentity (frozen, derived):
  identityId = SHA-256(canonical candidate serialization)
  → every produced evidence artifact can answer:
    "which exact service/model/config/profile produced this?"

DeploymentInstance (runtime):
  identityId + host/session info (socket path, PID, start time)
  — operational, never part of identity.

Candidate (release combination) vs Identity (hash of it) vs Instance
(running process) stay separate.
```

## 26. EvaluationProfile (P4-G27, P4-G28)

```text
EvaluationProfile (immutable, versioned):
  profileId, profileName, applicability[]:
    { systemFamily, stance: PRIMARY | SECONDARY | OUT_OF_SCOPE }

OUT_OF_SCOPE is a real value, not weight=0. Example ONEUI_SPECIALIST:
  ONEUI: PRIMARY | AOSP: SECONDARY | HyperOS/ColorOS: OUT_OF_SCOPE.

ProfileSafetyRule (frozen):
  A specialist may have weaker OUT_OF_SCOPE scores ONLY IF deployment
  selection guarantees it is never used in those contexts. Required proof:
  selection contract keyed on observable environment facts with a default-
  safe fallback (generalist or refusal). A profile label alone waives nothing.
```

## 27. Selection contract (P4-G29)

```text
Selection inputs (allowed): OS family, device family, display characteristics,
declared deployment profile — observable environment facts.
Selection MUST NOT read: BusinessIntent, SemanticGoal, Agent decisions.

Selection is infrastructure/environment mechanism, below Runtime semantics.
NOT IMPLEMENTED in this phase — no selector until evidence purchases one
(multi-model deployment pressure). Current stage: single generalist model,
no selection path.
```

## 28. Specialist / Generalist policy (P4-G66, P4-G67)

```text
Specialist: PRIMARY strict gates, SECONDARY declared floor, OUT_OF_SCOPE
informational. Strong unrelated scores never compensate PRIMARY failures.
Universal hard gates always apply (§31).

Generalist: broad system coverage required; per-family floors prevent one
excellent family hiding another supported family's failure via weighted
average. Weighted aggregation may not mask a PRIMARY floor breach.
```

## 29. Scorecard & metrics (P4-G30, P4-G31, P4-G68)

```text
NO_SINGLE_SCORE_AUTHORITY (frozen).

Hierarchy:
  AssetResult → SliceMetrics → CategoryScore → ProfileScorecard → OverallSummary

Dimensions (minimum): BySystem, ByPerceptionTask, ByComponentClass,
ByCorpusRole, ByDifficulty, ByCriticality, Performance, Safety.

OverallSummary is informational only. Promotion authority lives in:
  Hard Gates + Profile Gates + Relative Regression Gates + Evidence Sufficiency.

Metrics by task (only what applies):
  Detection:  precision, recall, F1, mAP, IoU
  Bounds:     IoU, center error (normalized), coordinate error
  OCR:        CER, WER, text match
  SwitchState: accuracy, precision/recall per ON/OFF/UNKNOWN
  Fusion:     final evidence correctness, false merge, missed merge
  UnknownSafety: fabrication rate, false-positive rate
```

## 30. Critical error metrics (P4-G32)

Disproportionate-impact errors, candidates for hard-gate membership:

```text
  • fabricated element (detection with no ground-truth counterpart)
  • wrong switch state on a CRITICAL switch asset
  • coordinate outside valid [0,1] frame or schema violation
  • critical object missing (ground-truth CRITICAL element undetected)
  • incorrect evidence schema version
  • UNKNOWN converted into false certainty (fabrication)
```

Which become hard gates is decided after baseline (§37); the class itself is
non-waivable where a gate exists.

## 31. Hard gates / profile gates / relative regression (P4-G33…P4-G35)

```text
HardGates (universal, non-waivable; no numeric thresholds until baseline):
  HG-1 Schema compatibility (uniclaw.localVisionEvidence.v1)
  HG-2 Coordinate contract (full-screen normalized [0,1], top-left)
  HG-3 No fabricated evidence / UNKNOWN safety (fabrication rate floor)
  HG-4 Critical Golden cases (MUST_PASS where ground truth is CRITICAL)
  HG-5 Crash / fail-closed safety (no positive evidence on failure paths)
  HG-6 Evidence provenance integrity (modelId/configId present, honest)
  Specialist profiles cannot waive HG-1…HG-6.

ProfileGates:
  PRIMARY: strict quality gates per applicable task.
  SECONDARY: minimum declared floor.
  OUT_OF_SCOPE: informational, non-promotional.

RelativeRegressionGate (vs current ACTIVE deployment):
  candidate overall passes minimum AND no critical regression AND PRIMARY
  metrics within allowed degradation AND Regression Corpus maintains required
  pass level. Numeric tolerances: FROZEN ONLY AFTER BASELINE (§37).

RegressionZeroTolerance (P4-G36):
  Regression cases with CRITICAL real-failure ground truth → MUST_PASS.
  Noisy/ambiguous cases: statistical quality treatment, never silently
  100%-forever unless ground truth justifies it.
```

## 32. Performance evaluation (P4-G37…P4-G39)

```text
PerformanceEvaluation (first-class category):
  Cold startup, warm startup, YOLO latency, OCR latency, fusion latency,
  total analyze latency; p50/p95/p99 (only with sufficient samples —
  no p99 from tiny counts); memory: peak RSS; CPU; timeout rate; crash rate.

PerformanceProfile (per run): hardware/device, CPU arch, Python version,
runtime/backend versions, worker count, input resolution, warm/cold state.
Latency from incompatible environments is never compared directly.

PerformanceGate: candidate not promoted on accuracy alone if latency/memory
regression makes Runtime unusable. Thresholds derived from ACTIVE baseline
(§37), not invented here.

Benchmark run status: VALID | INVALID | INSUFFICIENT.
Noisy/missing benchmark data is never interpreted as a performance pass.
```

## 33. Evaluation suite & run (P4-G40, P4-G41)

```text
EvaluationSuite (immutable, versioned):
  suiteId (content hash), assetSelection (by profile, corpusRole, systemFamily,
  task, criticality, executionLevel), metricContracts, gateSpecs.

EvaluationRun (immutable, never overwritten):
  evaluationRunId, deploymentCandidateId, evaluationSuiteVersion,
  executionBackend, environmentProfile, assetResults[], metrics, scorecard,
  hardGateResults, relativeComparison (vs ACTIVE), terminalOutcome.
```

## 34. Release decision & promotion authority (P4-G42, P4-G43)

```text
ReleaseDecision ∈ { PROMOTE, REJECT, INSUFFICIENT_EVIDENCE }
Decision cites EvaluationRun(s). Unknown/incomplete evaluation is never
forced into PASS/FAIL (CL-11).

PromotionAuthority: Project Leader / human-approved at current stage.
Evaluation computes evidence and gate results; it MUST NOT silently mutate
ACTIVE. No autonomous production deployment.
```

## 35. Change risk & evidence matrix (P4-G44…P4-G46)

```text
ChangeRisk:
  CODE_REFACTOR | CONFIG_CHANGE | FUSION_CHANGE | OCR_CHANGE |
  MODEL_CHANGE | MODEL_AND_CONFIG_CHANGE | SCHEMA_CHANGE

EvidenceRequirementMatrix (directional; not frozen until Phase 4 baselines):
  CODE_REFACTOR          → L0 + L1 + L2 recorded subset
  CONFIG/FUSION_CHANGE   → L0 + L2 full + Golden/Regression/Challenge suites
  OCR_CHANGE             → L0 + L2 full + OCR task metrics + performance
  MODEL_CHANGE           → full EvaluationSuite + Holdout + performance + L3 critical set
  MODEL_AND_CONFIG       → MODEL_CHANGE ∪ CONFIG_CHANGE
  SCHEMA_CHANGE          → contract review + L0..L3 + adapter compatibility proof
  PRODUCTION_RELEASE     → hard gates + profile scorecard + relative comparison
                           + L3 + selected L4 where required

CostTiers: CHEAP (L0/L1) < LOW (L2) < MEDIUM (L3) < EXPENSIVE (L4).
Choose cheapest evidence sufficient for the risk class; expensive layers
are not rerun when lower layers already prove equivalence (EF-02, EF-04).
```

## 36. Caching, incremental evaluation, scripts (P4-G47…P4-G50)

```text
Caching: a prior EvaluationRun result is reusable ONLY when all identities
match: AssetId, ModelId, ConfigId, ServiceVersion, evaluationScriptRevision,
environment constraints where metric-sensitive. Any change invalidates the
relevant slice (EF-04…EF-06).

Incremental: changed code/config/model → impacted asset slices → targeted
suite now; full release suite required before promotion. Targeted never
substitutes for release evaluation (EF-08).

EvaluationScripts (owned, versioned by code revision):
  evaluate_detection, evaluate_ocr, evaluate_fusion, evaluate_deployment,
  benchmark_performance, compare_active, score_release
  (names indicative; exact files decided at implementation slice).

ScoreProvenance (required on every score):
  evaluationSuiteVersion + groundTruthVersion + evaluatorRevision.
A score without these three is meaningless (P4-G50).
```

## 37. Baseline & threshold calibration (P4-G64, P4-G65)

```text
CurrentActiveBaseline (required before any numeric threshold):
  • modelId (full SHA-256 of current best.pt)
  • current effective config inventory + configHash
  • serviceVersion (1.0) + schema version
  • run production deployment against initial EvaluationSuite
  • record metric distribution per dimension

ThresholdCalibration process:
  baseline ACTIVE → evaluate corpus → observe distribution → identify
  critical requirements → propose thresholds → falsify → freeze.

NumericThresholds: BASELINE_REQUIRED.
NO invented values (no "Overall ≥ 90", no "mAP drop ≤ 2%", no "P95 ≤ 500ms").
```

## 38. Execution-layer integration (P4-G51…P4-G56)

```text
SimulationRole: proves Runtime semantics, failure handling, UNKNOWN handling,
retry/recovery, action outcome pressure. NEVER counted as visual model
accuracy evidence.

ReplayRole: tests Agent/Container/Traversal/GoalEvidence/failure semantics
with recorded perception; does NOT rerun the model. NEVER counted as
current-model perception accuracy evidence.

RecordedImageRole (L2): primary ML regression workhorse. Stored real
screenshot + current candidate deployment → fresh YOLO/OCR/fusion inference.
High-volume model/config evaluation without emulator.

EmulatorRole (L3): produces CaptureSession, Frames, screenshots, raw
perception, Observations, TraceRuns, actions, post-action evidence.
Admission rules apply (§7) — not every frame becomes canonical.

RealDeviceRole (L4): final release sampling, new device/system coverage,
emulator/real gap detection, hardware performance, reality falsification.
Not required on every candidate.

EmulatorRealGap: same Scenario at multiple levels with divergent results
→ RealityGap evidence record, never silent overwrite of lower-level results.
```

## 39. Evidence graph & provenance (P4-G57, P4-G58)

```text
Nodes: Scenario, EvaluationAsset, CaptureSession, Frame, Screenshot,
GroundTruth, DatasetVersion, TrainingRun, ModelArtifact, ConfigManifest,
DeploymentCandidate, EvaluationRun, TraceRun, FailureEpisode, ReleaseDecision.

Edges: explicitly linked partial graph. NO mandatory linear chain.
Missing relationships stay UNKNOWN. No inference from timestamps, filenames,
or directory proximity.
```

## 40. Storage ownership & dedup (P4-G59, P4-G60)

```text
StorageOwnership:
  Git (uni-agent):        manifests, scripts, evaluator code, policies,
                          decision docs
  Content-addressed store: model artifacts, screenshots, dataset bytes,
                          evaluation reports (same bytes stored once;
                          manifests reference shared AssetId)
  Harness corpus:         Runtime reality assets (existing ownership, not
                          duplicated into a parallel perception corpus)
  Current model size (~6.2 MB) remains in Git; migrate to the content-
  addressed store when volume pressure appears (multi-version models).
```

## 41. CI tiers (P4-G61)

```text
PR_FAST          L0 + L1                      blocking, minutes
NIGHTLY          L2 recorded subset           non-blocking (report)
MODEL_CANDIDATE  L2 full + performance        blocking for candidate admission
RELEASE_CANDIDATE L2 + L3 critical set + Holdout  blocking for promotion
MANUAL_REALITY   L4 sampling                  human-scheduled, gap evidence
```

No PR runs emulator/real-device suites.

## 42. Infrastructure failure semantics (P4-G62, P4-G63)

```text
MODEL_FAILED_EVALUATION (model scored poorly on valid evidence) → REJECT path.
EVALUATION_INFRASTRUCTURE_FAILED (missing model, broken evaluator, device
unavailable, corrupt asset, missing annotation) → INSUFFICIENT_EVIDENCE.
Never infrastructure failure → model rejection by default.
```

## 43. Rollback, observation, drift (P4-G72…P4-G74)

```text
Rollback: restores a known DeploymentIdentity {serviceVersion, schemaVersion,
modelId, configId, profile}. No retraining. Deployment operation, not model
mutation (CL-18).

PostDeploymentObservation: ACTIVE DeploymentIdentity attached to future
perception evidence / CaptureSession / Trace where observationally
appropriate — Harness-owned attachment. Never leaks into Runtime semantic
authority.

DriftFeedback loop:
  ACTIVE → Runtime reality → Trace/FailureEpisode → perception triage →
  AssetCandidate → admission → Regression/DatasetCandidate.
  No automatic retraining in Phase 4 baseline.
```

## 44. Training automation & authority (P4-G75, P4-G76)

```text
TrainingReproducibility: YES — scripts + manifests making a TrainingRun
replayable.
AutomaticRetraining: NO — a FailureEpisode alone never triggers training
or deployment.

Automated (mechanical): metric computation, hashing, dedup, suite execution,
regression comparison, report generation.
Human gate (Project Leader): ground-truth disputes, ambiguous admission,
profile scope change, threshold policy, production promotion.
```

## 45. MVP slices (P4-G77, P4-G78, P4-G79)

```text
RecommendedImplementationSlices (dependency-ordered):
  P4-1  Evaluation Asset schema + taxonomy + manifests
  P4-2  Recorded Image Evaluation Runner (L2)
  P4-3  Metrics + Scorecard
  P4-4  Current ACTIVE baseline
  P4-5  EvaluationProfile + ReleasePolicy
  P4-6  Hard / Relative promotion gate
  P4-7  Performance benchmark
  P4-8  Regression asset admission (+ FailureEpisode → candidate triage)
  P4-9  DatasetVersion + annotation provenance
  P4-10 TrainingRun + ModelManifest
  P4-11 ConfigManifest + configId
  P4-12 DeploymentIdentity + promotion/rollback
  P4-13 Emulator integration (L3)
  P4-14 Real-device release sampling (L4)
  P4-15 Failure → Regression candidate closure

FirstVerticalSlice (P4-1…P4-4):
  CURRENT ACTIVE deployment + existing recorded reality screenshots +
  asset taxonomy + L2 evaluation runner + initial scorecard
  → FIRST_PERCEPTION_EVALUATION_BASELINE
  No training involved. Validates evaluation infrastructure before any
  model lifecycle automation.

First purchased assets: asset manifest/schema, suite manifest/schema,
evaluator, metric calculators, scorecard report, candidate-vs-active
comparator, performance benchmark script, fixture corpus, report schema.
File-based immutable manifests; registries/services only when proven
insufficient.
```

## 46. Phase 4 forbidden scope (P4-G80)

```text
Phase4ForbiddenScope:
  ✗ semantic authority into Perception
  ✗ model score modifying Runtime GoalEvidence
  ✗ FailureEpisode triggering automatic Runtime recovery
  ✗ Evaluation system dispatching DeviceAction
  ✗ training during Runtime startup
  ✗ deployment during Runtime execution
  ✗ Asset Registry as God Object / one generic registry for everything
  ✗ distributed ML infrastructure without scale pressure
  ✗ cloud requirement for local baseline
  ✗ duplicate corpus replacing Harness reality assets
```

## 47. Ownership matrix (P4-G85)

```text
Artifact                 │ Owner (mutable state)      │ Mutable?
─────────────────────────┼────────────────────────────┼──────────
EvaluationAsset          │ Perception Evaluation Gov  │ immutable (identity)
GroundTruth              │ Annotation owner (human)   │ versioned-immutable
EvaluationSuite          │ Perception Evaluation Gov  │ versioned-immutable
EvaluationProfile        │ Project Leader             │ versioned-immutable
ReleasePolicy            │ Project Leader             │ versioned-immutable
DatasetVersion           │ Training Gov (future)      │ immutable
TrainingRun              │ Training pipeline          │ immutable record
ModelManifest            │ Model Gov (Project Leader) │ immutable per modelId
ConfigManifest           │ Config Gov (Project Leader)│ immutable
DeploymentCandidate      │ Release authority          │ immutable
EvaluationRun            │ Evaluation runner          │ immutable record
ReleaseDecision          │ Project Leader (human)     │ immutable record
ActiveDeployment pointer │ Release authority (PL)     │ ONE mutable pointer,
                         │                            │ ONE owner
VisionServiceHost state  │ VisionServiceHost          │ (Phase 2, unchanged)
```

One mutable state → one owner. One decision → one authority.

## 48. Falsifiers (P4-G81, P4-G82)

```text
ClosedLoopFalsifiers (CL-01…CL-20):
CL-01  Real/recorded failure becomes RegressionAssetCandidate.
CL-02  Candidate admission does not mutate source provenance.
CL-03  One asset participates in multiple suites without byte duplication.
CL-04  Training data cannot silently leak into Holdout.
CL-05  Candidate model cannot become ACTIVE without an EvaluationRun.
CL-06  High overall score cannot override a failed universal hard gate.
CL-07  Specialist passes with OUT_OF_SCOPE lows only when profile excludes it.
CL-08  PRIMARY category regression blocks promotion even if overall rises.
CL-09  MUST_PASS critical RegressionAsset failure blocks promotion.
CL-10  Performance regression can block promotion.
CL-11  Evaluation infra failure yields INSUFFICIENT_EVIDENCE, not false PASS.
CL-12  Config change triggers evaluation even when ModelId unchanged.
CL-13  Same ModelId + different ConfigId = distinct deployment candidates.
CL-14  Same deployment + suite + evaluator reproduces score within tolerance.
CL-15  Emulator evidence admits without automatically becoming Golden.
CL-16  Real-device failure links back to exact active DeploymentIdentity.
CL-17  Historical provenance cannot be silently upgraded.
CL-18  Rollback restores exact prior deployment identity.
CL-19  OUT_OF_SCOPE cannot participate as weighted PRIMARY score.
CL-20  No evaluation/release artifact acquires Runtime semantic authority.

EfficiencyFalsifiers (EF-01…EF-08):
EF-01  Repeated identical screenshot does not create duplicate canonical bytes.
EF-02  Code-only refactor does not schedule real-device full suite.
EF-03  Model change triggers required full perception evaluation.
EF-04  Unchanged slices reuse cached results when all identities match.
EF-05  Config change invalidates relevant cached perception scores.
EF-06  Evaluator revision change invalidates affected score cache.
EF-07  500 near-identical emulator frames do not become 500 canonical assets.
EF-08  Targeted dev suite never substitutes for final release suite.
```

## 49. Final architecture check (P4-G86)

```text
1.  Duplicated truth source?          NO — manifests reference content-addressed
                                      assets; ActiveDeployment single pointer.
2.  Same score reproducible?          YES — CL-14 + ScoreProvenance (§36).
3.  Promoted deployment → evidence?   YES — DeploymentCandidate cited by
                                      EvaluationRun cited by ReleaseDecision.
4.  Failure → producing deployment?   YES — CL-16 + post-deploy observation (§43).
5.  Failure → reusable asset?         YES — CL-01 + admission (§7).
6.  Asset shared across layers?       YES — content-addressed + graph (§6, §39).
7.  Specialist scored truthfully?     YES — profile stances + OUT_OF_SCOPE
                                      exclusion (CL-07, CL-19).
8.  Universal safety non-waivable?    YES — hard gates HG-1…HG-6 (§31).
9.  Expensive resources reserved?     YES — cost tiers + evidence matrix + EF-02.
10. Small team operable?              YES — file manifests, hash dedup, honest
                                      checklist; no enterprise secrecy theater.
11. Orthogonal classification?       YES — 9 dimensions + CF-01…CF-05.
12. Below Runtime semantic authority? YES — evidence only; CL-20; forbidden
                                      scope (§46).
```

No architecture pressure found. All twelve answers are YES, conditioned on
the constraints below.

---

## 50. Aggregate decision

```text
PERCEPTION_PLATFORM_PHASE_4_ML_EVALUATION_ASSET_AND_RELEASE_LIFECYCLE_GATE_RESULT

Decision:
  PURCHASE_WITH_CONSTRAINTS

  C1  Phase 3 graduation prerequisite gap (RE1–RE3 live equivalence, Host
      H1–H18, full Runtime regression, legacy removal) must close before
      any Phase 4 implementation authorization.
  C2  Numeric thresholds: BASELINE_REQUIRED — none frozen here.
  C3  First vertical slice (P4-1…P4-4) is the only authorized implementation
      entry point, and only after C1.

ClosedLoop:                 PASS (designed; CL-01…CL-20 as acceptance)
AssetTaxonomy:              9 orthogonal dimensions (A–I, §1)
AssetIdentity:              content-addressed {ContentHash, SchemaVersion};
                            metadata separated (§2)
AssetAdmission:             10-point review; ADMIT | REJECT | DEFER (§7)
DedupStrategy:              exact-hash first; perceptual dedup deferred (§8)
ScenarioAssetRelationship:  many-to-many explicit graph; one Scenario per
                            contract, five execution levels (§3)
ExecutionEvidenceLadder:    L0..L4 with cost/determinism/CI matrix (§4)
SimulationRole:             Runtime semantics only; NOT visual accuracy (§38)
ReplayRole:                 Runtime behavior; NOT current-model accuracy (§38)
RecordedImageRole:          L2 primary ML regression workhorse (§38)
EmulatorRole:               L3 asset production + gating; admission-gated (§38)
RealDeviceRole:             L4 release sampling + gap detection (§38)
EvidenceGraph:              explicit linked partial graph, no linear chain (§39)
DatasetBoundary:            TrainingDataset != EvaluationCorpus (§14)
AnnotationBoundary:         MODEL_PREDICTION vs GROUND_TRUTH_ANNOTATION (§16)
TrainingRun:                immutable record; checkpoints ≠ identity (§18)
ModelManifest:              minimum fields; UNKNOWN where absent (§20)
ModelLifecycle:             6 states; ROLLED_BACK=event; PROMOTED != ACTIVE (§21)
ConfigManifest:             canonical serialization → configId; configHash
                            stays historical (§23)
ConfigLifecycle:            DRAFT→VALIDATED→PROMOTED→ACTIVE→RETIRED|REJECTED (§24)
DeploymentIdentity:         candidate/identity/instance separated (§25)
EvaluationProfile:          PRIMARY | SECONDARY | OUT_OF_SCOPE stances (§26)
SpecialistPolicy:           strict PRIMARY, floor SECONDARY, routing proof
                            for OUT_OF_SCOPE waiver (§27, §28)
GeneralistPolicy:           per-family floors; no weighted-average masking (§28)
Scorecard:                  multidimensional; OverallSummary informational (§29)
HardGates:                  HG-1..HG-6 universal, non-waivable, threshold-free
                            until baseline (§31)
ProfileGates:               PRIMARY strict / SECONDARY floor / OUT_OF_SCOPE
                            informational (§31)
RelativeRegressionGate:     absolute + relative vs ACTIVE; tolerances post-
                            baseline (§31)
PerformanceEvaluation:      first-class; p50/p95/p99 with sample guards (§32)
PerformanceGate:            accuracy never alone; thresholds from baseline (§32)
HoldoutPolicy:              protected; leakage release-blocking (§13)
LeakagePolicy:              hash/session/near-duplicate checks (§15)
EvaluationSuite:            immutable, versioned, dimension-selected (§33)
EvaluationRun:              immutable record, never overwritten (§33)
ReleaseDecision:            PROMOTE | REJECT | INSUFFICIENT_EVIDENCE (§34)
PromotionAuthority:         Project Leader / human-approved (§34)
Rollback:                   restore exact DeploymentIdentity; no retraining (§43)
FailureToAssetLoop:         triage + admission; no automatic truth (§9)
ChangeRiskMatrix:           7 classes (§35)
EvidenceRequirementMatrix:  directional by risk class (§35)
CachingPolicy:              identity-matched reuse only (§36)
CITiers:                    PR_FAST / NIGHTLY / MODEL_CANDIDATE /
                            RELEASE_CANDIDATE / MANUAL_REALITY (§41)
StorageOwnership:           Git=manifests/code; content-addressed store=bytes;
                            Harness corpus=Runtime reality (§40)
OwnershipMatrix:            14 artifacts + ActiveDeployment pointer (§47)

ClosedLoopFalsifiers:       CL-01..CL-20 (§48)
EfficiencyFalsifiers:       EF-01..EF-08 (§48)
ClassificationFalsifiers:   CF-01..CF-05 (§22)

NumericThresholds:          BASELINE_REQUIRED

FirstVerticalSlice:         P4-1..P4-4 → FIRST_PERCEPTION_EVALUATION_BASELINE
                            from current ACTIVE + recorded reality screenshots,
                            no training (§45)

RecommendedImplementationSlices:
  P4-1 Asset schema → P4-2 L2 runner → P4-3 metrics/scorecard → P4-4 baseline
  → P4-5 profile/policy → P4-6 gates → P4-7 performance → P4-8 regression
  admission → P4-9 dataset/annotation → P4-10 training/manifest → P4-11
  configId → P4-12 deployment/promotion/rollback → P4-13 emulator →
  P4-14 real device → P4-15 failure→asset closure (§45)

Phase4ForbiddenScope:       8 items (§46)

RuntimeDelta:               NONE
SemanticDelta:              NONE
AuthorityDelta:             NONE
```

## 51. Next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_3_GRADUATION_REVIEW
  (prerequisite — must precede any Phase 4 implementation)

After Phase 3 graduation:

PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_FIRST_EVALUATION_BASELINE_IMPLEMENTATION_GATE
  (authorizes only the FirstVerticalSlice P4-1..P4-4)

NO_AUTOMATIC_IMPLEMENTATION
```

`PERCEPTION_PLATFORM_PHASE_4_ML_EVALUATION_ASSET_AND_RELEASE_LIFECYCLE_GATE_RESULT`

STOP.
