# Semantic Corpus Expansion Baseline

> Date: 2026-08-19
> Role: Project Leader / Corpus Engineering Baseline
> Base: `PROJECT_LEADER_SEMANTIC_VECTOR_BACKEND_EVALUATION_RESULT`
> Result: `PROJECT_LEADER_SEMANTIC_CORPUS_EXPANSION_BASELINE_RESULT`
> Decision: **SEMANTIC_CORPUS_EXPANSION_BASELINE_ESTABLISHED**
> NEXT_GATE: **PROJECT_LEADER_SEMANTIC_REAL_WORLD_CORPUS_COLLECTION**

## 1. Corpus goal

Turn Semantic Corpus from a small set of benchmark cases into a manageable,
extendable, regression-verifiable data asset. The corpus remains a benchmark
asset only; it never enters Runtime decisions.

## 2. Case model

`SemanticCase` now supports:

- Core: CaseId, Identity, InputObservation, ExpectedCandidate, ExpectedIdentity,
  Source, Difficulty
- Metadata: ViewportState, ScrollPosition, VisibleAnchorState, NoiseLevel,
  AmbiguityLevel

Metadata is used only for Dataset management and Benchmark analysis. It does not
enter Runtime Decision.

`SemanticCaseSource` now supports:

- RealTrace
- Manual
- Synthetic
- Regression

## 3. Category design

`SemanticCorpusCategory`:

```text
semantic-assets/
├── golden       (verified correct samples)
├── regression   (historical failure / repair samples)
├── adversarial  (false-recovery-prone samples)
└── experimental (exploration samples)
```

Different categories can be loaded and benchmarked independently via
`SemanticCorpusCatalog.FilterByCategory`.

## 4. Source management

Each case records its source (`RealTrace` / `Manual` / `Synthetic` / `Regression`).
Runtime is forbidden from automatically writing Corpus.

## 5. Validation rules

`SemanticCorpusValidator` checks:

- CaseId uniqueness
- ExpectedCandidate non-empty
- ExpectedIdentity required for non-negative cases
- InputObservation valid
- Metadata complete (ViewportState, VisibleAnchorState, NoiseLevel, AmbiguityLevel,
  ScrollPosition)

Invalid corpus cannot enter Benchmark.

## 6. Benchmark integration

Benchmark can be run per corpus category:

```text
semantic benchmark --corpus golden
semantic benchmark --corpus regression
```

The category filter returns the appropriate corpus set; the existing
`SemanticBenchmarkRunner` then executes normally.

## 7. Runtime boundary

- Corpus is an evaluation asset only.
- Metadata never enters Runtime Decision.
- Runtime continues to depend only on `ISemanticProvider`.
- No change to SemanticEvidence / ISemanticProvider / Runtime authority.

## 8. Forbidden auto-learning scope

- No Runtime automatic Corpus write.
- No automatic Vector Memory write.
- No Slow Semantic / LLM.
- No Element Meaning / Relation expansion.

## 9. Tests

`tests/Semantic/CorpusTests` adds:

- T1 Case validation
- T2 Category loading
- T3 Golden corpus loading
- T4 Regression corpus loading
- T5 Invalid case rejection
- T6 Benchmark category filtering
- T7 Metadata preservation

Semantic.Tests: **29/29 PASS**.

## 10. Validation

```text
dotnet build src/UniClaw.Runtime.sln                → 0 warnings, 0 errors
dotnet test tests/Semantic/Semantic.Tests.csproj    → 29/29 PASS
openspec validate --changes --strict                → PASS
scripts/check-consistency.sh                        → ALL PASS
```

## 11. Decision

```text
PROJECT_LEADER_SEMANTIC_CORPUS_EXPANSION_BASELINE_RESULT
Decision: SEMANTIC_CORPUS_EXPANSION_BASELINE_ESTABLISHED
```

Confirmed:

- 未改变 Runtime 行为
- 未改变 Semantic Contract
- 未实现 Slow Semantic
- 未引入 Vector Memory Write
- Corpus 仅作为评估资产