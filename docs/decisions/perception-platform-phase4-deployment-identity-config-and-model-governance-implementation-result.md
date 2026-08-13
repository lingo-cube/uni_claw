# Perception Platform Phase 4 — Deployment Identity, Config & Model Governance Implementation Result

> Date: 2026-08-13
> Role: Project Leader (Opus) / Identity Integrity Vertical Slice Implementation Verifier
> Input: `IMPLEMENT_PERCEPTION_PLATFORM_PHASE_4_DEPLOYMENT_IDENTITY_CONFIG_AND_MODEL_GOVERNANCE` (authorized)
> Result: `PERCEPTION_PLATFORM_PHASE_4_DEPLOYMENT_IDENTITY_CONFIG_AND_MODEL_GOVERNANCE_IMPLEMENTATION_RESULT`
> Status: **VALIDATED**

---

## Result

```text
IdentitySemanticReconciliation:  PASS (D0-1..D0-4 resolved in code)
ServiceVersionIdentity:          METADATA_ONLY (never behavior identity)
BehaviorIdentity:                {schemaVersion, modelId, configId,
                                 pipelineRevision}
DeploymentId:                    SHA-256(canonical behavior identity)
LabelMappingOwnership:           single owner — ConfigId (transitive);
                                 candidate ref is diagnostic only
EffectiveConfigInventory:        29 rows mechanized; 26 material settings
                                 each with EXACTLY ONE identity owner
ConfigManifest:                  PASS (uniclaw.perceptionConfig.v1)
ConfigCompleteness:              COMPLETE (current ACTIVE)
CurrentActiveConfigId:           config:edb7ad546d2b7f9c5b2b41affca70c13953e9efbbb5e2347c7418583778ac48f
LegacyConfigHashPreserved:       YES (compat identity; never configId)
PipelineRevision:                prev:da1f86cc808dcb497d21aad9bee337c5d05befd12d5d1a59054c3e56f7e15b87
PipelineSourceModules:           14 behavior-defining modules (no tests/
                                 training/evaluation/__pycache__)
ResolvedDependencyIdentity:      ultralytics 8.4.115, rapidocr 1.4.4,
                                 onnxruntime 1.23.2, torch 2.2.2,
                                 pillow 12.3.0, numpy 1.26.4 (importlib.metadata)
OcrIdentity:                     OCR_RUNTIME_VERSION_METADATA_ONLY
                                 (pip-bundled, pinned; no OCR registry)
ModelManifest:                   PASS
CurrentActiveModelManifest:      android_ui_detection_yolov8 /
                                 3f39b0d6…782 / ULTRALYTICS_PT / YOLOV8 /
                                 DEKI_YOLO_RAW_V1 / 21-class vocabulary /
                                 LEGACY_PROVENANCE_PARTIAL
TestCandidateModelManifest:      mini_synthetic_box / 0f72dd1c…c8 /
                                 YOLO11 / MINI_SYNTHETIC_BOX_V1 / ["box"] /
                                 TRAINING_LINEAGE_LINKED
TestCandidateArchitecture:       YOLO11 (yaml-grounded: C3k2/C2PSA/
                                 yolo11n.yaml — NOT YOLOv8)
TestCandidateLabelSpace:         MINI_SYNTHETIC_BOX_V1 (truthful, distinct)
ModelVersion:                    DEFERRED
CurrentActiveDeploymentId:       deploy:6b3d3081d6eb7544197c97514c13064f7fbaa10772e2ffadeefddd243e0e5f76
TestCandidateDeploymentId:       deploy:7bb95b3cd726fb95bebb3b63b72889f7adf5e2ded5a33e3839bdc53c29386d91
ActiveVsTestIdentityDiff:        MODEL_ONLY (modelChanged=true, behaviorChanged=true)
VersionEndpoint:                  PASS (/version: configId, configCompleteness,
                                  pipelineRevision, deploymentId — additive)
VersionReportsActualIdentity:     PASS (computed from loaded state; round-trip)
HostIdentityVerification:         PASS (expected-vs-observed, fail closed)
HostMismatchModel:                PASS
HostMismatchConfig:               PASS
HostMismatchPipeline:             PASS
HostMismatchSchema:               PASS
RestartIdentityReverification:    PASS (verification runs in every
                                  WaitForReadinessAsync — restart re-verifies)
EvaluationDeploymentBinding:      PASS (canonical DeploymentSnapshot fields)
ExecutionIdentityMismatchGuard:   PASS (EVALUATION_DEPLOYMENT_IDENTITY_MISMATCH
                                  → infrastructure failure, never quality run)
HistoricalEvaluationArtifactsUntouched: PASS (no rewrite API; historical
                                  snapshots stay LEGACY_PARTIAL)
DI01_DI20:                        ALL PASS (governance tests)
CFI01_CFI04:                      ALL PASS
IDR01_IDR07:                      ALL PASS
EXI01_EXI07:                      ALL PASS
TrainingRegression:               33/33 PASS
EvaluationRegression:             69/69 PASS
PythonPerceptionTests:            15/15 PASS
VisionHostTests:                  16/16 PASS (behavioral + identity, clean-state)
FullRuntimeRegression:            862/862 PASS (0 failed, 0 skipped)
ArchitectureGuards:               PASS
GoldenReplay:                     PASS (within full regression)
RuntimeDelta:                     NONE
SemanticDelta:                    NONE
AuthorityDelta:                   NONE
ReleasePolicyActivated:           NO
PromotionActivated:               NO
ActiveMutation:                   NO
ModelVersionActivated:            NO
DiffCheck:                        PASS
FoundationReadyForGraduation:     YES
```

---

## What was built

```text
platforms/perception/governance/
  __init__.py          package + frozen identity semantics
  inventory.py         29-row mechanized effective-config inventory +
                       single-ownership guard (26 material settings)
  config_manifest.py   PerceptionConfigManifest + canonical configId +
                       COMPLETE/PARTIAL/UNRESOLVED
  pipeline_revision.py content-addressed PipelineRevision — 14 behavior
                       modules + 6 ACTUAL resolved dependency versions
  model_manifest.py    ModelManifest — truthful format/architecture/
                       labelSpace separation; yaml-grounded architecture
                       detection (C2f→YOLOV8, C3k2/C2PSA→YOLO11)
  deployment.py        PerceptionDeploymentCandidate/Identity/Instance;
                       4 canonical identity axes only
  diff.py              mechanical identity diff + ChangeClassification
  build_active_identity.py  ACTIVE canonical identity + vertical proof
  artifacts/           config-manifests, pipeline-revisions,
                       model-manifests, deployments, current-active-identity.json
  tests/               37 falsifier tests (DI/CFI/IDR/EXI)

Modified:
  uniclaw_perception/health.py   /version additive actual-identity facts
  evaluation/deployment.py       canonical configId/deploymentId fields
  evaluation/runner_l2.py        execution truth guard (EXI-01..03)
  src/UniClaw.Vision.Host/       ExpectedDeploymentIdentity +
                                 VerifyIdentityOrThrow + VerifyIdentityAgainst
  tests/.../Vision/              vh_test_server.py identity-mismatch modes +
                                 VisionIdentityVerificationTests (5 tests)
```

## Proofs executed (real, not mocked)

**Round-trip proof** — real production server launched via uvicorn on a
fresh UDS; `/version` observed identity compared against the independently
composed ACTIVE identity:

```text
observed deploymentId == expected deploy:6b3d3081… — PASS
(modelId, configId, pipelineRevision all matched)
```

**Vertical proof** — ACTIVE (YOLOV8, 21-class Deki-Yolo) vs TEST candidate
(YOLO11-derived, 1-class mini): distinct deploymentIds, diff classified
MODEL_ONLY. No quality conclusion drawn.

**Host mismatch proofs** — four real fixture modes (wrong-model,
wrong-config, wrong-pipeline, unsupported-schema) each fail closed through
the Host verification predicate; the matching case passes. Executed from a
clean state with the repository-owned fixture (no manual repair).

## The seven graduation blockers — proven closed

1. Material evidence-affecting settings cannot change without identity
   change — 26/26 material settings have exactly one owner (inventory guard).
2. PipelineRevision reflects actual executed behavior — content-addressed
   source hashes + importlib.metadata dependency versions (IDR-06/07).
3. OCR behavior cannot change without identity change — backend/mode/
   textScore in ConfigId; package version in PipelineRevision (DI-19).
4. EvaluationRun cannot claim a different identity than it executed —
   EXI-01/02/03 guards reject mismatched model bytes / config / revision.
5. Host cannot become HEALTHY on identity mismatch — DI-16 tests, all four
   axes fail closed.
6. Current ACTIVE config reaches COMPLETE — all material inputs resolved.
7. ACTIVE identity reproducible from independent composition + /version
   observation — round-trip proof PASS.

## Recommended next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_DEPLOYMENT_IDENTITY_CONFIG_AND_MODEL_GOVERNANCE_GRADUATION_REVIEW

NO_AUTOMATIC_RELEASE_POLICY
NO_AUTOMATIC_PROMOTION
NO_AUTOMATIC_DEPLOYMENT
```

STOP.
