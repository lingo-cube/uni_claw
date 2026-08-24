# Semantic Capability Infrastructure Baseline

> Date: 2026-08-19
> Role: Project Leader / Infrastructure Baseline
> Base: `PROJECT_LEADER_SEMANTIC_ARCHITECTURE_FREEZE_RESULT` (frozen)
> Result: `PROJECT_LEADER_SEMANTIC_CAPABILITY_INFRASTRUCTURE_BASELINE_RESULT`
> Decision: **SEMANTIC_CAPABILITY_INFRASTRUCTURE_BASELINE_APPLIED**
> NEXT_GATE: **PROJECT_LEADER_SEMANTIC_FAST_VECTOR_BENCHMARK**

## 1. Why Semantic Infrastructure is needed

Semantic capability has graduated through Contract → Evidence Fusion → Fast
Semantic Container Identity. To make it manageable, evaluable, and replaceable as
an engineering capability, it needs infrastructure:

- Unified configuration instead of scattered options.
- Managed Semantic corpus for real-world / regression / synthetic cases.
- Evaluation framework for accuracy, safety, confidence, performance.
- Benchmark runner producing standard reports.

This baseline adds infrastructure only. It does not extend Semantic capability and
does not change Runtime behavior.

## 2. Responsibility boundaries

```text
Capabilities/Perception/Semantic
├── Contract
│   ├── SemanticEvidence
│   └── ISemanticProvider
├── Providers
│   └── Fast Semantic Provider
├── Retrieval
│   ├── IVectorSemanticIndex
│   └── Vector Backend Adapter (future)
├── Embedding
│   └── Embedding Provider abstraction (target architecture)
├── Corpus
│   └── Semantic Test Assets
├── Evaluation
│   ├── Accuracy Evaluation
│   ├── Safety Evaluation
│   ├── Calibration Evaluation
│   └── Performance Evaluation
├── Benchmark
│   └── Benchmark Runner
└── Configuration
    └── Semantic Options
```

Phase 1 implemented:

- Configuration Baseline
- Semantic Asset / Categorization Baseline
- Evaluation Framework Skeleton
- Benchmark Runner Skeleton

Not implemented in Phase 1:

- Vector Backend extension
- New Semantic Provider

## 3. Configuration management

Added `SemanticOptions` under `Capabilities/Perception/Semantic/Configuration`.

Supports:

- Provider enable (`FastSemanticProviderEnabled`)
- Vector backend selection (`VectorBackend`)
- Benchmark configuration (`SemanticBenchmarkOptions`)
- Evaluation parameters (`SemanticEvaluationOptions`)

Runtime continues to consume only `ISemanticProvider`; it does not read Semantic
configuration directly.

## 4. Corpus management

Added under `Capabilities/Perception/Semantic/Corpus`:

- `SemanticCase` — CaseId, InputObservation, ExpectedCandidate, ExpectedIdentity,
  Source, Difficulty.
- `SemanticCorpus` — named case collection (e.g. `DeveloperOptions-v1`).
- `SemanticCaseSource` — RealWorld / Regression / Synthetic.
- `SemanticCaseDifficulty` — Easy / Medium / Hard.

Phase 1 supports Container Identity cases only. Element Meaning / Relation cases
are not included.

## 5. Evaluation framework

Added under `Capabilities/Perception/Semantic/Evaluation`:

- `SemanticEvaluationMetrics`
  - Retrieval: Top1 Accuracy, TopK Recall
  - Safety: False Recovery Rate, False Positive Rate
  - Confidence: Calibration Error, Mean Confidence, Accuracy
  - Performance: Latency P50 / P95 / P99
- `ISemanticEvaluator`
- `SemanticEvaluator` skeleton implementation

## 6. Benchmark framework

Added under `Capabilities/Perception/Semantic/Benchmark`:

- `SemanticBenchmarkRunner`
- `SemanticBenchmarkReport`
- `SemanticCaseResult`

Input: Provider + Corpus + Configuration.
Output: Standard report with provider id, corpus id, metrics, and per-case results.

## 7. Future Vector Backend extension direction

- Keep `IVectorSemanticIndex` as the retrieval seam.
- Future backends (e.g. real vector DB) implement `IVectorSemanticIndex`.
- Backend selection goes through `SemanticOptions.VectorBackend`.
- Memory remains read-only; no Runtime auto-write.
- Future pipeline remains:

```text
Trace → Post Processing → Semantic Pattern → Validation → Vector Memory
```

## 8. Forbidden expansion scope

- No Slow Semantic
- No LLM
- No automatic Vector Memory write
- No new SemanticProvider
- No Element Meaning / Relation expansion
- No Action Recommendation / Planning
- No Runtime Authority / Agent / Goal / Action / Planner / L1 / DSH change
- No SemanticEvidence / ISemanticProvider contract change
- No Resolver Authority change

## 9. Test asset management

Independent Semantic test project created:

```text
tests/Semantic
├── ContractTests
├── ProviderTests
├── CorpusTests
├── EvaluationTests
├── BenchmarkTests
└── RegressionTests
```

Project: `tests/Semantic/Semantic.Tests.csproj` (independent from Runtime tests).

## 10. Validation

```text
dotnet build src/UniClaw.Runtime.sln
→ 0 warnings, 0 errors

dotnet test tests/Semantic/Semantic.Tests.csproj
→ 8/8 PASS

openspec validate --changes --strict --no-interactive
→ PASS

scripts/check-consistency.sh
→ ALL PASS
```

## 11. Decision

```text
PROJECT_LEADER_SEMANTIC_CAPABILITY_INFRASTRUCTURE_BASELINE_RESULT
Decision: SEMANTIC_CAPABILITY_INFRASTRUCTURE_BASELINE_APPLIED
NEXT_GATE: PROJECT_LEADER_SEMANTIC_FAST_VECTOR_BENCHMARK
```

- Not changed Runtime behavior
- Not changed Semantic Contract
- Not changed Agent authority
- Not implemented Slow Semantic