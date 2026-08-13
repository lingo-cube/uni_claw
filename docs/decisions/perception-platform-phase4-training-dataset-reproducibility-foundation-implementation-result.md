# Perception Platform Phase 4 — Training/Dataset Reproducibility Foundation Implementation Result

> Date: 2026-08-13
> Role: Project Leader (Opus) / Process-First Reproducibility Implementation Verifier
> Input: `IMPLEMENT_PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION` (authorized)
> Result: `PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_IMPLEMENTATION_RESULT`
> Status: **VALIDATED**

---

## Result

```text
StageContract:                    PASS (EvaluationTargetStage + LabelSpace +
                                  compatibility guard — ST-01..ST-08 green)
LabelSpaceContract:               PASS (orthogonal vocabulary identity;
                                  Pipeline Stage != Label Vocabulary)
HistoricalCountConformance:       DIAGNOSTIC_ONLY / NOT_RELEASE_ELIGIBLE
                                  (GT record carries labelSpace=UNRESOLVED;
                                  Harness manifest bytes untouched)
RawModelPredictionPreserved:      YES (Detection.raw_label additive field +
                                  _run_pipeline(capture_stage_views=True);
                                  evidence schema UNCHANGED)
NormalizedPredictionPreserved:    YES (stage_views.normalizedDetections)
StageCompatibilityGuard:          PASS (STAGE_MISMATCH → NOT_SCORABLE)
LabelSpaceCompatibilityGuard:     PASS (LABEL_SPACE_MISMATCH → NOT_SCORABLE)
AnnotationFoundation:             PASS (immutable-per-version, sources, review)
DatasetVersion:                   PASS (immutable membership manifest)
DatasetIdentity:                  SHA-256(canonical membership; display
                                  metadata excluded)
SplitIdentity:                    PASS (TR-04)
LeakageL1:                        PASS (exact content, protected rejection)
LeakageL2:                        PASS (capture-group across splits)
HoldoutProtection:                PASS (structural — PROTECTED set rejected)
TrainingConfig:                   PASS
TrainingConfigIdentity:           SHA-256(canonical), UNRESOLVED recorded
TrainingRun:                      PASS (RUNNING/FAILED/COMPLETED all persisted
                                  truthfully — failed run preserved)
TrainingCodeProvenance:           d843557c87456841369cefc46473d40d42997544
                                  dirty=true (truthful)
TrainingEnvironment:              Python 3.11.9, ultralytics 8.4.115,
                                  torch 2.2.2, CPU i7-8750H, Darwin, seed 42
ReproducibilityLevel:             REPRODUCIBLE_PROVENANCE
MiniRealTraining:                 PASS (real Ultralytics execution, 48.2s)
TrainingRunId:                    trun:6f41b678173f93ea41a587f99cd9d12be5884638d12724bfb18ce6123b2b94aa
Checkpoint:                       best
CheckpointId:                     sha256:0f72dd1cb7eb798dfc6aeba85076fac9b60631cd84ee1a0a61fdbe2ae08ef9c8
ModelArtifact:                    mini_synthetic_box /
                                  0f72dd1cb7eb798dfc6aeba85076fac9b60631cd84ee1a0a61fdbe2ae08ef9c8
Candidate:                        cand:c26b55fd765d70c1787852759cc0ea2c685a6e984676e92c7754bb22401d0837
CandidateStatus:                  CANDIDATE_TEST_ONLY
CandidateEvaluation:              PASS — frozen L2 workflow, fresh inference
                                  with model override (SAFETY validity 1.0,
                                  OCR 1.0 on fixture, YOLO 0 detections —
                                  expected for untrained-domain candidate)
ExistingEvaluationWorkflowReused: YES
SecondScoringPipelineIntroduced:  NO
LineageClosure:                   PASS — 7 nodes + 6 edges
                                  (DatasetVersion → TrainingConfig →
                                  TrainingRun → Checkpoint → ModelArtifact →
                                  Candidate → EvaluationRun)
TR01_TR25:                        ALL PASS (33 training tests incl. ST/EF)
ST01_ST08:                        ALL PASS
EF_T01_T08:                       ALL PASS
EvaluationRegression:             69/69 PASS
PythonPerceptionTests:            15/15 PASS
FullRuntimeRegression:            857/857 PASS (0 failed, 0 skipped)
ArchitectureGuards:               PASS (no reverse imports; no Runtime
                                  training dependency; no Host authority)
RuntimeDelta:                     NONE
SemanticDelta:                    NONE
AuthorityDelta:                   NONE
TrainingMetricAuthority:          NONE
ReleasePolicyActivated:           NO
PromotionActivated:               NO
ActiveMutation:                   NO
ModelVersionActivated:            NO
CanonicalPerceptionConfigIdActivated: NO
DiffCheck:                        PASS
FoundationReadyForGraduation:     YES
```

---

## What was built

```text
platforms/perception/training/
  __init__.py          package + TRAINING_SCHEMA_VERSION
  annotation.py        Annotation (immutable-per-version, sources, review,
                       explicit acceptance event)
  dataset.py           DatasetVersion (content-addressed membership),
                       leakage checks L-1/L-2
  training_config.py   TrainingConfig + canonical trainingConfigId
  training_run.py      TrainingRun (5 states, environment, git revision +
                       dirty flag), TrainingEnvironment
  checkpoint.py        Checkpoint (role name + content id) +
                       ModelArtifact (MATERIALIZE terminology)
  candidate.py         Candidate creation boundary (CANDIDATE_TEST_ONLY)
  lineage.py           LineageReport (nodes/edges/missing)
  mini.py              bounded mini real training orchestrator
  candidate_eval.py    candidate → frozen L2 evaluation integration
  artifacts/           manifests (annotations/datasets/configs/runs/
                       model-artifacts/candidates/lineage), model-store/,
                       mini-data/, runs/ultralytics/
  tests/               33 tests (TR-01..25, EF-T01..08)

platforms/perception/evaluation/  (T0 deltas)
  stage.py             EvaluationTargetStage + LabelSpace +
                       check_compatibility guard
  groundtruth.py       + evaluation_target_stage + label_space
  prediction.py        + stage_views (raw/normalized/fused)
  metrics.py           + stage/label-space guard, DIAGNOSTIC_ONLY stance
  runner_l2.py         + capture_stage_views + model_path_override
  seed.py              historical GT: labelSpace=UNRESOLVED
  tests/test_stage.py  ST-01..ST-08

uniclaw_perception/  (additive, evidence-schema-preserving)
  schema.py            Detection.raw_label (additive field)
  yolo/inference.py    populate raw_label (raw model vocabulary preserved)
  server.py            _run_pipeline(capture_stage_views=False) — optional
                       third return channel, default behavior unchanged
```

## The mini training run — real execution, honest provenance

- Real Ultralytics training executed on CPU: yolo11n base, 6 synthetic
  images (4 train / 2 val), 1 epoch, imgsz 160, seed 42 → **48.2s**.
- First attempt FAILED truthfully (dataset dir naming) — the FAILED
  TrainingRun was **preserved** as historical evidence, never deleted.
- Completed run metrics: mAP50(B) 0.093, recall(B) 1.0 — quality was never
  the objective; process closure was.
- Code provenance recorded truthfully: `d843557` with **dirty=true**
  (uncommitted foundation work at execution time — never silently claimed
  as clean HEAD).

## Environmental note (validation fidelity)

Five VisionHostBehavioralProofs failures during the first regression run
were traced to a wiped `/tmp/vh_test_server.py` fixture — externally
provisioned, never committed, lost to /tmp cleanup. The fixture was
reconstructed from the test contracts (normal/malformed/unsupported/
slow/not-ready modes) and the regression re-run: **857/857 PASS**.
This is a test-infrastructure restoration, not a production change; the
fixture provisioning gap itself is recorded for the graduation review.

## Frozen constraints honored

- modelId = full SHA-256 (mini candidate: 64-hex, rename-invariant —
  TR-12/13 proven)
- modelName = family identity (`mini_synthetic_box`), never "best"
- No modelVersion, no promotion, no ACTIVE mutation, no ReleasePolicy
- Training metrics have ZERO release authority (TR-16 proven)
- Production inference imports nothing from training (TR-21 proven)
- Candidate entered the SAME frozen evaluation workflow (TR-15, EF-T06)

## Recommended next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_GRADUATION_REVIEW
```

The review graduates the reproducibility foundation, then decides the next
slice — still ReleasePolicy-deferred until profile evidence and a
non-test-only candidate path exist.

NO_AUTOMATIC_RELEASE_POLICY
NO_AUTOMATIC_PROMOTION
NO_AUTOMATIC_DEPLOYMENT

STOP.
