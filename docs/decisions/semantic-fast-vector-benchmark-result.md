# Semantic Fast Vector Benchmark Result

> Date: 2026-08-19
> Role: Project Leader / Benchmark Verifier
> Gate: `PROJECT_LEADER_SEMANTIC_FAST_VECTOR_BENCHMARK`
> Base: `PROJECT_LEADER_SEMANTIC_CAPABILITY_INFRASTRUCTURE_BASELINE_RESULT`
> Result: `PROJECT_LEADER_SEMANTIC_FAST_VECTOR_BENCHMARK_RESULT`
> Decision: **FAST_SEMANTIC_VECTOR_BENCHMARK_ESTABLISHED**
> NEXT_GATE: **PROJECT_LEADER_SEMANTIC_VECTOR_BACKEND_EVALUATION**

## 1. Summary

Established the first Fast Semantic Container Identity benchmark using the
existing Semantic infrastructure:

- Corpus: `DeveloperOptions-v1`
- Provider: `FastSemanticContainerIdentityProvider`
- Index: `InMemoryVectorSemanticIndex`
- Runner: `SemanticBenchmarkRunner`
- Evaluation: `SemanticEvaluator`

No Semantic capability expansion, no Slow Semantic, no LLM, no Vector Memory
write.

## 2. Corpus

5 cases covering:

- Title visible
- Title leaves viewport
- Partial elements missing
- Wrong page
- Similar page interference

## 3. Accuracy baseline

| Metric | Value |
|---|---|
| Top1 Accuracy | 1.0000 |
| Top3 Recall | 1.0000 |
| Top5 Recall | 1.0000 |
| TopK Recall (K=3) | 1.0000 |

## 4. Safety baseline

| Metric | Value |
|---|---|
| False Recovery Rate | 0.0000 |
| False Positive Rate | 0.0000 |

No wrong-page or similar-page case triggered recovery.

## 5. Confidence baseline

| Metric | Value |
|---|---|
| Mean Confidence | 0.2667 |
| Accuracy | 1.0000 |
| Calibration Error | 0.7333 |

Confidence is measured as evidence weight only; Runtime threshold remains
unchanged.

## 6. Latency baseline

| Metric | Value |
|---|---|
| P50 | 0.0028 ms |
| P95 | 1.0103 ms |
| P99 | 1.2088 ms |
| Samples | 5 |

## 7. Test coverage

Added in `tests/Semantic/BenchmarkTests`:

- T1 Correct retrieval
- T2 TopK calculation
- T3 False recovery detection
- T4 Confidence evaluation
- T5 Latency measurement
- T6 Empty vector result
- T7 Regression case loading

Semantic.Tests: **15/15 PASS**.

## 8. Benchmark report

- `docs/benchmarks/semantic-fast-vector-benchmark-developer-options.md`

## 9. Validation

```text
dotnet build src/UniClaw.Runtime.sln                → 0 warnings, 0 errors
dotnet test tests/Semantic/Semantic.Tests.csproj    → 15/15 PASS
openspec validate --changes --strict                → PASS
scripts/check-consistency.sh                        → ALL PASS
```

## 10. Decision

```text
PROJECT_LEADER_SEMANTIC_FAST_VECTOR_BENCHMARK_RESULT
Decision: FAST_SEMANTIC_VECTOR_BENCHMARK_ESTABLISHED
NEXT_GATE: PROJECT_LEADER_SEMANTIC_VECTOR_BACKEND_EVALUATION
```

No Runtime / Semantic Contract / Provider Boundary change required. No STOP.