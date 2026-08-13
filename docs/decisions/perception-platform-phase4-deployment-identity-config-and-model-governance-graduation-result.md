# Perception Platform Phase 4 — Deployment Identity, Config & Model Governance Graduation Result

> Date: 2026-08-13
> Role: Project Leader (Opus) / Foundation Graduation & Runtime Identity Falsification
> Result: `PERCEPTION_PLATFORM_PHASE_4_DEPLOYMENT_IDENTITY_CONFIG_AND_MODEL_GOVERNANCE_GRADUATION_RESULT`
> Decision: **GRADUATED_WITH_RECORDED_DEFERRALS**

---

## 0. Decision

```text
Foundation:
  GRADUATED

Decision:
  GRADUATED_WITH_RECORDED_DEFERRALS

Recorded deferrals:
  ReleasePolicy            DEFERRED
  Promotion / Activation / Rollback   NOT_IMPLEMENTED
  L3 Emulator / L4 Real Device evaluation   NOT_IMPLEMENTED
  ModelVersion             DEFERRED
  Numeric release thresholds   NOT_FROZEN
  EvaluationProfile        architecture-purchased, implementation deferred
```

This review was adversarial: it attempted to falsify the relationship
between DECLARED IDENTITY and ACTUALLY LOADED/EXECUTED BEHAVIOR. It found
and closed **two real graduation blockers** before graduation — that is the
review working as designed.

---

## 1. The two blockers found by falsification (and closed)

### Blocker 1 — /version described DISK, not LOADED behavior (G9/G11)

Falsification result: `/version` re-hashed the model file and behavior
source files **per call**. Replacing the model file or editing a source
module after startup would have made the running process report an
identity for behavior it was NOT executing.

Closure (mechanical, no architecture delta — "identity snapshot at load",
the gate's acceptable design A):

```text
governance/runtime_snapshot.py
  → capture_snapshot() computed ONCE at lifespan (after model warmup):
      modelId          = SHA-256 of artifact bytes AT LOAD
      config manifest  = built from the resolved in-memory config, with
                         label-mapping content hash from the SAME bytes
                         loaded (never re-read)
      pipelineRevision = source/dependency/OCR-model identity AT STARTUP
      deploymentId     = derived from snapshot constituents
  → /version reports the frozen snapshot (canonical path)
```

### Blocker 2 — OCR model bytes were identity-invisible (G15/OCR-03)

Falsification result: 3 RapidOCR ONNX model files (16 MB total:
ch_PP-OCRv4_det/rec + cls) exist independently on disk inside the installed
package directory — replaceable WITHOUT a package version change. Evidence
could change while ConfigId and PipelineRevision stayed identical.

Closure (mechanical, existing axis — no new axis, no OCR registry):

```text
PipelineRevision now includes ocrModels/*.onnx content hashes
(resolved from the installed rapidocr package path).
Byte replacement → different PipelineRevision. Missing files → revision
incomplete (blocks COMPLETE identity claims).
```

## 2. Behavioral falsification proofs (real, not mocked)

```text
RuntimeSnapshotFalsifiers (real servers + real disk mutation):
  RSI-01 loaded model + disk replaced post-start → /version unchanged   PASS
  RSI-02 loaded config + source mutated post-start → /version unchanged  PASS
  RSI-03 loaded pipeline + source edited post-start → /version unchanged PASS
  RSI-04 restart after valid model replacement → new ModelId             PASS
  RSI-05 restart after effective config change → new ConfigId            PASS
  RSI-06 restart after behavior-source change → new PipelineRevision     PASS
  RSI-07 deploymentId independently re-derived from observed constituents PASS
         (no production-helper reuse — G22/G34 satisfied)
  RSI-08 ACTIVE expected/observed converge (fresh server, real model)    PASS

OcrFalsifiers:
  OCR-01 backend/mode/textScore change → ConfigId change                 PASS
  OCR-02 pinned OCR runtime version change → PipelineRevision change     PASS
  OCR-03 real ONNX files content-hashed into PipelineRevision            PASS
         (3 files covered, none MISSING; simulated byte change → new rev)
```

## 3. Freezes

```text
BehaviorIdentity:   FROZEN — {SchemaVersion, ModelId, ConfigId,
                    PipelineRevision}; deploymentId = SHA-256(canonical)
ReleaseUnit:        FROZEN_PERCEPTION_DEPLOYMENT_IDENTITY
                    (never ModelArtifact / best.pt / modelName / TrainingRun)
ConfigManifest:     FROZEN (uniclaw.perceptionConfig.v1; resolved effective
                    values; operational settings excluded)
ConfigCompleteness: COMPLETE (current ACTIVE — all material inputs resolved)
ConfigId:           FROZEN — SHA-256(canonical COMPLETE effective config)
LegacyConfigHash:   COMPATIBILITY_ONLY (never canonical)
ModelManifest:      FROZEN — immutable facts; format/architecture/
                    labelSpace/classVocabulary truthful per artifact
                    (production YOLOV8/DEKI_YOLO_RAW_V1 vs test YOLO11/
                    MINI_SYNTHETIC_BOX_V1 — no default leakage, G29/G30)
ModelId:            FROZEN_FULL_SHA256
ModelVersion:       DEFERRED
PipelineRevision:   FROZEN — behavior-source content hashes + actual
                    resolved dependency versions + OCR model file hashes
LoadedPipelineIdentity:  PASS (snapshot; RSI-03/06)
LoadedModelIdentity:     PASS (snapshot; RSI-01/04)
LoadedConfigIdentity:    PASS (snapshot; RSI-02/05)
OcrIdentity:        PASS — fully owned by existing axes (ConfigId +
                    PipelineRevision); OCR_ARTIFACT_ID pressure closed
ServiceVersion:     METADATA_ONLY (IDR-01: service-only change keeps
                    deploymentId)
DeploymentInstanceBoundary: FROZEN (PID/UDS/session/restarts ≠ identity)
VersionEndpointActuality:   PASS (snapshot facts; never echoes expected)
HostExpectedObservedVerification: FROZEN — mechanism authority only;
                    fail closed on model/config/pipeline/schema mismatch
CanonicalProductionHostPath: VERIFIED — VisionHostConfig
                    .ForCanonicalProduction REQUIRES ExpectedDeploymentIdentity
                    (canonical path cannot launch with verification disabled;
                    legacy paths construct directly and omit expectations
                    deliberately)
RestartIdentityReverification: PASS (every StartAsync re-verifies; RSI-04..06)
EvaluationDeploymentBinding: FROZEN — new runs reference exact identity
ExecutionTruthGuard: PASS — EXI-01/02/03 reject executed-vs-claimed
                    mismatch as infrastructure failure, never model failure
CurrentActiveDeploymentId: deploy:101f5ddccd2db3d179de5ed00205f45887442a3e74f443fcdda9f0beb88a71b8
CurrentActiveRoundTrip:  PASS (observed == expected, snapshot mechanism)
HistoricalEvaluationArtifacts: UNCHANGED (byte-identical; no backfill)
```

## 4. Validation

```text
GovernanceTests:    48/48 PASS (37 unit + 11 behavioral RSI/OCR)
TrainingTests:      33/33 PASS
EvaluationTests:    69/69 PASS
PerceptionTests:    15/15 PASS
VisionHostTests:    16/16 PASS (clean fixture state)
FullRuntimeRegression: 862/862 PASS (0 failed, 0 skipped)
ArchitectureGuards: PASS
DiffCheck:          PASS

DI01_DI20: ALL PASS   CFI01_CFI04: ALL PASS
IDR01_IDR07: ALL PASS  EXI01_EXI07: ALL PASS
RSI01_RSI08: ALL PASS  OCR01_OCR03: ALL PASS

RuntimeDelta:   NONE
SemanticDelta:  NONE
AuthorityDelta: NONE
ReleasePolicy:  DEFERRED
Promotion:      NOT_IMPLEMENTED
ActiveMutation: NO
GraduationBlockers: NONE
```

## 5. The nine graduation blockers — verdict

1. reported identity diverging from loaded behavior — **CLOSED** (snapshot)
2. model file mutation making /version lie — **CLOSED** (RSI-01/04)
3. source mutation making PipelineRevision describe disk — **CLOSED** (RSI-03/06)
4. config mutation making ConfigId describe unused config — **CLOSED** (RSI-02/05)
5. OCR behavior changing with all axes unchanged — **CLOSED** (OCR-03)
6. material behavior code outside identity ownership — **CLOSED** (14-module
   inventory + single-ownership guard, re-verified)
7. Config COMPLETE not actually complete — **CLOSED** (re-audited)
8. canonical production Host path bypassing verification — **CLOSED**
   (ForCanonicalProduction requires ExpectedIdentity)
9. EvaluationRun claiming an unexecuted identity — **CLOSED** (EXI-01..03)

## 6. Next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_CANDIDATE_VS_ACTIVE_COMPARISON_AND_EVALUATION_PROFILE_GATE

Sequence:
  Deployment Identity Foundation  ← GRADUATED (this review)
    ↓
  Candidate vs ACTIVE Comparison  ← NEXT
    ↓
  EvaluationProfile
    ↓
  ReleasePolicy
    ↓
  Promotion / Activation / Rollback
    ↓
  L3 Emulator → L4 Real Device → Failure/Regression/Dataset closure

NO_AUTOMATIC_RELEASE_POLICY
NO_AUTOMATIC_PROMOTION
NO_AUTOMATIC_DEPLOYMENT
```

STOP.
