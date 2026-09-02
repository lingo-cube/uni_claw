# BGE-small Held-out Validation — ContainerIdentity-heldout-v1

> Gate: `PROJECT_LEADER_BGE_SMALL_HELD_OUT_VALIDATION`
> Date: 2026-08-30
> Decision doc: `docs/decisions/bge-small-held-out-validation-result.md`
> Scope: validate whether the frozen Round-2 settings (rules + per-identity
> thresholds + BGE-small embedding configuration) generalize to data that never
> participated in rule design or threshold tuning.
> Constraints honored: no Runtime / SemanticEvidence / ISemanticProvider /
> IVectorSemanticIndex / default-backend / InMemory-fallback change; no BGE
> production wiring; no HuggingFace runtime / Ray / Slow Semantic / Qwen VLM
> integration; zero tuning on held-out results.

## 1. Frozen Evaluation Profile

`BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1`
(`semantic-assets/profiles/BGE_SMALL_CONTAINER_IDENTITY_PROFILE_V1.json`,
sha256 `80a9a2a83845ab45086af576c04d41feb16bad89d3e1349efc7a71c8e65aedd3`):

| Field | Value |
|---|---|
| ModelId | `BAAI/bge-small-en-v1.5` |
| ModelRevision | fastembed-pinned (recorded in report) |
| EmbeddingDimension | 384 |
| FeatureExtractionVersion | `v1-text-plus-type` (per-element `text (type)`) |
| IdentityPrototypeVersion | `v1-canonical-signatures` (4 prototypes, derived from tuning-corpus canonical signatures) |
| GlobalRulesVersion | `v1-rules` (R1 structural compatibility, R2 previous-identity conflict rejection fail-closed, R3 per-identity threshold, R4 minimum-evidence abstention) |
| PerIdentityThresholds | DeveloperOptions 0.30 · WifiSettings 0.30 · NetworkAndInternet 0.65 · SettingsRoot 0.30 (Round-2 table, `docs/benchmarks/semantic-embedding-round2.md`) |
| SimilarityMetric | cosine |
| Backend | fastembed + ONNX (runtime tool of the Round-1/2 evaluations) |
| TargetCorpusVersion | `ContainerIdentity-heldout-v1` |

Provenance is recorded in the profile: it was reconstructed from the committed
docs/corpora and frozen **before** any held-out case was executed. Thresholds,
prototypes, feature extraction and rules were never changed afterwards (T2/T3).

InMemory comparison profile: `INMEMORY_PRODUCTION_DEFAULT_PROFILE_V1` — the
existing production-default `InMemoryVectorSemanticIndex` (4-identity pattern
set, threshold 0.3), unmodified.

## 2. Held-out Corpus — ContainerIdentity-heldout-v1

`semantic-assets/heldout/ContainerIdentity-heldout-v1.json`
(sha256 `fd415a52511db7190fa26859eb1b11d805fba54f57414b4f60f9389449ed63c5`).

| Dimension | Value |
|---|---|
| Corpus version | v1 |
| Case count | 48 |
| Identity distribution | DeveloperOptions 12 · WifiSettings 12 · NetworkAndInternet 12 · SettingsRoot 12 |
| Expected negatives | 24 (D low-info, E similar-page interference, F hard negatives) |
| Source distribution | RealTrace 10 (verbatim/contiguous subsets of committed capture evidence) · Manual 27 · Synthetic 7 · Regression 4 |
| Difficulty | Easy 8 · Medium 16 · Hard 24 |
| Viewport state | TitleVisible 9 · TitleOffscreen 11 · Partial 9 · WrongPage 16 · Unknown 3 |
| Ambiguity level | 0 → 22 · 1 → 3 · 2 → 19 · 3 → 4 |

Per identity the corpus covers A Normal x2, B title-offscreen/scroll x2,
C partial observation x2, D low-information x1, E similar-page interference x2,
F hard negative x3 (wrong container / visually · text-overlap similar container
/ insufficient evidence incl. empty and near-empty semantic queries).

Isolation proofs: `HeldOutValidationTests.T0` (tuning shape pinned: 10 corpora /
35 cases) and `T1` (held-out ids and element fingerprints disjoint from every
tuning corpus; category `Experimental`, excluded from tuning helpers).

## 3. Metrics Definition (shared by both backends)

Top1 accuracy = cases where the final decision (single claim or abstain)
equals the expected candidate ("None" == abstain). Top3/Top5 recall = expected
identity in the first 3/5 admitted candidates (abstain counts as hit only for
expected-None). False recovery = **any accepted claim** on an expected-None
case (acceptance == recovery gate; every accepted claim here was ≥ 0.659), i.e.
FR == FP by construction on both pipelines. Hard-negative rejection rate =
expected-None cases abstained. Abstention correctness = same. Latency = per-case
decision (feature extraction + embed + cosine + rules) P50/P95/P99.

## 4. Results — BGE-small frozen profile

Report: `semantic-assets/heldout/reports/container-identity-heldout-v1-bge-small-profile-v1.json`

### Accuracy

| Metric | Value |
|---|---|
| Top1 Accuracy | 0.7500 (36/48) |
| Top3 Recall | 0.7917 |
| Top5 Recall | 0.7917 |

### Safety

| Metric | Value |
|---|---|
| False Recovery Rate | **0.4167 (10/24)** — HARD GATE 1 VIOLATED |
| False Positive Rate | 0.4167 (10/24) |
| Hard Negative Rejection Rate | 0.5833 (14/24) |
| Abstention Correctness | 0.5833 |
| PreviousVerifiedIdentity conflict violation | 0 (fail-closed holds — GATE 3 PASS) |

### Performance

| Metric | Value |
|---|---|
| P50 | 3.81 ms |
| P95 | 6.80 ms |
| P99 | 7.40 ms |
| Samples | 48 |

P95 ≈ 6.8 ms remains inside the Fast Semantic acceptable band (Round-2
baseline P95 8.0 ms; no regression; measured on this environment).
No present performance budget violation.

### Breakdown

**By identity** (positive cases only; ALL 10 false recoveries fall in the
expected-None bucket):

| Identity | Cases | Top1 | FP |
|---|---|---|---|
| DeveloperOptions | 6 | 1.0000 | 0 |
| WifiSettings | 6 | 0.6667 | 0 |
| NetworkAndInternet | 6 | 1.0000 | 0 |
| SettingsRoot | 6 | 1.0000 | 0 |
| None (negatives) | 24 | 0.5833 | 10 |

**By difficulty**: Easy 0.875 · Medium 0.9375 · Hard 0.5833 (10 FP).
**By source**: RealTrace 0.80 (2 FP) · Manual 0.7407 (5 FP) · Regression 0.75 (1 FP) · Synthetic 0.7143 (2 FP).
**By viewport**: TitleVisible 0.8889 (0 FP) · TitleOffscreen 0.7273 (2 FP) · Partial 1.0 (0 FP) · WrongPage 0.5625 (7 FP) · Unknown 0.6667 (1 FP).
**By ambiguity**: 0 → 0.9091 · 1 → 1.0 · 2 → 0.5263 (9 FP — ambiguity is the dominant failure axis) · 3 → 0.75 (1 FP).

## 5. Failure Analysis (12 failed cases, 10 = false recovery, 2 = positive misses)

| Classification | Count | Cases |
|---|---|---|
| THRESHOLD_GENERALIZATION_FAILURE | 9 | ho-dev-D1, ho-dev-F2, ho-wifi-E1, ho-wifi-E2, ho-net-D1, ho-net-E1, ho-net-F1, ho-root-E1, ho-root-F2 |
| EMBEDDING_SEPARATION_FAILURE | 2 | ho-wifi-A2, ho-wifi-B2 (positive misses: WifiSettings page ranks NetworkAndInternet first — "net magnet") |
| FEATURE_REPRESENTATION_FAILURE | 1 | ho-net-F3 (near-empty query: zero text tokens still embedded to 0.659 → net claim) |
| STRUCTURAL_RULE_FAILURE / IDENTITY_PROTOTYPE_FAILURE / CORPUS_DEFECT / UNKNOWN | 0 | — |

Root cause pattern: BGE-small's embedding distribution for short UI label lists
is **dense**: held-out observations score 0.66–0.92 against all four prototypes,
including content that must abstain (e.g. "Accessibility · Display · Color and
motion" → DeveloperOptions 0.777; single "System" row → DeveloperOptions 0.776;
root-row subset with previous NetworkAndInternet → NetworkAndInternet 0.921).
The frozen per-identity thresholds (0.30/0.30/0.65/0.30), calibrated on the
24-case tuning set, cannot gate this distribution — the acceptance band admits
nearly anything network/root-vocabulary-adjacent. The 2 positive misses are
ordering failures (Wi-Fi page content ranks NetworkAndInternet first), and the
near-empty query admits a spurious network claim — a feature-representation
weakness of embedding empty evidence.

Audit note (corpus design): one designated stress probe (ho-root-F2,
settings-search label soup) was intentionally included among the 24 negatives;
the remaining 23 negatives are abstainable via the frozen rules (conflict or
threshold) — the 10 false recoveries are therefore genuine over-assertions, not
corpus artifacts.

## 6. InMemory Baseline Comparison (identical corpus, identical metrics)

Report: `semantic-assets/heldout/reports/container-identity-heldout-v1-inmemory-profile-v1.json`

| Metric | InMemory (production default) | BGE-small frozen profile | Delta |
|---|---|---|---|
| Top1 Accuracy | 0.4167 | 0.7500 | +0.3333 |
| Top3 Recall | 0.4167 | 0.7917 | +0.3750 |
| Top5 Recall | 0.4167 | 0.7917 | +0.3750 |
| False Recovery Rate | 0.9583 (23/24) | 0.4167 (10/24) | −0.5417 |
| False Positive Rate | 0.9583 | 0.4167 | −0.5417 |
| Hard Negative Rejection | 0.0417 | 0.5833 | +0.5417 |
| P50 / P95 / P99 (ms) | 0.0014 / 0.0025 / 0.0033 | 3.81 / 6.80 / 7.40 | BGE slower but within budget |

Finding: the production-default InMemory index **does not generalize** — on the
tuning corpus it recorded Top1=1.0, FR=0, FP=0; on the held-out corpus its
overlap scorer accepts 23/24 negatives (worst case: an empty-text "type:text"
observation at confidence 1.0). BGE-small + frozen profile is substantially
better on every metric (accuracy +33pts, FPR −54pts) and **never** emitted a
claim that violated PreviousVerifiedIdentity conflict rejection. But BGE-small
still fails the absolute safety bar.

## 7. Qualification Criteria

| Gate | Criterion | Result |
|---|---|---|
| HARD GATE 1 | False Recovery Rate = 0 | **FAIL (0.4167)** |
| HARD GATE 2 | FPR ≤ current safety baseline (identical-corpus InMemory 0.9583) | PASS (0.4167) |
| HARD GATE 3 | PreviousVerifiedIdentity conflict rejection fail-closed | PASS (0 violations) |
| HARD GATE 4 | Abstain without sufficient evidence | **FAIL (3 low-evidence cases admitted: ho-dev-D1, ho-net-D1, ho-net-F3)** |
| Performance | Fast Semantic acceptable band (~8 ms P95 comparison baseline) | PASS (6.80 ms, no regression recorded vs 8.0 ms Round-2) |
| Accuracy | stable high recall on held-out | **FAIL (Top1 0.75 vs 1.00 tuning; generalizing accuracy gap)** |

Safety does not qualify: **BGE_SMALL_SAFETY_NOT_QUALIFIED**.
(Accuracy generalization is also insufficient — Top1 0.75, 2 ordering misses —
but the binding disqualifier is the false recovery rate.)

## 8. Next Step (analysis only — no Runtime wiring)

Per the gate, a failed gate is recorded, not repaired inside this gate. The
recommended next gate is an analysis of the feature/prototype/threshold buyer
(embedding representation, prototype texts, per-identity threshold refit on an
expanded tuning corpus) and re-running THIS SAME held-out corpus to turn the
safety gates green, before any vector-index integration is reconsidered.

See `docs/decisions/bge-small-held-out-validation-result.md`.