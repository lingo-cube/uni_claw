# Semantic Fast Vector Benchmark — DeveloperOptions

> Date: 2026-08-19
> Benchmark: `semantic-fast-vector-benchmark-developer-options`
> Base: `PROJECT_LEADER_SEMANTIC_CAPABILITY_INFRASTRUCTURE_BASELINE_RESULT`
> Scope: Fast Semantic Container Identity Retrieval only

## 1. Corpus

- Corpus ID: `DeveloperOptions-v1`
- Cases: 5
- Categories: RealWorld, Regression, Synthetic

| Case | Difficulty | Expected |
|---|---|---|
| dev-A-title-visible | Easy | DeveloperOptions |
| dev-B-title-offscreen | Medium | DeveloperOptions |
| dev-C-partial-elements | Medium | DeveloperOptions |
| dev-D-wrong-page | Hard | None |
| dev-E-similar-page | Hard | None |

## 2. Provider

- Provider: `FastSemanticContainerIdentityProvider`
- Source: `FAST`
- Output: `SemanticEvidence` (ContainerIdentity)

## 3. Index

- Index: `InMemoryVectorSemanticIndex`
- Backend: InMemory (read-only)
- Pattern: DeveloperOptions-v1

## 4. Accuracy

| Metric | Value |
|---|---|
| Top1 Accuracy | 1.0000 |
| Top3 Recall | 1.0000 |
| Top5 Recall | 1.0000 |
| TopK Recall (K=3) | 1.0000 |

## 5. Safety

| Metric | Value |
|---|---|
| False Recovery Rate | 0.0000 |
| False Positive Rate | 0.0000 |

Wrong page and similar-page cases did not trigger recovery.

## 6. Confidence

| Metric | Value |
|---|---|
| Mean Confidence | 0.2667 |
| Accuracy | 1.0000 |
| Calibration Error | 0.7333 |

Confidence is measured as evidence weight only; no Runtime threshold was modified.

## 7. Latency

| Metric | Value |
|---|---|
| P50 | 0.0044 ms |
| P95 | 1.0405 ms |
| P99 | 1.2451 ms |
| Samples | 5 |

Latency is in-memory provider end-to-end measurement. Feature extraction and
vector retrieval are included in the provider call.

## 8. Conclusion

Fast Semantic Container Identity retrieval on the DeveloperOptions-v1 corpus:

- Achieves perfect retrieval accuracy on the current in-memory corpus.
- Has zero false recovery / false positive in this benchmark slice.
- Runs in sub-millisecond P99 on the in-memory index.

This is an initial baseline for future backend evaluation.