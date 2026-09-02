# PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION_RESULT

> Status: SAFETY_NOT_QUALIFIED | Decision: `BGE_SMALL_SAFETY_NOT_QUALIFIED` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION`
> Benchmark report: `docs/benchmarks/bge-small-held-out-validation.md`
> Frozen profile: `semantic-assets/profiles/BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1.json`
> Held-out corpus: `semantic-assets/heldout/ContainerIdentity-heldout-v1.json`

## Decision

```
PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION_RESULT

Decision: BGE_SMALL_SAFETY_NOT_QUALIFIED
```

False recoveries occurred on held-out data (10/24 negatives accepted, FR =
0.4167) and insufficient-evidence cases were admitted (3/7):
**禁止接 Runtime.** The frozen Round-2 configuration (conflict rejection +
structural compatibility + per-identity thresholds 0.30/0.30/0.65/0.30 +
BGE-small v1-text-plus-type feature extraction) does not generalize safely to
ContainerIdentity-heldout-v1. This gate records the failure; no in-gate repair,
no re-declared PASS. No Runtime / provider / index / corpus file was modified by
this gate.

## 1. Frozen Profile

`BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1` (sha256
`80a9a2a83845ab45086af576c04d41feb16bad89d3e1349efc7a71c8e65aedd3`):
Model `BAAI/bge-small-en-v1.5` (fastembed + ONNX, dim 384, cosine) ·
FeatureExtraction `v1-text-plus-type` · IdentityPrototype `v1-canonical-signatures`
(4 prototypes) · GlobalRules `v1-rules` (R1 structural, R2 previous-identity
conflict fail-closed, R3 per-identity threshold, R4 minimum-evidence) ·
Thresholds DeveloperOptions 0.30 / WifiSettings 0.30 / NetworkAndInternet 0.65 /
SettingsRoot 0.30 (Round-2 table). Frozen before held-out execution; immutability
proven by test T2 (profile sha stable across evaluation, pinned by the BGE
report) and thresholds unchanged by T3.

## 2. Held-out Corpus

`ContainerIdentity-heldout-v1` — 48 cases (≥ 40 required): 4 identities × 12
(A Normal ×2, B title-offscreen/scroll ×2, C partial ×2, D low-information ×1,
E similar-page interference ×2, F hard negative ×3). Source distribution:
RealTrace 10 / Manual 27 / Synthetic 7 / Regression 4. Independent of the
tuning corpus: no shared case ids, no shared element fingerprints (T0/T1 proof;
tuning set excluded from every tuning helper). Corpora physically under
`semantic-assets/{tuning,heldout,regression,adversarial}/`.

## 3. Accuracy

| Metric | BGE-small frozen | InMemory (same corpus) |
|---|---|---|
| Top1 | **0.7500 (36/48)** | 0.4167 |
| Top3 | 0.7917 | 0.4167 |
| Top5 | 0.7917 | 0.4167 |

Positive recall: DeveloperOptions 1.0 · NetworkAndInternet 1.0 · SettingsRoot 1.0 ·
WifiSettings 0.6667. Both positive misses are WifiSettings pages ranked
NetworkAndInternet first (EMBEDDING_SEPARATION_FAILURE). Accuracy generalization
is insufficient (1.00 → 0.75), but safety is the binding disqualifier.

## 4. Safety

| Metric | BGE-small frozen | InMemory (same corpus) |
|---|---|---|
| False Recovery Rate | **0.4167 (10/24)** | 0.9583 |
| False Positive Rate | 0.4167 | 0.9583 |
| Hard Negative Rejection Rate | 0.5833 | 0.0417 |
| Abstention Correctness | 0.5833 | 0.0417 |
| Conflict-rejection violations (GATE 3) | 0 | 0 (rule not implemented by index; violations inherent to index design) |

HARD GATE 1 FAIL (`FR = 0.4167 ≠ 0`), HARD GATE 4 FAIL (3 insufficient-evidence
cases admitted: ho-dev-D1, ho-net-D1, ho-net-F3), HARD GATE 3 PASS (fail-closed
conflict rejection held on every case), HARD GATE 2 PASS vs identical-corpus
InMemory (0.4167 ≤ 0.9583).

## 5. Performance

BGE-small P50 3.81 ms / P95 6.80 ms / P99 7.40 ms (48 samples). Inside the Fast
Semantic budget; no regression vs Round-2 baseline (P95 8.0 ms). InMemory:
P50 0.0014 / P95 0.0025 / P99 0.0033 ms (240 samples; fixture-grade index).

## 6. Failure Distribution (12 failed cases)

| Classification | Count |
|---|---|
| THRESHOLD_GENERALIZATION_FAILURE | 9 |
| EMBEDDING_SEPARATION_FAILURE | 2 |
| FEATURE_REPRESENTATION_FAILURE | 1 |
| STRUCTURAL_RULE_FAILURE / IDENTITY_PROTOTYPE_FAILURE / CORPUS_DEFECT / UNKNOWN | 0 |

Dominant mechanism: BGE-small's held-out similarity distribution is dense
(0.66–0.92 for must-abstain content, including unrelated pages and single rows);
the frozen per-identity thresholds cannot gate it. One case (ho-net-F3) admitted
a claim from a near-empty query (zero text tokens) — feature-representation
weakness. One designated text-overlap stress probe (ho-root-F2) was included per
the gate's hard-negative priority; the other 23 negatives were abstainable via
the frozen rules, so the 10 false recoveries are genuine over-assertions.

## 7. InMemory Comparison (identical corpus, identical metrics)

InMemory (production default) dropped from its tuning record (Top1 1.0, FR 0,
FP 0) to Top1 0.4167, FR/FP 0.9583 on held-out — the incumbent **does not
generalize** (overlap scoring over-fits the tuning vocabulary). BGE-small +
frozen profile exceeds it on every metric (accuracy +33 pts, FPR −54 pts) and
never violated conflict rejection. Nonetheless, the absolute safety bar is not
met; relative superiority does not qualify.

## 8. Gate proofs (tests/Semantic)

PASS: T0 (tuning shape) · T1 (held-out independence) · T2 (profile immutability) ·
T3 (thresholds unchanged vs Round-2) · T5 (conflict rejection fail-closed) ·
T7 (per-identity breakdown, both backends) · T9 (committed assets match code).
RED (honest failure record — NOT fixed in-gate): T4 (hard-negative rejection
1.0 required, measured 0.5833) · T6 (insufficient-evidence abstention required,
3 admitted) · T8 (BGE false recovery must be 0, measured 0.4167; FPR ≤ InMemory
holds). Pre-existing suite: 32/32 PASS.

## NEXT_GATE

`PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_ANALYSIS`

Analysis-only: (1) diagnose the dense-similarity root cause — embedder choice,
`v1-text-plus-type` query representation (type-word contamination), and
prototype text quality; (2) refit feature representation / per-identity
thresholds on an expanded tuning corpus (never on held-out); (3) re-run this
SAME `ContainerIdentity-heldout-v1` corpus to turn T4/T6/T8 green. Runtime
integration (`PROJECT_LEADER_BGE_SMALL_VECTOR_INDEX_INTEGRATION`) remains
forbidden until the held-out safety gates pass.

Runtime / ISemanticProvider / IVectorSemanticIndex / SemanticEvidence /
default backend / InMemory fallback: **unchanged**.