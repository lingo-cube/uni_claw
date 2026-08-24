# Semantic Embedding Threshold Scan

> Date: 2026-08-19
> Scope: Safety-first threshold scan for BGE embedding backends on
> DeveloperOptions-v1 style corpus (6 cases: 4 positive, 2 negative).
> Constraint: offline read-only evaluation, no Runtime wiring.

## Method

- Prototypes: DeveloperOptions, WifiSettings, NetworkAndInternet, SettingsRoot.
- Similarity: cosine similarity.
- Candidate returned only if best similarity >= threshold.
- Metric priority: FalseRecovery = 0 first, then maximize Top1.

## BAAI/bge-small-en-v1.5

| Threshold | Top1 | Top3 | Top5 | FalseRecovery | FalsePositive |
|---|---|---|---|---|---|
| 0.30 | 0.667 | 0.667 | 0.667 | 1.000 | 1.000 |
| 0.40 | 0.667 | 0.667 | 0.667 | 1.000 | 1.000 |
| 0.50 | 0.667 | 0.667 | 0.667 | 1.000 | 1.000 |
| 0.55 | 0.667 | 0.667 | 0.667 | 1.000 | 1.000 |
| 0.60 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.65 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.70 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.75 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.80 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.85 | 0.667 | 0.667 | 0.667 | 0.000 | 0.000 |
| 0.90 | 0.333 | 0.333 | 0.333 | 0.000 | 0.000 |

Latency: P50=6.6630ms, P95=8.1259ms, P99=8.1259ms

Best safety-first threshold: **0.85** (FalseRecovery=0, Top1=0.667)

## BAAI/bge-base-en-v1.5

| Threshold | Top1 | Top3 | Top5 | FalseRecovery | FalsePositive |
|---|---|---|---|---|---|
| 0.30 | 0.667 | 0.667 | 0.667 | 1.000 | 1.000 |
| 0.40 | 0.667 | 0.667 | 0.667 | 1.000 | 1.000 |
| 0.50 | 0.667 | 0.667 | 0.667 | 1.000 | 1.000 |
| 0.55 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.60 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.65 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.70 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.75 | 0.833 | 0.833 | 0.833 | 0.500 | 0.500 |
| 0.80 | 0.667 | 0.667 | 0.667 | 0.500 | 0.500 |
| 0.85 | 0.667 | 0.667 | 0.667 | 0.000 | 0.000 |
| 0.90 | 0.333 | 0.333 | 0.333 | 0.000 | 0.000 |

Latency: P50=26.5825ms, P95=28.7592ms, P99=28.7592ms

Best safety-first threshold: **0.85** (FalseRecovery=0, Top1=0.667)

## Conclusion

- Threshold 0.85 can eliminate false recovery on this small corpus.
- Cost: Top1 drops to 0.667 because low-confidence positive cases (e.g. partial
  observation) are rejected.
- The current InMemory baseline remains stronger on this corpus:
  Top1=1.0, FalseRecovery=0.
- A simple global similarity threshold is not sufficient for production.
  Next step should add:
  - more negative samples across identities,
  - per-identity thresholds or classifier,
  - conflict rejection using PreviousVerifiedIdentity / Container History / Vision
    structure.