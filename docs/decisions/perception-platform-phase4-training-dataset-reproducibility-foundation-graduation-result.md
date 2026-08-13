# Perception Platform Phase 4 — Training/Dataset Reproducibility Foundation Graduation Result

> Date: 2026-08-13
> Role: Project Leader (Opus) / Foundation Graduation with Bounded Test-Infra Closure
> Result: `PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_GRADUATION_RESULT`
> Decision: **GRADUATED_WITH_RECORDED_DEFERRALS**

---

## 0. Decision

```text
Foundation:
  GRADUATED

Decision:
  GRADUATED_WITH_RECORDED_DEFERRALS

Recorded deferrals (next-phase items, NOT foundation defects):
  1. ReleasePolicy — DEFERRED (architecture purchased, implementation deferred)
  2. CanonicalPerceptionConfigId — NOT_IMPLEMENTED (configHash remains
     LEGACY_PARTIAL_CONFIG_IDENTITY)
  3. DeploymentIdentity — INCOMPLETE (next gate completes it)
  4. Holdout — NOT_ESTABLISHED
  5. Numeric thresholds — NOT_FROZEN
  6. ModelVersion governance — not activated

Test infrastructure gap (G18):
  CLOSED in this review — see §18.
```

The foundation graduates as the **canonical reproducible training/data
lineage foundation**. Process semantics graduated; mini-model quality is
irrelevant to this decision.

---

## 1. Training lineage (G1) — FROZEN

```text
Asset → Annotation → DatasetVersion → TrainingConfig → TrainingRun
→ Checkpoint → ModelArtifact → Candidate → EXISTING EvaluationRun

No future training implementation may bypass this lineage.
Proven: mini real run closed the full chain (7 nodes + 6 edges,
content-addressed end to end).
```

## 2. Stage / label space (G2) — FROZEN

```text
Pipeline Stage != Label Vocabulary (orthogonal identity).
EvaluationTargetStage: RAW_DETECTION | OCR | FUSED_EVIDENCE |
FINAL_PERCEPTION_EVIDENCE.
Scoring requires compatible stage AND label space; otherwise
STAGE_MISMATCH / LABEL_SPACE_MISMATCH → NOT_SCORABLE — never model failure.
Historical yoloExpectedCounts: DIAGNOSTIC_ONLY / NOT_RELEASE_ELIGIBLE.
Historical manifest bytes NOT rewritten (verified — untouched).
```

## 3. Raw / normalized / fused views (G3) — FROZEN

```text
raw model prediction    → raw model vocabulary (Detection.raw_label)
normalized detection    → canonical detection vocabulary
fused evidence          → fused/output vocabulary
No reverse reconstruction from normalized label to raw class (ST-08 proven —
the alias mapping is lossy and no reverse path exists).
```

## 4. Production schema safety (G4) — VERIFIED

```text
capture_stage_views default = false (signature-verified).
Detection.to_json unchanged — evidence schema carries no rawLabel key
(verified).
Production endpoints unaffected; evaluation obtains richer stage evidence
via the optional return channel only.
```

## 5–9. Dataset / annotation / leakage / config / reproducibility — FROZEN

```text
DatasetBoundary:       DatasetVersion = immutable content-addressed
                       membership manifest; training folders are DERIVED
                       execution views; identity never from directory/
                       timestamp/display name (TR-01..04, TR-25 proven).
AnnotationBoundary:    Annotation != Prediction; Annotation != Evaluation
                       GroundTruth; versioned, review-owned; correction →
                       new identity; historical TrainingRun references the
                       exact annotation version used (TR-05, TR-19 proven).
LeakagePolicy:         FROZEN_WITH_L3_DEFERRED — L-1 exact content, L-2
                       capture-group where truthful; protected evaluation
                       assets rejected from training (TR-06/07 proven).
TrainingConfigBoundary: TrainingConfig != PerceptionConfig;
                       trainingConfigId = SHA-256(canonical); UNRESOLVED
                       explicit (TR-08 proven).
ReproducibilityLevel:  REPRODUCIBLE_PROVENANCE — not bitwise (frozen).
```

## 10. Training run (G10, G11) — VERIFIED

```text
States: CREATED | RUNNING | COMPLETED | FAILED | CANCELLED.
FailedRunPreservation: PASS — the first FAILED mini attempt
(trun:5cbabd09…, dataset-path error) is preserved alongside the later
COMPLETED run (trun:6f41b678…); nothing was overwritten or deleted.

Code provenance: d843557c87456841369cefc46473d40d42997544, dirty=true.
Interpreted correctly: truthfully attributable, NOT reproducible from a
clean commit alone. Dirty=true is first-class provenance — never silently
converted to clean HEAD.
```

## 11–15. Checkpoint / artifact / name / candidate — FROZEN

```text
CheckpointBoundary:    checkpointName = role only; checkpointId = content
                       identity; "best" = training-policy selection only —
                       never production best / PROMOTED / ACTIVE /
                       release-approved / modelVersion (TR-11..13 proven).
ModelArtifactBoundary: modelId = full SHA-256 exact bytes; rename-invariant,
                       byte-sensitive; MATERIALIZE terminology (promotion
                       reserved for release lifecycle).
ModelName:             test candidate = mini_synthetic_box — explicit
                       TEST-ONLY family identity, no semantic collision
                       with android_ui_detection_yolov8 (verified).
CandidateBoundary:     Training completion X→ ACTIVE; ModelArtifact →
                       explicit CANDIDATE; CANDIDATE_TEST_ONLY structurally
                       unable to become ACTIVE through the foundation
                       (no activation API exists — TR-14/15 proven).
```

## 16. Evaluation integration (G16) — FROZEN_SAME_WORKFLOW

```text
Training metrics have ZERO release authority (TR-16 proven).
Candidate quality measured by the SAME graduated Evaluation workflow
(candidate_eval reuses EvaluationRun/Prediction/Matcher/Metrics/Scorecard —
EF-T06 proven: no second metric implementation exists).
```

## 17. Current ACTIVE legacy model (G17) — FROZEN

```text
android_ui_detection_yolov8 /
3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782
provenance = LEGACY_PROVENANCE_PARTIAL.
No fabricated DatasetVersion / TrainingConfig / TrainingRun / Checkpoint
facts. Grandfathering remains truthful.
```

## 18. Test infrastructure gap (G18–G21) — CLOSED

```text
TestFixtureGap:        CLOSED

Chosen mechanism: B. DETERMINISTIC_FIXTURE_GENERATOR
  • Repository-owned fixture source:
    tests/UniClaw.Runtime.Tests/Vision/vh_test_server.py
  • csproj: <Content Include="Vision/vh_test_server.py"
    CopyToOutputDirectory="PreserveNewest" />
  • [ModuleInitializer] ProvisionVisionHostFixture() in
    VisionHostBehavioralProofs.cs: copies repo-owned source to
    /tmp/vh_test_server.py before any test runs (idempotent byte-compare).

Clean-state proof (this review):
  • /tmp/vh_test_server.py deleted
  • Vision behavioral tests from clean state: 11/11 PASS — no manual repair
  • Full regression: 857/857 PASS

Fixture authority (G19): the fixture is TEST INFRASTRUCTURE ONLY — no
production launch path changed, no alternate Vision implementation, no
fallback provider. ProductionLaunchPaths remain unchanged (verified).
Fixture behavior (G20): all five modes exercised by existing tests
(normal / malformed / unsupported / slow / not-ready) preserved from the
test contracts, not memory.
```

## 19. Dependency boundary (G22) — FROZEN

```text
Production inference X→ training tooling (TR-21 proven)
VisionServiceHost   X→ training authority
Runtime             X→ dataset/training governance (grep-verified)
Training            X→ ACTIVE mutation (TR-14 proven)
Evaluation remains the sole quality measurement workflow.
```

## 20. File-based governance (G23) — CONFIRMED

Current scale does not justify database / MLFlow / ModelRegistry service /
training service / distributed scheduler. File-based immutable manifests
remain sufficient (proven at scale 6-image dataset through full lineage).

## 21. Next phase order (G24–G29) — ADOPTED

```text
ReleasePolicy remains DEFERRED. Reasons: small corpus, PARTIAL evidence
sufficiency, no holdout, thresholds NOT_FROZEN, no canonical configId,
incomplete deployment identity.

Next gate:
  PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_DEPLOYMENT_IDENTITY_CONFIG_AND_MODEL_GOVERNANCE_GATE

Next gate intent:
  • PerceptionConfigManifest + configId = SHA-256(canonical effective
    perception configuration) — audit ALL evidence-affecting settings
    (YOLO imgsz, confidence, NMS, aliases, OCR config, preprocessing,
    fusion constants, label mapping, env overrides); exclude Host
    operational settings.
  • Minimum ModelManifest (modelName/modelId/format/provenance/
    label-space identity/legacy stance) — no premature modelVersion.
  • PerceptionDeploymentCandidate / PerceptionDeploymentIdentity
    (ServiceVersion + SchemaVersion + ModelId + ConfigId +
    PipelineRevision; OCR-artifact + profile membership decided by
    repository evidence).
  • Candidate vs ACTIVE identity comparison.
  Only after this identity is complete: EvaluationProfile → ReleasePolicy →
  Promotion → Activation → Rollback become operational.
```

---

## Aggregate freeze

```text
PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_GRADUATION_RESULT

Decision:                   GRADUATED_WITH_RECORDED_DEFERRALS
Foundation:                 GRADUATED
StageContract:              FROZEN
LabelSpaceContract:         FROZEN
RawPredictionViews:         FROZEN
AnnotationBoundary:         FROZEN
DatasetBoundary:            FROZEN
LeakagePolicy:              FROZEN_WITH_L3_DEFERRED
TrainingConfigBoundary:     FROZEN
TrainingRunBoundary:        FROZEN
CheckpointBoundary:         FROZEN
ModelArtifactBoundary:      FROZEN
CandidateBoundary:          FROZEN
EvaluationIntegration:      FROZEN_SAME_WORKFLOW
ReproducibilityLevel:       REPRODUCIBLE_PROVENANCE
CurrentActiveLegacyProvenance: LEGACY_PROVENANCE_PARTIAL
MiniRealTraining:           PROVEN (real execution, 48.2s)
FailedRunPreservation:      PASS
TestFixtureGap:             CLOSED
VisionHostFixtureProvisioning: repo-owned + ModuleInitializer deterministic
                             provisioning (Option B)
PostFixtureHostTests:       11/11 PASS from clean state
TrainingTests:              33/33 PASS
EvaluationTests:            69/69 PASS
PythonPerceptionTests:      15/15 PASS
FullRuntimeRegression:      857/857 PASS
ArchitectureGuards:         PASS
RuntimeDelta:               NONE
SemanticDelta:              NONE
AuthorityDelta:             NONE
ReleasePolicy:              DEFERRED
CanonicalPerceptionConfigId: NOT_IMPLEMENTED
DeploymentIdentity:         INCOMPLETE
```

## Next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_DEPLOYMENT_IDENTITY_CONFIG_AND_MODEL_GOVERNANCE_GATE

NO_AUTOMATIC_RELEASE_POLICY
NO_AUTOMATIC_PROMOTION
NO_AUTOMATIC_DEPLOYMENT
```

STOP.
