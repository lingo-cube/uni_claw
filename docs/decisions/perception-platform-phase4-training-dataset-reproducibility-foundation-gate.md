# Perception Platform Phase 4 — Training/Dataset Reproducibility Foundation Gate

> Date: 2026-08-13
> Role: Project Leader (Opus) / Architecture Gate
> Result: `PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_GATE_RESULT`
> Decision: **PURCHASE_WITH_CONSTRAINTS**
> Implementation: **NOT AUTHORIZED** (gate only; separate task authorizes implementation)

---

## 0. Stage semantics closure (T0) — including the STAGE_MISMATCH disposition

```text
StageMismatchDisposition:
  CLOSED_BY_STAGE_CONTRACT (T54)
  • Historical yoloExpectedCounts expectation: NOT rewritten.
  • Current count-conformance result: DIAGNOSTIC_ONLY / NOT_RELEASE_ELIGIBLE
    (already classified at graduation).
  • The mismatch becomes structurally impossible going forward:
    every GroundTruth and every Prediction declares its stage;
    cross-stage scoring is rejected (NOT_SCORABLE), never silently compared.

EvaluationTargetStage (purchased enum, shared vocabulary):
  RAW_DETECTION | OCR | FUSED_EVIDENCE | FINAL_PERCEPTION_EVIDENCE

GroundTruth and Annotation are NOT the same artifact (frozen):
  • Annotation = label truth for training (typically RAW_DETECTION).
  • GroundTruth = label truth for evaluation (stage per evaluation target).
  • Both carry a targetStage field with the SAME enum vocabulary.
  • A canonical annotation vocabulary CAN truthfully describe raw detection
    labels; it does NOT collapse into fused semantic GroundTruth.

RawPredictionPreservation (purchased):
  Prediction artifact gains the raw-stage outputs needed for RAW_DETECTION
  scoring: evidence["yolo"] raw label list (post-normalization, pre-fusion).
  Already available in pipeline output; just not persisted in the first
  baseline. Persist it; no production change.
```

## 1. Dataset model (T1–T4)

```text
DatasetVersion = immutable membership manifest + annotation references.
NOT a mutable folder. NOT "whatever is in this directory".

Membership split classes: TRAIN | VALIDATION | TEST | CALIBRATION.
HOLDOUT is NOT created from current data (Holdout remains NOT_ESTABLISHED).

DatasetIdentity:
  datasetVersionId = SHA-256(canonical membership manifest)
  canonical content: schema version, ordered asset ids, per-asset split
  assignment, per-asset annotation identity refs, capture grouping, metadata.
  Directory path / timestamp / display name are NOT identity.

SplitIdentity:
  split assignment is part of canonical content — same assets with a
  different split → different DatasetVersion. No silent masquerade.

TrainingAssetBoundary:
  Training role = membership reference to existing AssetId (EvaluationAsset /
  Reality asset identity). NO byte duplication (T3).
  TrainingDatasetMembership != EvaluationSuiteMembership (frozen).
  Same AssetId may participate in both only by explicit independent
  membership records. No "same folder means same role" inference.

CaptureGroupId (T12, purchased):
  Optional grouping field derived from CaptureSession/Scenario identity where
  repository metadata permits. Frame N in TRAIN + frame N+1 in VALIDATION
  from the same capture group → leakage warning/block by policy. Where no
  grouping metadata exists, the field is absent — never inferred.
```

## 2. Leakage policy (T5, T31, T32)

```text
LeakagePolicy (detection levels, from the start):
  L-1 EXACT_CONTENT: same AssetId in protected-eval and training → BLOCK.
  L-2 SAME_CAPTURE: same captureGroupId across split boundary → BLOCK.
  L-3 NEAR_DUPLICATE (perceptual): DEFERRED until L-1/L-2 prove insufficient.

HoldoutProtection:
  Future holdout assets carry PROTECTED_EVALUATION_ONLY.
  The Dataset builder MUST REJECT protected assets for training membership.
  Holdout leakage = blocking integrity failure (future; holdout not established).

EvaluationLeakageQualification (T31):
  If a Regression/Golden asset is later admitted to training membership,
  a leakage report records the asset as TRAINING_SHARED — its future
  evaluation score may not be presented as untouched generalization
  evidence without qualification.
```

## 3. Annotation model (T6–T9)

```text
Annotation = versioned, immutable-per-version artifact.

AnnotationIdentity:
  annotationId = SHA-256(canonical annotation record)
  record: assetId, annotationSchemaVersion, targetStage,
          annotationSource, reviewStatus, labelPayload, provenance,
          predecessorAnnotationId (for corrections).
  Correcting a label → NEW annotation identity; historical version
  referenced by existing TrainingRuns remains intact (TR-19, T46).

AnnotationSource (minimal truthful taxonomy):
  HUMAN_CREATED | HUMAN_CORRECTED | IMPORTED | MODEL_ASSISTED
  MODEL_ASSISTED = human accepted/reviewed a model suggestion.
  MODEL_PREDICTION != GROUND_TRUTH (frozen): a prediction exported into a
  label file is never canonical truth without a human acceptance event.

AnnotationReview (minimal, no theater):
  DRAFT → REVIEWED → { ACCEPTED | REJECTED }
  Question answered: "Was this annotation accepted as training truth?"

AnnotationTargetStage (T9, repository-verified):
  YOLO object-detection training annotation targets RAW_DETECTION.
  The annotation vocabulary = the model's class-index vocabulary
  (Deki-Yolo raw class names), NOT fused candidate types.
  Never label raw detection annotation as FINAL_PERCEPTION_EVIDENCE.

AnnotationOwnership:
  Annotation truth owner = annotation process (human review event).
  TrainingRun references annotation identities; it never owns truth.
```

## 4. Vocabulary audit (T10) — repository truth

```text
Three distinct vocabularies confirmed (NOT one shared vocabulary):

1. RAW MODEL VOCABULARY (training annotation space)
   = Deki-Yolo class names as model.names reports (~21 classes + aliases;
   23 raw keys in YOLO_LABEL_ALIASES).
   Authoritative source for class ids: the model artifact itself.
2. CANONICAL PERCEPTION LABELS (runtime raw detection space)
   = 11 labels via YOLO_LABEL_ALIASES
   (button checkbox icon image input list_item popup switch tab text_block toolbar).
3. FUSED/OUTPUT VOCABULARY (fused evidence space)
   = 13 interactive labels + text_block promotion + menu_item reclassifications
   (fusion DEFAULT_INTERACTIVE_LABELS).
   Plus label-mapping.json: 14 canonical → Runtime-type mappings.

Explicit mapping relationships are recorded per stage:
  RAW_DETECTION annotation → raw model class indices.
  Runtime normalization → canonical labels.
  Fusion → candidate types → Runtime types via label-mapping.json.
No giant semantic vocabulary collapsed across stages.
```

## 5. Training config (T13, T14, T47)

```text
TrainingConfig = immutable record of all output-affecting training inputs:
  baseModelArtifactId (initialization), epochs, batchSize, imgsz, optimizer,
  learningRate, scheduler, augmentation, seed, classVocabulary,
  framework (ultralytics) + version-affecting parameters.

TrainingConfigIdentity:
  trainingConfigId = SHA-256(canonical effective training configuration)
  deterministic sorted-key serialization; timestamps/machine details excluded.
  Any materially training-affecting change → new id (TR-08, T47).
  Unresolved parameters are recorded as UNRESOLVED — no full-reproducibility
  claim is made for what is not captured.

TrainingConfig != PerceptionConfig (frozen distinction — different identities,
different lifecycles).
```

## 6. TrainingRun (T15–T18, T50)

```text
TrainingRun = immutable record:
  trainingRunId, datasetVersionId, trainingConfigId,
  trainingCodeRevision (git commit; dirty-state recorded truthfully as
  codeRevision + dirty=true — never silently record HEAD),
  baseModelArtifactId, environmentProfile, startedAt (history metadata),
  terminalOutcome, producedCheckpoints[], trainingMetrics (diagnostic).

TrainingEnvironment (T15): pythonVersion, ultralyticsVersion, torchVersion,
runtimeVersion (CPU/CUDA if applicable), deviceType, osName, seed.
No machine-identity over-capture.

TrainingRunStates (minimal): CREATED | RUNNING | COMPLETED | FAILED | CANCELLED.
Failed runs remain historical evidence — never deleted (T18).

Training cost facts (T50): duration, hardware, peak memory if cheaply
available — operational facts, NOT inference Performance Evaluation.

TrainingMetricAuthority: NONE (T27 frozen).
Training metrics (loss, val-split mAP/precision/recall) are diagnostic for
training. They never alone authorize promotion.
```

## 7. Checkpoint / ModelArtifact / identity (T19–T23, T44)

```text
CheckpointIdentity:
  checkpointName = training role / human label ("best", "last", "epoch_50").
  Checkpoint bytes receive their own content identity
  (checkpointId = SHA-256(bytes)).
  "best" is NOT artifact identity. Filename is NOT identity (TR-11, TR-12).

BestCheckpointSemantics:
  "best" = Ultralytics training policy selection (best validation metric
  during the run). The selection metric/provenance is recorded where
  available. best.pt is NEVER interpreted as: production best / release
  approved / ACTIVE / PROMOTED.

ModelArtifact:
  A selected checkpoint promoted into a ModelArtifact.
  modelId = full SHA-256 of exact artifact bytes — FROZEN (no change).
  Renaming best.pt → candidate.pt does NOT change modelId (TR-12/T44).
  Byte change → different modelId (TR-13).

ModelName: FROZEN_FAMILY_IDENTITY (android_ui_detection_yolov8).
  New TrainingRuns normally produce new artifacts within the same family
  unless a new family is explicitly created. Never derived from filename/
  checkpoint role/hash.

ModelVersion: DEFERRED.
  Governance/release metadata — never TrainingRun checkpoint identity.
  Assignment event belongs to the release lifecycle (promotion), which does
  not exist yet. No arbitrary SemVer now.
```

## 8. Candidate creation (T24, T25)

```text
CandidateCreation:
  Checkpoint → ModelArtifact → CANDIDATE is an EXPLICIT boundary.
  Requirements: valid ModelArtifact identity + TrainingRun provenance +
  DatasetVersion + TrainingConfig provenance.
  NO evaluation result required to become CANDIDATE — evaluation happens next.
  Training completion NEVER mutates ACTIVE (TR-14).

CandidateIdentity:
  ModelArtifact != PerceptionDeploymentCandidate (frozen distinction).
  The same ModelArtifact may later be evaluated with different PerceptionConfig.
  Deployment candidate stays: {serviceVersion, schemaVersion, modelId,
  configId(future), profileId(future)}.

CANDIDATE_TEST_ONLY marker exists (T42): a test-only candidate may prove the
workflow and must never become ACTIVE.
```

## 9. Evaluation landing (T26, T28, T54)

```text
EvaluationIntegration: SAME_FROZEN_WORKFLOW.
  ModelArtifact/Candidate → existing DeploymentSnapshot representation →
  existing EvaluationSuite → EvaluationRun → Prediction → Matcher/Metrics →
  Scorecard.
  NO training_evaluator.py, NO special_training_score, NO best_model_score
  as competing authority (T26, EF-T06).

Terminology freeze (T28):
  training validation split  = development-time (Ultralytics val split)
  EvaluationSuite            = independent platform evaluation evidence
  future Holdout             = protected release/generalization evidence
  These three are never all called "validation".

StageContract (T54, closes STAGE_MISMATCH):
  RAW_DETECTION GT scores raw YOLO predictions only.
  FUSED/FINAL GT scores fused/final predictions only.
  Mixed-stage scoring is rejected as NOT_SCORABLE (TR-17, TR-18).
```

## 10. Current ACTIVE backfill & reproducibility (T39, T40)

```text
CurrentActiveLegacyProvenance:
  LEGACY_PROVENANCE_PARTIAL.
  Current ACTIVE model (3f39b0d6…): modelId + modelName truthfully known.
  Historical DatasetVersion / TrainingConfig / TrainingRun facts: UNKNOWN —
  NOT fabricated. Model remains valid as a grandfathered pre-governance
  deployment; it does not receive retrofitted provenance.

ReproducibilityLevel:
  REPRODUCIBLE_PROVENANCE (target): exact inputs/config/code/environment/
  artifacts traceable. NOT bitwise reproducibility — not promised unless
  verified (T39).
```

## 11. Storage & dependency direction (T33–T37, T51–T53)

```text
StorageOwnership (file-based first):
  platforms/perception/datasets/   — DatasetVersion manifests + annotations
  platforms/perception/training/   — TrainingConfig + TrainingRun manifests,
                                     training scripts
  platforms/perception/models/     — existing canonical model storage
                                     (android_ui_detection_yolov8/best.pt =
                                     canonical artifact with checkpoint-name
                                     residue; NOT moved in this slice — T53)
  Evaluation foundation stays at platforms/perception/evaluation/.
No database, no registry service, no MLFlow.

DatasetMaterialization (T34):
  images/labels/train/val folders = DERIVED execution view of the manifest.
  Deleting/regenerating materialized folders loses nothing (TR-25).

Dependency direction (T35, enforced):
  training tooling → model artifacts
  production inference → model artifacts
  production inference X→ training tooling  (TR-21)

RuntimeIsolation (T36): Runtime → Dataset/TrainingRun/ModelManifest/Annotation
all FORBIDDEN. Runtime consumes normalized perception evidence only.
HostIsolation (T37): VisionServiceHost does not train/select/evaluate/promote.
It starts the already-selected deployment only.

Security (T51): annotations/configs are declarative manifests — no executable
formats, no arbitrary code execution from dataset artifacts.
```

## 12. Failure → dataset boundary (T29, T30, T49)

```text
Failure → DatasetCandidate is NEVER automatic.
Asset admission and Dataset membership are separate decisions.
A RegressionAsset does NOT automatically become training data — explicit
TRAINING_MEMBERSHIP required (TR-24); sometimes independent evaluation is
more valuable than training reuse.
```

## 13. Falsifiers

```text
TR-01  Same membership + annotation ids + splits → same DatasetVersion id.
TR-02  Adding one asset → new DatasetVersion.
TR-03  Changing an annotation → new DatasetVersion.
TR-04  Changing train/validation split → new DatasetVersion.
TR-05  Model prediction cannot become accepted annotation automatically.
TR-06  Holdout-protected asset cannot enter training dataset.
TR-07  Exact content overlap protected-eval ↔ training is detected.
TR-08  TrainingConfig materially changed → new TrainingConfigId.
TR-09  TrainingRun records exact DatasetVersion + TrainingConfig + code revision.
TR-10  Failed TrainingRun cannot produce fabricated ModelArtifact.
TR-11  Checkpoint name "best" does not become modelName.
TR-12  Renaming checkpoint does not change modelId.
TR-13  Changing checkpoint bytes changes modelId.
TR-14  Training completion cannot mutate ACTIVE.
TR-15  Candidate cannot bypass existing Evaluation workflow.
TR-16  Training metric cannot act as release authority.
TR-17  RAW_DETECTION GT cannot score FUSED_EVIDENCE predictions.
TR-18  Stage mismatch → NOT_SCORABLE / explicit mismatch, never failure.
TR-19  Historical annotations used by old TrainingRuns remain immutable.
TR-20  Current ACTIVE legacy model represents partial provenance without
       fabricating missing facts.
TR-21  Production inference does not import training tooling.
TR-22  VisionServiceHost does not acquire training authority.
TR-23  Runtime has zero training/data governance dependency.
TR-24  RegressionAsset does not automatically become TrainingDataset member.
TR-25  Dataset materialization can be deleted/regenerated without changing
       canonical DatasetVersion.

EfficiencyFalsifiers:
EF-T01 Same image bytes shared across Evaluation and Training stored once.
EF-T02 DatasetVersion references membership; role change duplicates no bytes.
EF-T03 Mini training run proves workflow without full production cost.
EF-T04 Training infrastructure requires no emulator/real device.
EF-T05 Training artifact immediately enters existing L2 Evaluation.
EF-T06 No second metric/scorecard implementation introduced.
EF-T07 Dataset growth = manifest change, not framework redesign.
EF-T08 Lineage scales to many TrainingRuns without a mutable "latest"
      directory becoming truth.
```

## 14. Implementation slices (ordered)

```text
P4-T0  EvaluationTargetStage + Prediction raw-stage preservation +
       stage compatibility guard (purchased delta; closes STAGE_MISMATCH)
P4-T1  Annotation manifest + provenance + identity
P4-T2  DatasetVersion + split identity + leakage checks (L-1/L-2)
P4-T3  TrainingConfig + canonical TrainingConfigId
P4-T4  TrainingRun manifest + lifecycle + environment/code provenance
P4-T5  Checkpoint + ModelArtifact identity
P4-T6  Candidate creation boundary
P4-T7  Mini real TrainingRun (tiny synthetic-derived dataset, small epochs,
       CPU, if ultralytics training is executable locally; otherwise
       infrastructure blocker reported — NO fake training, T43)
P4-T8  Candidate → existing L2 Evaluation integration (fresh inference)
P4-T9  Lineage report / closure tests (TR-01…TR-25, EF-T01…EF-T08)
```

## 15. Architecture questions — explicit answers

1. **Canonical training data truth source** = DatasetVersion manifest
   (immutable, content-addressed membership + annotation refs). Materialized
   folders are derived views.
2. **EvaluationAsset vs training source Asset** = same content identity
   (AssetId); training role is a separate membership reference. Linked, not
   duplicated identities.
3. **Annotation truth owner** = annotation process with human review events;
   TrainingRun references, never owns.
4. **Annotation stage representation** = targetStage field (same enum as
   GroundTruth.evaluationTargetStage).
5. **DatasetVersion membership owner** = DatasetVersion manifest
   (content-addressed; new version per membership change).
6. **DatasetVersion immutability** = identity from canonical content;
   mutation impossible by construction — new content → new id, old manifest
   file never overwritten.
7. **TrainingConfig reproducibility** = canonical TrainingConfigId covering
   all training-affecting inputs; UNRESOLVED recorded honestly.
8. **TrainingRun attribution** = datasetVersionId + trainingConfigId +
   codeRevision (+dirty flag) + environmentProfile.
9. **Bitwise vs provenance reproducibility** = REPRODUCIBLE_PROVENANCE
   target; bitwise not promised.
10. **best.pt representation** = checkpointName "best" + checkpointId
    (content) + selection-metric provenance; never identity, never status.
11. **Checkpoint → ModelArtifact** = explicit promotion boundary with
    artifact bytes frozen under modelId (full SHA-256).
12. **ModelArtifact → CANDIDATE** = explicit candidate record requiring
    artifact identity + training provenance; evaluation follows.
13. **Candidate → Evaluation workflow** = existing DeploymentSnapshot +
    EvaluationSuite + EvaluationRun + fresh L2 Prediction — no new pipeline.
14. **Leakage detection** = L-1 exact content, L-2 capture group; holdout
    membership rejected at build time; leakage report qualifies shared assets.
15. **Current ACTIVE legacy model** = LEGACY_PROVENANCE_PARTIAL backfill;
    no fabricated facts.
16. **New objects with Runtime authority** = NONE. RuntimeDelta / SemanticDelta
    / AuthorityDelta = NONE.

## 16. Admission decision

```text
PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_GATE_RESULT

Decision:                    PURCHASE_WITH_CONSTRAINTS

  C1  Implementation of P4-T0 (EvaluationTargetStage delta) is REQUIRED
      first — it closes the purchased STAGE_MISMATCH gap.
  C2  Mini training run only if ultralytics training is executable locally;
      otherwise the infrastructure blocker is reported — produced
      checkpoints are never fabricated (T43).
  C3  CANDIDATE_TEST_ONLY candidates can never become ACTIVE.
  C4  No holdout fabrication, no modelVersion activation, no release policy,
      no promotion, no ACTIVE mutation in this foundation.

EvaluationFoundation:        GRADUATED_WITH_RECORDED_DEFERRALS
StageMismatchDisposition:    CLOSED_BY_STAGE_CONTRACT (T54)
EvaluationTargetStage:       PURCHASED (RAW_DETECTION | OCR | FUSED_EVIDENCE |
                             FINAL_PERCEPTION_EVIDENCE)
RawPredictionPreservation:   PURCHASED (persist evidence["yolo"] raw list)
TrainingAssetBoundary:       membership reference, no byte duplication
AnnotationModel:             versioned; targetStage + source + review
AnnotationOwnership:         annotation process (human review events)
AnnotationTargetStage:       RAW_DETECTION for YOLO training
DatasetVersion:              immutable membership manifest
DatasetIdentity:             SHA-256(canonical membership)
SplitIdentity:               part of canonical content
LeakagePolicy:               L-1 exact / L-2 capture-group; near-dup deferred
HoldoutProtection:           PROTECTED_EVALUATION_ONLY; builder rejects
TrainingConfig:              immutable; != PerceptionConfig
TrainingConfigIdentity:      SHA-256(canonical), UNRESOLVED recorded
TrainingRun:                 immutable; 5 states; failed runs preserved
TrainingEnvironment:         minimal provenance set
TrainingCodeRevision:        git commit + dirty flag
CheckpointIdentity:          checkpointName (role) + checkpointId (content)
BestCheckpointSemantics:     training-policy selection only
ModelArtifact:               checkpoint → artifact promotion boundary
ModelId:                     FROZEN_FULL_SHA256
ModelName:                   FROZEN_FAMILY_IDENTITY
ModelVersion:                DEFERRED
CandidateCreation:           explicit; no evaluation required yet
TrainingMetricAuthority:     NONE
EvaluationIntegration:       SAME_FROZEN_WORKFLOW
CurrentActiveLegacyProvenance: LEGACY_PROVENANCE_PARTIAL
ReproducibilityLevel:        REPRODUCIBLE_PROVENANCE
StorageOwnership:            file-based; datasets/ training/ models/
                             (production model NOT moved)

TR01_TR25:                   ALL PURCHASED
EfficiencyFalsifiers:        EF-T01..EF-T08

RecommendedImplementationSlices: P4-T0 → P4-T1 → P4-T2 → P4-T3 → P4-T4 →
                             P4-T5 → P4-T6 → P4-T7 → P4-T8 → P4-T9

ForbiddenScope:
  automatic retraining, hyperparameter search, Optuna, distributed training,
  GPU scheduler, training service, cloud training, ModelRegistry service,
  automatic modelVersion increment, automatic promotion, automatic
  deployment, ACTIVE mutation, ReleasePolicy, EvaluationProfile thresholds,
  canonical Perception configId, emulator evaluation, real-device
  evaluation, self-learning loop

RuntimeDelta:                NONE
SemanticDelta:               NONE
AuthorityDelta:              NONE

NextTask:
  IMPLEMENT_PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION

NO_AUTOMATIC_RELEASE_POLICY
NO_AUTOMATIC_PROMOTION
NO_AUTOMATIC_DEPLOYMENT
```

STOP.
