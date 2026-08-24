# Fast Semantic Container Identity — Real-World Validation

> Date: 2026-08-19
> Role: Project Leader / Validation Verifier
> Gate: `PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_REAL_WORLD_VALIDATION`
> Input: `PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_GRADUATED`
> Result: `PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_REAL_WORLD_VALIDATION_RESULT`
> Classification: **A. VALIDATED** (deterministic Runtime-shaped validation)

## 1. Scope

This gate validates Fast Semantic Container Identity Recovery against the known
**DeveloperOptions** scrollable-container scenarios. The validation is executed
as a deterministic Runtime-shaped harness in the test project, using the
graduated `FastSemanticContainerIdentityProvider` + `InMemoryVectorSemanticIndex`
+ `SemanticEvidenceFusion`.

No production code was changed. No Agent / Resolver / Vision / L1 / DSH
modification. No new Semantic type, Element Meaning, Relation, Slow Semantic, or
LLM.

## 2. Validation test artifact

Added:

- `tests/UniClaw.Runtime.Tests/Perception/FastSemanticContainerIdentityRealWorldValidationTests.cs`

It simulates Runtime authority deterministically and records each run.

## 3. Scenario matrix

| Run | ObsSeq | TextResolverResult | FastSemanticResultCount | Candidate | Confidence | RuntimeValidationResult | FinalContainerIdentity | FinalBelief | SemanticContradiction |
|---|---|---|---|---|---|---|---|---|---|
| A TitleVisible | 1 | DeveloperOptions | 1 | DeveloperOptions | 0.50 | TEXT_RESOLVER_SUCCESS | DeveloperOptions | DeveloperOptions | false |
| B TitleOffscreen | 2 | null | 1 | DeveloperOptions | 0.75 | RUNTIME_VALIDATION_RECOVERED | DeveloperOptions | DeveloperOptions | false |
| C BottomRandomScroll | 3 | null | 0 | null | null | FAIL_CLOSED | null | null | true |
| D WrongPage | 4 | null | 0 | null | null | FAIL_CLOSED | null | null | true |

Safety runs:

| Run | ObsSeq | TextResolverResult | FastSemanticResultCount | Candidate | Confidence | RuntimeValidationResult | FinalContainerIdentity | FinalBelief | SemanticContradiction |
|---|---|---|---|---|---|---|---|---|---|
| LowConfidence | 20 | null | 1 | DeveloperOptions | 0.33 | FAIL_CLOSED | null | null | true |
| WrongContainer | 40 | null | 1 | DeveloperOptions | 0.75 | FAIL_CLOSED | null | null | true |
| StaleObservation (old 30 → current 31) | 31 | null | 1 (rejected) | DeveloperOptions | 0.75 | FAIL_CLOSED (fusion rejected) | null | null | true |
| VectorMiss | 50 | null | 0 | null | null | FAIL_CLOSED | null | null | true |

## 4. Baseline vs Fast (Scenario B)

| Experiment | SemanticEvidence accepted | Recovery | SemanticContradiction |
|---|---|---|---|
| Baseline (NoOp provider) | 0 | No | true |
| Fast Semantic | 1 | Yes (DeveloperOptions) | false |

Across the A–D suite:

- Baseline contradictions: B, C, D = 3
- Fast contradictions: C, D = 2
- Identity recovery increase: B recovered (1 additional recovery)
- False recovery rate: 0

## 5. Safety verification

- Low-confidence candidate (< 0.6) → no recovery, fail-closed preserved.
- Stale ObservationSequence → fusion rejects, no recovery.
- Wrong container (previous verified identity mismatch) → no recovery.
- Vector miss → empty evidence → old fail-close behavior preserved.

No false positive / false recovery observed.

## 6. Performance / boundedness

- Fast provider is synchronous and completes in bounded time.
- `FastSemanticContainerIdentityTests` T3 (latency bounded) passes.
- No retry loop, no LLM, no blocking Runtime loop.

## 7. Success criteria

| # | Criterion | Result |
|---|---|---|
| 1 | At least one real scroll-container scenario: Text Resolver failure + Fast Semantic candidate + Runtime validation recovery | ✅ PASS (Scenario B) |
| 2 | No Semantic false positive | ✅ PASS (C, D, WrongContainer, VectorMiss, Stale) |
| 3 | No Resolver authority bypass | ✅ PASS |
| 4 | Runtime authority unchanged | ✅ PASS |

## 8. Test results

```text
FastSemanticContainerIdentityTests                              → 12/12 PASS
FastSemanticContainerIdentityRealWorldValidationTests           → 9/9 PASS
```

## 9. Validation evidence

```text
openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive
→ PASS

scripts/check-consistency.sh
→ ALL PASS

dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj
→ 0 warnings, 0 errors

dotnet build tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj
→ 0 warnings, 0 errors
```

## 10. Classification

```text
PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_REAL_WORLD_VALIDATION_RESULT
Classification: A. VALIDATED
```

This is deterministic Runtime-shaped validation based on the known
DeveloperOptions scroll-container scenario. Physical-device / real-UI corpus proof
can be added as a later hardware validation slice if required; it does not block
the current boundary validation.

## 11. Next step

Fast Semantic Container Identity Recovery is ready for continued graduation /
hardware corpus admission as a separate real-device validation pass.