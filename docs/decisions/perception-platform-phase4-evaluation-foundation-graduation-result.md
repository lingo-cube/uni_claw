# Perception Platform Phase 4 — Evaluation Foundation Graduation Result

> Date: 2026-08-13
> Role: Project Leader (Opus) / Foundation Graduation & Next Slice Resequencing
> Result: `PERCEPTION_PLATFORM_PHASE_4_EVALUATION_FOUNDATION_GRADUATION_RESULT`
> Decision: **GRADUATED_WITH_RECORDED_DEFERRALS**
> Implementation: **NOT AUTHORIZED IN THIS TASK**

---

## 0. Decision

```text
Foundation:
  GRADUATED

Decision:
  GRADUATED_WITH_RECORDED_DEFERRALS
  (process foundation sound; corpus/coverage deferrals recorded)

Recorded deferrals (do NOT invalidate process graduation):
  1. small corpus (3 suite assets; EvidenceSufficiency PARTIAL)
  2. Holdout not established (NONE)
  3. missing system families (OneUI/HyperOS/ColorOS UNASSESSED)
  4. missing switch-state ground truth (NOT_PRESENT)
  5. L3 emulator / L4 real device not implemented
  6. numeric thresholds NOT_FROZEN
  7. NEW: count-conformance stage semantics unresolved (G5 — see §5)
     → current count-conformance metric reclassified DIAGNOSTIC_ONLY,
       NOT_RELEASE_ELIGIBLE until GroundTruth stage identity is explicit
```

The foundation is a **process platform capability**, not a model-quality
certification. The small corpus and PARTIAL sufficiency are truthful coverage
states — success criteria of the first slice, not failures.

---

## 1. Canonical workflow (G1) — FROZEN

```text
Evidence → Asset Candidate / EvaluationAsset → Classification → GroundTruth
→ EvaluationSuite → EvaluationRun → Fresh Prediction → Matching → Metrics
→ Scorecard → Coverage → EvidenceSufficiency → Immutable Evaluation/Baseline Report

All future perception capability work (model, config, OCR, fusion,
specialist/generalist deployments) MUST reuse this workflow.
No parallel model-specific evaluation systems.
```

## 2. Asset model (G2) — FROZEN

```text
EvaluationAsset identity = content-addressed (sha256 over bytes).
Classification stays orthogonal (9 dimensions, UNKNOWN valid everywhere).
GroundTruth stays separate from Prediction (prediction never becomes truth).
Suite membership references Asset identity — never copies bytes.
Harness/Reality assets reused by reference.
Asset count growth (5 → 50 → 500 → 5000) must not alter the semantic model.
```

## 3. Evidence ladder (G3) — FROZEN

```text
L0 Simulation   = Runtime semantic/mechanism evidence
L1 Replay       = Runtime behavior evidence
L2 Recorded Image Inference = current Perception deployment accuracy evidence
L3 Emulator     = future integrated reality execution evidence
L4 Real Device  = future highest-cost reality evidence
No lower layer may masquerade as another layer's evidence (PF3/PF4 proven).
```

## 4. Small corpus truth (G4) — ACCEPTED AS TRUTH

`EvidenceSufficiency=PARTIAL`, `Holdout=NONE`, switch-state GT NOT PRESENT,
OneUI UNASSESSED, settings-diag NEEDS_GROUND_TRUTH — all truthful coverage
states. No corpus expansion required before foundation graduation.

## 5. Count-conformance semantics (G5) — CRITICAL FINDING

### 5.1 Repository evidence

The Harness screenshots manifest records:

```json
"verificationCredential": {
  "ocrExpected": [...],
  "yoloExpectedCounts": { "text_block": 17, "icon": 13, "list_item": 3 }
}
```

The field name is `yoloExpectedCounts` — implying a **YOLO-stage** expectation.
But the manifest does not explicitly pin the boundary:
- pre/post label normalization (`normalize_yolo_label`),
- pre/post interactive-label filtering (`DEFAULT_INTERACTIVE_LABELS`),
- raw `yolo` array vs fused `candidates`.

The first-baseline implementation scored this expectation against **fused
candidate types** (post-fusion `candidates[].type` — after chevron heuristic,
search-box reclassification, unmatched-OCR promotion). Observed: text_block 6
vs 17, icon 6 vs 13, list_item 0 vs 3.

### 5.2 Classification

```text
CountConformanceSemantics:
  STAGE_MISMATCH

  • Expectation stage provenance: UNRESOLVED (manifest field name suggests
    RAW_YOLO stage; exact boundary not recorded).
  • Scoring stage used by baseline: FUSED_EVIDENCE (candidate types).
  • The two stages do not align.

  Result reclassified: DIAGNOSTIC_ONLY / NOT_RELEASE_ELIGIBLE
  (covers both "historical diagnostic expectation only" and "unresolved
  semantic provenance" readings).

  The historical expectation is NOT rewritten (G5 rule honored — the
  manifest bytes are unchanged).
```

### 5.3 Consequence

The Evaluation Foundation graduates. **No future ReleasePolicy may consume
this count-conformance metric as a model-quality gate** until GroundTruth
stage semantics are explicit (G6/G7 below). The metric remains visible in
reports as diagnostic-only.

## 6. Ground truth stage identity (G6) — MINIMAL_DELTA_PURCHASED

```text
GroundTruthStageIdentity:
  MINIMAL_DELTA_PURCHASED (implementation deferred to next task)

Purchase justification: the G5 raw-vs-fused ambiguity is real, repository-
evidenced, and cannot currently be represented truthfully. Existing
PerceptionTask identity does NOT answer "which output boundary does this
expected truth describe" — the same ELEMENT_DETECTION task key scored
fused-stage candidates (synthetic fixtures) and raw-stage counts (harness
manifest) with no distinguishing field.

Minimal delta (design only, NOT implemented here):
  GroundTruth gains: evaluationTargetStage
    ∈ { RAW_DETECTION, OCR, FUSED_EVIDENCE, FINAL_PERCEPTION_EVIDENCE }
  • Optional with default? NO — explicit per GroundTruth record. Existing
    records are re-examined at implementation: synthetic fixture GT →
    FUSED_EVIDENCE; harness-manifest count expectations → RAW_DETECTION
    (with provenance note that the exact boundary remains unrecorded).
  • No other taxonomy dimension is added. This is one field, not a framework.
```

## 7. Metric semantics (G7) — FROZEN

```text
A metric result is valid only if PredictionStage is compatible with
GroundTruthTargetStage.

Compatibility matrix (future):
  RAW_DETECTION GT       ← raw yolo detections (evidence["yolo"] labels)
  OCR GT                 ← raw OCR tokens
  FUSED_EVIDENCE GT      ← fused candidates
  FINAL_PERCEPTION GT    ← serialized post-remap evidence

Mismatch → NOT_SCORABLE, or explicit DIAGNOSTIC_ONLY result.
Stage mismatch NEVER becomes model failure.
```

Note: the current Prediction artifact persists fused candidates + counts but
not the raw yolo label list — implementing RAW_DETECTION scoring will require
persisting the raw `yolo` array in the Prediction artifact. That is part of
the same minimal delta, deferred with G6.

## 8. Performance baseline (G8) — RECORDED_NOT_GATED

```text
Warm analyze ~6.2s (YOLO ~3.2s, OCR ~2.9s, fusion ~1ms), n=3 samples,
settings-home-api35-full-20260803.png, x86_64 macOS, Python 3.11,
single-process/single-worker.
Useful baseline evidence. NOT a release threshold. Foundation neither
graduated nor rejected on this number.
```

## 9. Immutability (G9) — FROZEN

```text
Suite versions immutable. EvaluationRun immutable. Baseline immutable
(write-once; tamper refused — B16 proven).
Evaluator revision change → new EvaluationRun (B17 proven).
Asset addition → new Suite / Run / Baseline (PF2 proven, byte-identical
previous artifacts).
Never retroactively rewrite prior evaluation history.
```

## 10. Evidence sufficiency (G10) — FROZEN

```text
SUFFICIENT | PARTIAL | INSUFFICIENT — separate from model quality.
3/3 on a tiny corpus does NOT imply GENERALIST certification.
Current baseline: PARTIAL (truthful, acceptable).
```

## 11. No single score authority (G11) — FROZEN

```text
OverallSummary: presentation only, no promotion authority.
Future release decisions require: Evidence Sufficiency + Universal Hard
Gates + Profile Gates + Relative Regression + Performance Gates.
```

## 12. Future profile support (G12) — CONFIRMED

Current foundation supports GENERALIST/SPECIALIST + PRIMARY/SECONDARY/
OUT_OF_SCOPE without changing EvaluationAsset or EvaluationRun semantics
(9-dimensional taxonomy + sliceable scorecard + per-asset results proven).
Profiles NOT implemented in this task.

## 13. Training integration landing point (G13) — FROZEN

```text
TrainingRun → ModelArtifact → DeploymentCandidate → SAME EvaluationRun
workflow. Training must not create a second scoring pipeline.
```

## 14. Next slice resequencing (G14) — ADOPTED

The original P4-5 (EvaluationProfile + ReleasePolicy) is **re-sequenced after**
the training/dataset reproducibility foundation.

```text
Rationale (repository facts):
  • evaluation process is proven
  • corpus intentionally small; EvidenceSufficiency PARTIAL
  • NumericThresholds NOT_FROZEN
  • no new Candidate model exists yet
  • training provenance infrastructure does not exist
  • modelVersion lifecycle does not exist
  • canonical configId does not exist

Do NOT implement ReleasePolicy merely because the classes can be written.
Operational ReleasePolicy waits for: a reproducible Candidate, baseline
comparison evidence, sufficient category evidence to calibrate policy.

Next module:
  PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_GATE
```

## 15. Training foundation boundary (G15) — FROZEN SCOPE

The next slice is reproducibility only, NOT a full ML platform:

```text
IN:  Dataset membership/version, annotation provenance, train/validation
     split identity, leakage checks, TrainingConfig identity, TrainingRun,
     checkpoint provenance, model artifact SHA-256, candidate creation.
OUT: automatic retraining, hyperparameter search, training scheduler,
     GPU farm, ModelRegistry service, automatic promotion, automatic deployment.
```

## 16. Evaluation / training separation (G16) — FROZEN

```text
TrainingDataset != EvaluationSuite.
Memberships are explicit independent relationships.
Evaluation GroundTruth may be related to training annotation but must not
silently share authority.
Protected Holdout must not leak into training (release-blocking).
```

## 17. Future candidate flow (G17) — FROZEN TARGET

```text
DatasetVersion → TrainingRun → Checkpoint → ModelArtifact → modelId
→ CANDIDATE → EvaluationSuite → EvaluationRun → Scorecard
→ Candidate vs ACTIVE
ReleasePolicy becomes operational only after this is real.
```

---

## Aggregate freeze

```text
PERCEPTION_PLATFORM_PHASE_4_EVALUATION_FOUNDATION_GRADUATION_RESULT

Decision:                     GRADUATED_WITH_RECORDED_DEFERRALS
Foundation:                   GRADUATED
CanonicalWorkflow:            FROZEN (13 steps, §1)
AssetBoundary:                FROZEN (content-addressed identity, §2)
GroundTruthBoundary:          FROZEN + MINIMAL_DELTA (EvaluationTargetStage)
EvaluationSuiteBoundary:      FROZEN (versioned membership by AssetId)
EvaluationRunBoundary:        FROZEN (deterministic identity, immutable)
ScorecardBoundary:            FROZEN (multidimensional, no single-score authority)
EvidenceSufficiency:          FROZEN (PARTIAL is truthful, never fake)
EvidenceLadder:               FROZEN (L0-L4, no layer masquerade)
BaselineImmutability:         FROZEN
FreshInferenceBoundary:       FROZEN (L2 = fresh current-model inference only)

CountConformanceSemantics:    STAGE_MISMATCH → DIAGNOSTIC_ONLY /
                              NOT_RELEASE_ELIGIBLE (G5; expectation not rewritten)
GroundTruthStageIdentity:     MINIMAL_DELTA_PURCHASED (EvaluationTargetStage
                              field; implementation deferred)
PerformanceBaseline:          RECORDED_NOT_GATED (~6.2s warm, n=3)
Holdout:                      DEFERRED
NumericThresholds:            NOT_FROZEN
Profiles:                     ARCHITECTURE_PURCHASED_IMPLEMENTATION_DEFERRED
ReleasePolicy:                ARCHITECTURE_PURCHASED_IMPLEMENTATION_DEFERRED
TrainingIntegrationLandingPoint: FROZEN (same EvaluationRun workflow)
NextSliceResequencing:        ADOPTED (training/dataset reproducibility
                              before ReleasePolicy)

RuntimeDelta:                 NONE
SemanticDelta:                NONE
AuthorityDelta:               NONE
```

## Next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_TRAINING_DATASET_REPRODUCIBILITY_FOUNDATION_GATE

The gate should also carry the minimal GroundTruth EvaluationTargetStage
delta (G6) as a bundled mechanical fix — it is small, purchased here, and
required before any ReleasePolicy consumes count metrics.

NO_AUTOMATIC_IMPLEMENTATION
NO_RELEASE_POLICY_IMPLEMENTATION_YET
NO_AUTOMATIC_TRAINING
```

STOP.
