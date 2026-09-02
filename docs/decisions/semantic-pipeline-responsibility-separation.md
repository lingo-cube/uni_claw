# Semantic Fast Perception Pipeline — Responsibility Separation

> Status: SEPARATED | Decision: `SEMANTIC_PIPELINE_RESPONSIBILITIES_SEPARATED` | Date: 2026-08-30
> Gate: `PROJECT_LEADER_SEMANTIC_PIPELINE_RESPONSIBILITY_SEPARATION`
> Basis: `PROJECT_LEADER_SEMANTIC_PERCEPTION_PIPELINE_BOUNDARY_AND_SAFETY_REVIEW_RESULT`
> (`SEMANTIC_PIPELINE_RESPONSIBILITY_MIX_FOUND`)
> Scope: limited responsibility separation inside `UniClaw.Semantic.Infrastructure`
> only. NO Runtime / Agent / SemanticEvidence / ISemanticProvider / fusion /
> resolver change. NO tuning, NO held-out repair, NO new model / backend /
> Ray / HuggingFace / Slow Semantic. Qualification status unchanged.

## Decision

```
PROJECT_LEADER_SEMANTIC_PIPELINE_RESPONSIBILITY_SEPARATION_RESULT

Decision: SEMANTIC_PIPELINE_RESPONSIBILITIES_SEPARATED

NEXT_GATE: PROJECT_LEADER_SEMANTIC_SAFETY_HARDENING_APPLY
```

## 1. Before responsibility map

| Concern | Held by (before) | Problem |
|---|---|---|
| Deterministic overlap matching | `InMemoryVectorSemanticIndex` (named like a vector index) | not a vector index |
| Acceptance threshold | `InMemoryVectorSemanticIndex.Retrieve` + `InMemoryVectorIndexOptions.MatchThreshold` | policy inside retrieval + options |
| Prototype/pattern data | `InMemoryVectorIndexOptions.Patterns` + the index itself | prototype inside retrieval |
| Structural / conflict / min-evidence rules | only the Python embedding benchmark (not the C# path) | rules had no C# home |
| Embedding model naming | `SemanticVectorBackend.Bge` ("BGE embedding backend") | embedding model × retrieval backend concept mix |
| Config identities | `SemanticOptions { FastSemanticProviderEnabled, VectorBackend, InMemoryIndex, Benchmark, Evaluation }` | no embedding/prototype/policy/pipeline identity |
| Feature extraction | `FastSemanticFeatureExtractor` (static, pure) | fine — kept, made an explicit contract |
| Provider | `FastSemanticContainerIdentityProvider` (index → evidence inline) | threshold baked via index |

## 2. After responsibility map

```
FastSemanticContainerIdentityProvider          (pipeline assembler; ISemanticProvider)
  → IContainerSemanticFeatureExtractor         Feature: Observation → ContainerSemanticQuery
  → IEmbeddingProvider (+EmbeddingVector)      Embedding: representation → vector (+ model identity)
  → IContainerIdentityPrototypeStore           Prototype: Identity → prototypes (owner of identity meaning)
  → DeterministicSemanticMatcher | IVectorSemanticIndex   Retrieval: nearest candidates (no acceptance)
  → IContainerIdentityCandidatePolicy          Policy: context → Accept / ABSTAIN
  → IContainerIdentityEvidenceBuilder          Evidence: accepted candidate → SemanticEvidence
```

`ExactInMemoryVectorIndex` = real vector retrieval (cosine, no threshold).
`SemanticVectorIndexRegistry` / `ISemanticVectorIndexFactory` = RETRIEVAL backends only.
`SemanticVectorBackend` values = retrieval backends only (BGE removed).
`CandidatePolicies.LegacyReference()` = exact legacy profile semantics; `CandidatePolicies.V1()` = separated V1 rules.

## 3. Feature boundary

`IContainerSemanticFeatureExtractor` — one duty: Observation → `ContainerSemanticQuery`
(unchanged arithmetic). No embedding / prototype lookup / threshold / acceptance /
Runtime belief. `FastSemanticFeatureExtractor` now implements it (static → instance).

## 4. Embedding boundary

`IEmbeddingProvider`(Embed) → `EmbeddingVector` { values, dimension, `EmbeddingModelIdentity`
{ ModelId, Revision, Dimension, Runtime, Precision } }. `DeterministicSemanticEmbeddingProvider`
= deterministic/test implementation (stable FNV-1a token hashing, no threshold/prototype/
acceptance/vector-DB). BGE-small enters LATER via BgeSmallEmbeddingProvider (not wired here).

## 5. Prototype boundary

`ContainerIdentityPrototype` { Identity, PrototypeId, representation, Version, ProfileRef, optional Vector }.
`IContainerIdentityPrototypeStore` = sole owner of known identity representations.
Vector indexes REFERENCE prototypes (store injected); they never own canonical meaning.
Legacy `SemanticPattern` preserved as a seed representation (`Store.FromSemanticPatterns`).

## 6. Retrieval boundary

`IVectorSemanticIndex.Retrieve(EmbeddingVector) → IReadOnlyList<SemanticCandidate>` — ranked
nearest candidates ONLY. No acceptance threshold, no policy, no evidence, no structural/
conflict rule (each is externally applied). `ExactInMemoryVectorIndex` implements it;
`DeterministicSemanticMatcher` hones the legacy overlap arithmetic as the reference/test
retriever (same scoring, no threshold).

## 7. Candidate Policy boundary

`IContainerIdentityCandidatePolicy.Decide(CandidateEvaluationContext) → CandidatePolicyResult`.
`CandidateEvaluationContext` carries ranked candidates + matched prototypes + previous
identity + element types + evidence sufficiency. Output: accepted candidate or `IsAbstain`
(ABSTAIN = normal success path). Rules expressed = existing V1 semantics only
(threshold, structural compatibility, previous-identity conflict rejection, minimum-evidence
abstention). NO new mechanisms (margin / multi-prototype / sufficiency) — those belong to
`SEMANTIC_SAFETY_HARDENING_APPLY`. Policy never forms Runtime belief / world state.

## 8. Configuration identity

`SemanticOptions` now expresses each identity independently:

| Identity | Key |
|---|---|
| Retrieval backend | `VectorBackend` + `Retrieval` (metric/topK) |
| Embedding | `Embedding` (provider + `EmbeddingModelIdentity`) |
| Prototype profile | `Prototype.ProfileVersion` |
| Candidate policy | `Policy` (profile version + V1 flags) |
| Pipeline profile | `PipelineProfileId` |

`SemanticVectorBackend.Bge` deleted (T8 proves no BGE backend concept).
`SemanticVectorIndexRegistry` = retrieval only; embedding providers are not registrable.

## 9. Profile identity

`SemanticPerceptionProfile` record binds component identities independently;
`SemanticPerceptionProfiles.SeparatedV1` =
`SEMANTIC_CONTAINER_IDENTITY_PROFILE_V1` { feature v1-text-plus-type, embedding
deterministic-v1, prototypes v1-canonical-signatures, retrieval DeterministicMatcher,
metric overlap, policy v1 }. Qualification status is UNCHANGED — Profile V1 remains
SAFETY_NOT_QUALIFIED on held-out (RED evidence preserved).

## 10. Compatibility proof

- `Compat_ThresholdOnlySeparatedReproducesLegacyExactly`: separated pipeline with the
  legacy (threshold-only) policy == legacy reference on ALL 35 tuning cases, 0 divergences.
- `Compat_SeparatedV1DocumentsRuleDifferences`: separated V1 policy (rules now expressed)
  differs on a PINNED set of 12 tuning case ids, all attributable to fail-closed conflict
  rejection / structural filtering on generic type-only ties — documented, not tuned,
  not held-out-driven.
- held-out InMemory side uses the same reference-matcher arithmetic as the committed
  report (identical numbers); BGE side untouched.
- Latency (tuning corpus loops): legacy p50 0.0028 / p95 0.0050 ms vs separated
  p50 0.0032 / p95 0.0054 ms — no significant overhead (µs-level, within noise;
  the separated policy adds only constant-order filtering).
- Full suite: 52 PASS / 3 RED.

## 11. Held-out qualification remains RED

`T4_HardNegativeRejection` / `T6_InsufficientEvidenceAbstains` / `T8_InMemoryAndBge...`
stay RED with the unchanged evidence: BGE frozen profile FR 0.4167, HNR 0.5833.
Not repaired, not re-announced — required behavior of this gate.

## Exit conditions

| # | Condition | Result |
|---|---|---|
| 1 | BGE no longer a Vector Backend concept | ✅ `SemanticVectorBackend.Bge` removed (T8) |
| 2 | Embedding ↔ Retrieval independent | ✅ provider/index contracts independent (T1/T6) |
| 3 | Prototype not owned by vector index | ✅ store is owner; index references (T3) |
| 4 | Threshold/policy not owned by vector index | ✅ no threshold in index (T2) |
| 5 | Candidate policy independently testable | ✅ (T4) |
| 6 | Config identifies Embedding/Retrieval/Prototype/Policy/Pipeline Profile | ✅ (T7) |
| 7 | Runtime-facing contracts unchanged | ✅ zero UniClaw.Runtime changes; ISemanticProvider unchanged (T6) |
| 8 | Legacy V1 behavior reproducible | ✅ exact on tuning; differences documented+pinned |
| 9 | Held-out failures not silently fixed | ✅ T4/T6/T8 RED, identical evidence |
| 10 | No Ray / HuggingFace / new embedding / new backend | ✅ none introduced |

## Verification

- `dotnet build src/UniClaw.Runtime.sln` — 0 errors
- `dotnet test tests/Semantic/Semantic.Tests.csproj` — 52 PASS / 3 RED (T4/T6/T8; required)
- `openspec validate --changes --strict --no-interactive` — run in-gate
- `scripts/check-consistency.sh` — run in-gate

## Deliverables

- `src/UniClaw.Semantic.Infrastructure/Fast/`: IContainerSemanticFeatureExtractor,
  IEmbeddingProvider, EmbeddingVector, EmbeddingModelIdentity, DeterministicSemanticEmbeddingProvider,
  IContainerIdentityPrototypeStore + ContainerIdentityPrototypeStore + ContainerIdentityPrototype,
  DeterministicSemanticMatcher, ExactInMemoryVectorIndex, IContainerIdentityCandidatePolicy +
  ContainerIdentityCandidatePolicy + CandidateEvaluationContext/Result/Options + CandidatePolicies,
  IContainerIdentityEvidenceBuilder + ContainerIdentityEvidenceBuilder,
  SemanticPerceptionProfile + SemanticPerceptionProfiles, FastSemanticPipelineFactory,
  refactored FastSemanticContainerIdentityProvider, refactored FastSemanticFeatureExtractor,
  refined IVectorSemanticIndex; deleted InMemoryVectorSemanticIndex.
- `src/UniClaw.Semantic.Infrastructure/Retrieval/`: backend constants retrieval-only (no BGE),
  registry retrieval-only, slim InMemoryVectorIndexOptions (no patterns/threshold).
- `src/UniClaw.Semantic.Infrastructure/Configuration/SemanticOptions.cs`: independent identities.
- `tests/Semantic/.../SemanticResponsibilitySeparationTests.cs`: T1–T8 + 2 compat proofs + latency.
- Updated test fixtures (legacy-provider constructions now explicit store+policy).