# tuning/ — Tuning Corpus Registry (Reference Only)

The tuning corpus lives in the canonical C# benchmark corpora
(`tests/Semantic/BenchmarkTests/`) and is referenced here — it is NOT copied
into `semantic-assets`. Held-out validation must never load or tune against it.

## Corpus inventory (used to design rules and per-identity thresholds)

| Corpus | Cases | Role |
|---|---|---|
| `DeveloperOptions-v1` (DeveloperOptionsBenchmarkCorpus) | 5 | Original A–E benchmark corpus |
| `WifiSettings-v1` (ContainerIdentityCorpora) | 2 | Per-identity seed corpus |
| `NetworkAndInternet-v1` (ContainerIdentityCorpora) | 2 | Per-identity seed corpus |
| `SettingsRoot-v1` (ContainerIdentityCorpora) | 2 | Per-identity seed corpus |
| `DeveloperOptions-golden-v1` … `SettingsRoot-golden-v1` (ExpandedContainerIdentityCorpora) | 20 | Golden A–E per identity |
| `container-identity-regression-v1` (ExpandedContainerIdentityCorpora) | 3 | Historical failure regression |
| `container-identity-adversarial-v1` (ExpandedContainerIdentityCorpora) | 1 | Adversarial seed |
| **Total (unique)** | **35** | — |

The **Round-2 tuning set** that produced the per-identity threshold table
(DeveloperOptions 0.30, WifiSettings 0.30, NetworkAndInternet 0.65,
SettingsRoot 0.30 — `docs/benchmarks/semantic-embedding-round2.md`) was a
24-case subset of the above (4 positive + 2 negative per identity).

## Isolation contract

The disjointness proof (ids + element fingerprints) between the tuning set
above and `ContainerIdentity-heldout-v1` is computed in
`HeldOutValidationTests.T1` and pinned by `T0_TuningCorpusShapeIsStable`
(10 corpora / 35 cases).