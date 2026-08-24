# Semantic Embedding Round 2 — Conflict Rejection + Per-Identity Threshold

> Date: 2026-08-19
> Scope: Expanded cross-identity evaluation for BGE embedding backends.
> Corpus: 24 cases across DeveloperOptions / WifiSettings / NetworkAndInternet /
> SettingsRoot (4 positive + 2 negative per identity).
> Methods:
> - Conflict rejection: predicted candidate must match PreviousVerifiedIdentity
>   (when previous identity exists).
> - Structural compatibility: predicted candidate requires at least one
>   overlapping element type with the observation.
> - Per-identity thresholds.

## Results

### BAAI/bge-small-en-v1.5

| Strategy | Top1 | Top3 | Top5 | FalseRecovery | FalsePositive |
|---|---|---|---|---|---|
| Global 0.85, no rules | 0.500 | 0.500 | 0.500 | 0.000 | 0.000 |
| Global best (0.65) + rules | 0.958 | 0.958 | 0.958 | 0.000 | 0.000 |
| Per-identity + rules | **1.000** | **1.000** | **1.000** | **0.000** | **0.000** |

Latency: P50=6.3953ms, P95=7.9953ms, P99=9.4442ms (24 samples)

### BAAI/bge-base-en-v1.5

| Strategy | Top1 | Top3 | Top5 | FalseRecovery | FalsePositive |
|---|---|---|---|---|---|
| Global 0.85, no rules | 0.417 | 0.417 | 0.417 | 0.000 | 0.000 |
| Global best (0.65) + rules | 0.958 | 0.958 | 0.958 | 0.000 | 0.000 |
| Per-identity + rules | **0.958** | **0.958** | **0.958** | **0.000** | **0.000** |

Latency: P50=24.1627ms, P95=28.5508ms, P99=32.4177ms (24 samples)

## Per-identity safe thresholds (with rules)

| Identity | bge-small | bge-base |
|---|---|---|
| DeveloperOptions | 0.30 | 0.30 |
| WifiSettings | 0.30 | 0.30 |
| NetworkAndInternet | 0.65 | 0.65 |
| SettingsRoot | 0.30 | 0.30 |

## Conclusion

- With **conflict rejection + structural compatibility + per-identity thresholds**,
  BGE-small achieves:
  - Top1 = 1.000
  - FalseRecovery = 0.000
- BGE-base achieves:
  - Top1 = 0.958
  - FalseRecovery = 0.000
- The rules remove the false-recovery problem found in the first round.
- BGE-small is currently the best embedding candidate:
  - High accuracy
  - Zero false recovery
  - Lower latency than BGE-base

## Next step

- Validate on a larger held-out corpus / real traces.
- Consider BGE-small as a candidate `IVectorSemanticIndex` backend behind
  `SemanticOptions.VectorBackend`.
- Keep InMemory as production fallback until a real-device corpus confirms the
  result.