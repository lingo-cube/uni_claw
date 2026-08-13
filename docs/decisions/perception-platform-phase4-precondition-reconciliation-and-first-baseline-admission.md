# Perception Platform Phase 4 — Precondition Reconciliation & First Baseline Admission

> Date: 2026-08-12
> Role: Project Leader (Opus) / Precondition Reconciliation & Bounded Slice Admission
> Result: `PERCEPTION_PLATFORM_PHASE_4_PRECONDITION_RECONCILIATION_AND_FIRST_BASELINE_ADMISSION_RESULT`
> Decision: **PURCHASE_WITH_CONSTRAINTS**
> Implementation: **NOT YET AUTHORIZED** (admission only; separate task authorizes implementation)

---

## R1 — Precondition reconciliation

```text
Phase3Precondition:
  DISCHARGED

Phase 3 graduation (docs/decisions/perception-platform-phase3-graduation-result.md)
verified this review: legacy removed, 0 active legacy references, Python tests
15/15, full Runtime regression 857/857, RE1–RE4 + Host H1–H18 + P4 accepted.
```

Each frozen Phase 4 gate decision re-checked against Phase 3 graduation facts:

| Phase 4 frozen decision | Phase 3 graduation fact | Contradiction? |
|---|---|---|
| Multidimensional EvaluationAsset taxonomy | Perception = evidence only; provenance preserved | NONE |
| Scenario != EvaluationAsset | Scenario catalog + reality assets unchanged | NONE |
| Partial evidence graph | FailureEpisode/reality model unchanged | NONE |
| Ladder L0–L4; L2 primary ML workhorse | L2 does not need emulator (live NOT_EXECUTABLE is irrelevant to L2) | NONE |
| GENERALIST/SPECIALIST profile concept | Current single generalist model — first baseline uses INITIAL profile | NONE |
| PRIMARY/SECONDARY/OUT_OF_SCOPE | Deployment-support declaration, not scoring convenience | NONE |
| NO_SINGLE_SCORE_AUTHORITY | unchanged | NONE |
| Universal non-waivable Hard Gates | threshold-free until baseline | NONE |
| Relative Candidate-vs-ACTIVE gate | first baseline BECOMES the ACTIVE comparison target | NONE |
| Performance evaluation | separable from accuracy | NONE |
| FailureEpisode → RegressionAssetCandidate | FailureEpisode stays Harness-only | NONE |
| modelName/modelVersion/modelId/checkpointName distinct | graduation froze full-SHA-256 modelId + modelName | NONE |
| configId future identity; configHash stays PARTIAL | graduation froze PARTIAL compatibility stance | NONE |
| PerceptionDeploymentIdentity | serviceVersion 1.0 + schema v1 + modelId available | NONE |
| Project Leader/human promotion authority | unchanged | NONE |

```text
Phase4Architecture:
  UNCHANGED
```

The Phase 4 gate ([perception-platform-phase4-ml-evaluation-asset-and-release-lifecycle-gate.md](perception-platform-phase4-ml-evaluation-asset-and-release-lifecycle-gate.md))
remains the single architecture authority. This admission purchases only its
FirstVerticalSlice (P4-1…P4-4).

---

## R2 — First slice purpose

The slice answers: **"How good is the CURRENT ACTIVE perception deployment,
using assets we already trust?"** No training, no thresholds, no promotion.
Output is a discovery baseline: quality, weaknesses, taxonomy coverage, corpus
gaps, performance, usable metrics, threshold evidence.

## R3 — Current ACTIVE deployment snapshot

```text
CurrentActiveDeployment (snapshot strategy — record what exists truthfully):

  ServiceVersion:       1.0            (GET /version serviceVersion)
  SchemaVersion:        uniclaw.localVisionEvidence.v1
  ModelName:            android_ui_detection_yolov8
  ModelId:              3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782
                        (full 64-char SHA-256 — frozen)
  OcrBackend:           rapidocr
  PipelineRevision:     uniclaw_perception __version__ = 1.0.0
  ConfigIdentity:       LEGACY_PARTIAL_CONFIG_IDENTITY
                        = sha256(label-mapping.json):a85d7e78a27cde23…
                        (truthful PARTIAL stance; canonical configId NOT fabricated)
```

`LEGACY_PARTIAL_CONFIG_IDENTITY` is the approved stance until P4-11 (ConfigManifest)
builds canonical configId.

## R4 — Asset discovery (repository truth, this review)

```text
Discovered assets:

A1  uni-claw/artifacts/assets/screenshots/settings-home-api35-full-20260803.png
    Source: Harness reality corpus (emulator, API35, recorded 2026-08-03)
    Provenance: RECORDED_REALITY   |  Ground truth: NONE (annotation absent)
    Scenario linkage: settings-home reality capture
    Classification: NEEDS_GROUND_TRUTH  (L2-ready screenshot, unscorable today)

A2  uni-claw/artifacts/assets/screenshots/settings-diag-20260803.png
    Source: Harness reality corpus (recorded 2026-08-03)
    Provenance: RECORDED_REALITY   |  Ground truth: NONE
    Classification: NEEDS_GROUND_TRUTH

A3  platforms/perception/tests/fixtures/vision_test_controlled_screen.evidence.json
    Stored old perception OUTPUT (not a screenshot; controlled synthetic screen)
    Classification: INFORMATIONAL_ONLY (stored output cannot prove current-model
    accuracy — R11 forbids replay-as-accuracy; useful as schema fixture)

A4  platforms/perception/tests/fixtures/vision_test_controlled_screen.android-ui-yolo.evidence.json
    Same as A3 — INFORMATIONAL_ONLY

A5  platforms/perception/tests/fixtures/settings-real.android-ui-yolo.evidence.json
    Stored old perception OUTPUT; references real device screenshot
    (Screenshot_2026-07-26-17-47-23-33_fc704e6b…jpg, PKJ110)
    Underlying screenshot NOT verified present in repo
    Classification: INFORMATIONAL_ONLY (source screenshot may be re-locatable;
    if recovered, the screenshot itself becomes ADMIT_READY subject to GT)

A6  uni-claw/artifacts/runs/integration/adb-wifi-navigate/
    Historical Wi-Fi navigation run assets
    Classification: NEEDS_GROUND_TRUTH (screenshots may exist inside run bundle)

A7  No dedicated switch ON/OFF ground-truth image set found
    Classification: NOT_PRESENT → coverage gap (SwitchState GT absent)

A8  No holdout set exists
    HoldoutStatus: NONE — reported as gap, does not block first baseline (R8)

ExistingAssetReuse: reference-by-path/AssetId manifests; NO byte duplication.
Assets stay in their current Harness locations; the suite manifest references them.
```

## R5–R6 — Asset schema + identity

```text
AssetSchema (purchased, minimal):
  Nine orthogonal dimensions (Provenance, CorpusRole, SystemFamily,
  ScenarioDomain, PerceptionTask, ComponentClass, Difficulty, Criticality,
  Theme tags) — no mega-enum. CF-01 example representable as specified.

  SystemFamily values evidenced today: ANDROID_AOSP (emulator API35 image —
  from device/manifest evidence only), OTHER, UNKNOWN. OneUI/HyperOS/ColorOS
  values may be DECLARED in schema but are UNASSESSED until evidence exists.

AssetIdentity (purchased):
  AssetId = "sha256:{ContentHash}"
  Identity = { ContentHash, AssetSchemaVersion }  → move/rename/metadata-change
  invariant. Filename is never identity. Suite membership is a manifest
  reference, never a byte copy.
```

## R7 — Ground truth

```text
GroundTruth (purchased):
  PerceptionPrediction != GroundTruth (separate record types).
  Task-scoped truth only: expected presence / class / text / bounds /
  switch state / expected UNKNOWN / expected absence — each optional per asset.
  An asset need not label every task. GT is versioned (GroundTruthVersion);
  human review events required for GT edits.
```

## R8–R10 — Corpus roles, system family, profile

```text
Initial corpus: GOLDEN / REGRESSION / CHALLENGE / CALIBRATION only where
evidence supports the label. First baseline reality: mostly CALIBRATION +
GOLDEN candidates from A1/A2 after annotation; REGRESSION requires admitted
failure evidence (R24) — likely EMPTY in first baseline; CHALLENGE likely
EMPTY (no deliberate difficulty assets yet). Honest zeros.

HoldoutStatus: NONE. No fake holdout from development-used assets. Gap
reported. Does not block the first baseline.

SystemFamily: never inferred from visual appearance. Emulator API35 AOSP
image may be ANDROID_AOSP with manifest evidence; everything else UNKNOWN.

ProfileBaseline: INITIAL_CURRENT_DEPLOYMENT_PROFILE with PRIMARY = asset
categories actually covered + UNASSESSED for everything else. No OUT_OF_SCOPE
declarations as scoring convenience. No specialist profiles yet.
```

## R11–R14 — L2 runner, run identity, metrics, matching

```text
L2Runner (purchased):
  Fresh inference only: Decode → Preprocess → YOLO → OCR → Fusion → Remap →
  Serialize, executed by uniclaw_perception against the CURRENT deployment
  snapshot. Replaying stored evidence JSON as accuracy proof is FORBIDDEN (B5).

EvaluationRun identity (minimum): EvaluationRunId, EvaluationSuiteId/Version,
AssetId, ServiceVersion, SchemaVersion, ModelId, ConfigIdentity
(LEGACY_PARTIAL_CONFIG_IDENTITY until P4-11), EvaluatorRevision,
EnvironmentProfile. No canonical configId field yet.

InitialMetrics (only where GT exists):
  Detection: precision/recall/F1; Classification: accuracy/confusion;
  Bounds: IoU + normalized center error; OCR: exact/normalized text match
  (CER/WER only with sufficient GT quality); SwitchState: ON/OFF/UNKNOWN
  correctness; Fusion: final candidate correctness, false merge, missed merge;
  UNKNOWN safety: fabricated-positive rate.
  No meaningless metrics to fill the report (R13).

MatchingPolicy (purchased, explicit + versioned):
  Greedy deterministic assignment: sort predictions and GT objects by
  (class-compatible, IoU desc); one prediction ↔ one GT object; class
  compatibility required; text association only for OCR/fusion tasks;
  unmatched predictions = false positives, unmatched GT = false negatives.
  Policy version recorded in EvaluatorRevision. Matching semantics live in
  the versioned policy, never buried in the score formula.
```

## R15–R20 — Scorecard, coverage, weights, safety, performance

```text
Scorecard: multidimensional sections QUALITY / SAFETY / PERFORMANCE /
COVERAGE, each sliced BySystemFamily / ByPerceptionTask / ByComponentClass /
ByCorpusRole / ByCriticality. OverallSummary = presentation only.

CoverageModel: every score carries denominator + state:
  ASSESSED | PARTIALLY_ASSESSED | UNASSESSED | INSUFFICIENT_EVIDENCE.
  Zero assets in a category → UNASSESSED, never 100% (B9).

Weights: NONE in first baseline. Raw category scores only. Weights and
ReleasePolicy come after observing the baseline (R17).

SafetyBaseline: schema validity, coordinate-range validity, fabricated-positive
rate, UNKNOWN preservation, fail-closed on malformed evidence — scored where
executable, reported separately. No numeric thresholds frozen.

PerformanceBaseline: warm total analyze latency + YOLO/OCR/fusion split where
instrumented. Record CPU arch, Python version, modelId, input resolution,
worker topology, warm/cold. P50/P95 only; P99 only with sufficient samples;
sample count always recorded. QUALITY and PERFORMANCE stay separate sections.
```

## R21–R24 — Baseline immutability, suite, error corpus input

```text
Baseline: CURRENT_ACTIVE_BASELINE, immutable once recorded. New evaluator/
suite revision → NEW baseline record, never mutation. BaselineId +
EvaluationSuiteVersion + EvaluatorRevision + DeploymentIdentitySnapshot +
EnvironmentProfile; timestamp is history metadata only.

EvaluationSuite: smallest versioned suite; membership = AssetId references +
required tasks + taxonomy slices + backend (L2) + evaluator revision.
No per-suite directory copies.

ErrorCorpusInput: existing confirmed perception failures searched (legacy
evidence: subtitle phantom, search-box misclassification, type-blind matching
— documented in legacy-visual-perception-pressure-supplement.md). These are
RegressionAssetCandidate candidates only — the admission boundary is
demonstrated structurally; NO automatic admission; NO automatic
FailureEpisode → GroundTruth. Candidate records may be created pointing at
evidence; admission is a human decision later (P4-8).
```

## R25–R33 — Future readiness and constraints

```text
Specialist readiness: taxonomy + per-asset results support future
system-family slicing; no specialist routing implemented.

Profile-gate readiness: per-asset EvaluationResult preserved so a later
ReleasePolicy can re-aggregate PRIMARY/SECONDARY/OUT_OF_SCOPE without
re-running inference.

CorpusRole=PERFORMANCE allowed; benchmark inputs may reference quality assets.

L0/L1 not pulled into this slice; EvaluationAsset↔Scenario↔Replay links
preserved where known (partial graph).

L3 emulator / L4 real device: architecture-purchased, implementation-deferred.
First baseline must not depend on them.

Storage: file-based immutable manifests only. No database, no registry
service, no artifact server. Harness reality files referenced in place.

Cache: no cache system built; future cache key =
{AssetContentHash, ModelId, ConfigIdentity, ServiceVersion, EvaluatorRevision}
— any change invalidates (B7, B8).

NumericThresholds: BASELINE_REQUIRED — NOT_FROZEN. No overall/mAP/P95
thresholds authorized.

ReleaseAuthority: no promotion, no ACTIVE mutation, no candidate activation.
Evaluation outputs evidence only. Project Leader/human authority unchanged.
```

## R34 — Falsifiers B1–B20 (adopted as implementation acceptance)

```text
B1  Same bytes at different path → same AssetId.
B2  One asset in multiple roles/suites without byte duplication.
B3  Provenance unchanged when asset becomes Regression candidate.
B4  Prediction never stored as GroundTruth automatically.
B5  L2 runner performs fresh inference, never replays old output.
B6  Same deployment + asset + evaluator → deterministic result or declared tolerance.
B7  ModelId change invalidates evaluation reuse.
B8  Perception-affecting config identity change invalidates reuse.
B9  Zero assets in category → UNASSESSED, not perfect score.
B10 Overall score cannot hide per-category results.
B11 UNKNOWN/fabrication safety separately visible.
B12 Evaluation infra failure → INSUFFICIENT_EVIDENCE, not PASS.
B13 Performance sample count recorded.
B14 No P99 from insufficient samples.
B15 Existing reality asset referenced without byte duplication.
B16 CURRENT_ACTIVE_BASELINE immutable after creation.
B17 Evaluator revision change → new EvaluationRun identity.
B18 TrainingDataset role and EvaluationCorpus role not conflated.
B19 SystemFamily UNKNOWN stays UNKNOWN without evidence.
B20 No evaluation artifact changes Runtime semantic behavior.
```

## R35 — Implementation slices (authorized order)

```text
P4-1A  Asset schema + taxonomy + identity
P4-1B  GroundTruth + suite manifests
P4-2A  L2 fresh-inference runner
P4-2B  Prediction ↔ GroundTruth matcher (versioned policy)
P4-3A  Task metrics
P4-3B  Scorecard + coverage report
P4-3C  Minimal performance benchmark
P4-4A  Inventory/admit existing trustworthy assets (A1–A6)
P4-4B  Execute CURRENT ACTIVE baseline
P4-4C  Persist immutable baseline report
P4-4D  Analyze coverage gaps
```

## R36–R38 — Output and success criteria

Implementation must produce `FIRST_PERCEPTION_EVALUATION_BASELINE_RESULT` with:
DeploymentSnapshot, EvaluationSuite, AssetCount, AssetClassificationCoverage,
SystemFamilyCoverage, TaskCoverage, CorpusCoverage, QualityScorecard,
SafetyScorecard, PerformanceBaseline, CoverageGaps, UnassessedCategories,
GroundTruthGaps, AssetCandidates, CurrentActiveBaselineId,
NumericThresholds=NOT_FROZEN, RecommendedNextEvidenceWork.

Success = reproducible evaluation + reused assets + multidimensional
classification + sliceable scorecard + explicit gaps + reproducible
performance + no single-score authority + specialist-ready + no Runtime
changes + baseline evidence for later thresholds.

Honest holes (no OneUI assets, no holdout, minimal OCR GT, no real-device
performance) are EXPECTED and must be reported, never patched with synthetic
filler.

---

## Admission decision

```text
PERCEPTION_PLATFORM_PHASE_4_PRECONDITION_RECONCILIATION_AND_FIRST_BASELINE_ADMISSION_RESULT

Decision:
  PURCHASE_WITH_CONSTRAINTS

  C1  NumericThresholds = BASELINE_REQUIRED / NOT_FROZEN (no thresholds
      authorized in this slice).
  C2  Config identity recorded as LEGACY_PARTIAL_CONFIG_IDENTITY; canonical
      configId NOT fabricated (P4-11 remains future).
  C3  HoldoutStatus = NONE reported honestly; does not block.
  C4  No promotion, no ACTIVE mutation, no training, no specialist routing,
      no cache system, no L3/L4 execution in this slice.

Phase3Precondition:   DISCHARGED
Phase4Architecture:   UNCHANGED
AuthorizedSlice:      FIRST_PERCEPTION_EVALUATION_BASELINE (P4-1A…P4-4D)

AssetSchema:          purchased (§R5)
Taxonomy:             9 orthogonal dimensions, evidenced values only
GroundTruth:          task-scoped, versioned, never from prediction copies
AssetIdentity:        content-addressed {ContentHash, AssetSchemaVersion}
EvaluationSuite:      versioned manifest of AssetId references
L2Runner:             fresh current-model inference (replay-as-accuracy FORBIDDEN)
MatchingPolicy:       explicit versioned greedy policy, class+IoU gated
Metrics:              GT-supported only; no filler metrics
Scorecard:            QUALITY/SAFETY/PERFORMANCE/COVERAGE; sliceable
CoverageModel:        ASSESSED/PARTIALLY_ASSESSED/UNASSESSED/INSUFFICIENT_EVIDENCE
PerformanceBaseline:  warm analyze latency + stage split; sample-count guarded
CurrentActiveDeployment: snapshot strategy (§R3)
ExistingAssetReuse:   reference-only, no byte duplication
HoldoutStatus:        NONE (gap reported)
NumericThresholds:    BASELINE_REQUIRED

Falsifiers:           B1–B20
ImplementationSlices: P4-1A → P4-1B → P4-2A → P4-2B → P4-3A → P4-3B →
                      P4-3C → P4-4A → P4-4B → P4-4C → P4-4D

ForbiddenScope:
  training automation, ModelRegistry, configId activation, promotion
  automation, model lifecycle automation, emulator/real-device orchestration,
  release thresholds, weights/ReleasePolicy, cache implementation,
  specialist routing, any Runtime/semantic/authority change

RuntimeDelta:   NONE
SemanticDelta:  NONE
AuthorityDelta: NONE

NextTask:
  IMPLEMENT_PERCEPTION_PLATFORM_PHASE_4_FIRST_EVALUATION_BASELINE

NO_AUTOMATIC_TRAINING
NO_AUTOMATIC_PROMOTION
NO_AUTOMATIC_PHASE_4_EXPANSION
```

STOP.
