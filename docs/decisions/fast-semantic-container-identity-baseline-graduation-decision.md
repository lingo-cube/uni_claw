# Fast Semantic Container Identity Baseline — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-fast-semantic-container-identity-baseline/`
> Authority: Graduated Semantic Evidence Fusion (`docs/decisions/semantic-evidence-fusion-graduation-review.md`) and the Fast Semantic Container Identity Baseline decision (`docs/decisions/fast-semantic-container-identity-baseline.md`) remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** per proposal.md — the base is `PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_GRADUATED`, and the next target is **Scrolled Container Identity Drift** (page title leaves the viewport → `CreateMultiPageResolver` returns null → container continuity check fails → Runtime raises a false `SemanticContradiction`); this change freezes the **Fast Semantic Container Identity Recovery** architecture — a bounded Fast Semantic Evidence Provider supplying `ContainerIdentity` evidence into the graduated `SemanticEvidenceFusion` seam, without replacing the resolver or changing Agent/Vision/Belief authority.

This receipt claims only that:

1. `FastSemanticContainerIdentityProvider` is defined under `Capabilities/Perception/Semantic/Fast`, consuming only `ObservationContext` (Current Observation, Visible Elements, Container History, Previous Verified Identity) and returning `SemanticEvidence` of kind `ContainerIdentity`; it does not accept Goal, Action, Expected State, or Planner Context, and is synchronous, bounded, and returns empty evidence on failure (proposal.md; design.md §2; specs/fast-semantic-container-identity-baseline/spec.md R1);
2. `IVectorSemanticIndex` + `ContainerSemanticQuery` + `SemanticCandidate` are defined as read-only semantic pattern retrieval; the Vector Index returns candidates, not Facts, and does not decide (proposal.md; design.md §3; spec R2);
3. the Fast Semantic flow is frozen — Observation → Feature Extraction → Vector Retrieval → SemanticEvidence → SemanticEvidenceFusion → Runtime Validation — with bounded latency, no retry loop, no reasoning, and failure = empty evidence (proposal.md; design.md §4; spec R3);
4. Vector Memory is read-only in this baseline — no Runtime write, no auto-learning; the future write pipeline (Trace → Post Processing → Semantic Pattern → Validation → Vector Memory) is deferred, not implemented (proposal.md; design.md §5; spec R4);
5. Container Identity Validation stays Runtime-owned — the Text Resolver remains, Semantic Evidence Candidate is an additional input, Runtime Validation combines previous verified identity, container history, observation continuity, and semantic evidence before deciding whether to recover Container Identity, and Semantic does NOT directly set `CurrentContainer` (proposal.md; design.md §6; spec R5);
6. the Fast/Slow boundary is explicit — Fast Semantic is synchronous bounded vector retrieval; Slow Semantic is a future async LLM checkpoint that this change does not implement (proposal.md; design.md §7; spec R6);
7. no Agent, Goal, Action, Planner, L1 Assistance, DSH, Vision Service, CreateMultiPageResolver, ContainerIdentityResolver, or Belief Authority changes are made (proposal.md Out of scope/forbidden; design.md §9; spec R7);
8. Semantic confidence is an evidence weight, not Truth — confidence above a threshold does not equal Truth, and Runtime decides Belief (design.md §8 T10; spec R8);
9. a test matrix T1–T10 is defined for the APPLY gate (proposal.md "Defines tests T1–T10 for the APPLY gate"; design.md §8).

No claim is made for: Vector Database, Embedding, LLM Semantic, Real Semantic Provider, Fast Semantic Provider production code, Container Resolver replacement, Runtime Belief modification (proposal.md Non-goals), nor for Vector write path / auto-learning / Slow Semantic implementation (proposal.md Out of scope/forbidden; design.md §9; spec R6/R4).

## 2. Validation evidence

- tasks.md "Slices (this gate)" records Slice 0 (OpenSpec scaffolding: proposal/design/spec/tasks/README/.openspec.yaml) through Slice 10 (validation) all complete `[x]`, including Slice 1 (Decision document `docs/decisions/fast-semantic-container-identity-baseline.md`) and Slice 9 (strict boundary freeze: no Agent / Goal / Action / Planner / L1 / DSH / Vision / Resolver / Belief Authority change).
- tasks.md "Validation record" records: `openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive` — **PASS**; `scripts/check-consistency.sh` — **ALL PASS**; `dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj` — **0 warnings, 0 errors**; `dotnet build tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj` — **0 warnings, 0 errors**; `dotnet test --filter FastSemanticContainerIdentity` — **12/12 PASS**.
- proposal.md "Validation" and design.md §10 prescribe `openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive` and `scripts/check-consistency.sh` as the gate commands (results as recorded in tasks.md above).
- `docs/decisions/fast-semantic-container-identity-baseline.md` §12 records the same two validation commands, and §13 records the gate result: `PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_RESULT`, Decision `FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_FROZEN`, and `NEXT_GATE = PROJECT_LEADER_APPLY_FAST_SEMANTIC_CONTAINER_IDENTITY`.
- design.md §8 defines the APPLY test matrix T1–T10: vector hit returns SemanticEvidence; vector miss returns empty evidence; fast semantic latency bounded; semantic candidate does not become Fact; old container identity requires Runtime validation; no Vector provider keeps Runtime unchanged; Agent unchanged; Resolver unchanged; scrolled container can receive candidate evidence; semantic confidence does not equal Truth.
- specifications/delta spec (`openspec/changes/fast-semantic-container-identity-baseline/specs/fast-semantic-container-identity-baseline/spec.md`) defines 8 ADDED requirements with 15 scenarios (provider evidence + empty-failure + forbidden inputs; vector index read-only retrieval + not-decide; bounded flow + no retry/reasoning; read-only Vector Memory + deferred future pipeline; Runtime-owned validation + scrolled-container recovery; Fast/Slow boundary; Agent/Resolver/Belief unchanged; confidence-as-evidence-weight), each scenario anchoring an APPLY test tag (T1–T10, "T6-adjacent").

## 3. Scenario receipts and falsifiers

tasks.md records a "Falsifier mapping (design-level)" F1–F10, each marked complete `[x]`; the recorded result is the completion status of the mapping, not a failing proof:

| Falsifier (tasks.md) | Recorded result |
|---|---|
| F1 — Fast Semantic does not bypass Runtime | mapped, marked complete |
| F2 — Fast Semantic does not directly modify Belief | mapped, marked complete |
| F3 — Fast Semantic does not execute Action | mapped, marked complete |
| F4 — Confidence does not equal Truth | mapped, marked complete |
| F5 — Vector miss returns empty evidence | mapped, marked complete |
| F6 — No Vector provider keeps Runtime unchanged | mapped, marked complete |
| F7 — No Agent replacement | mapped, marked complete |
| F8 — No Resolver replacement | mapped, marked complete |
| F9 — No Vision responsibility expansion | mapped, marked complete |
| F10 — No L2 planning capability | mapped, marked complete |

Additionally, the change's negative/forbidden requirements are defined in `openspec/changes/fast-semantic-container-identity-baseline/specs/fast-semantic-container-identity-baseline/spec.md` (ADDED requirements verbatim-by-reference): the provider MUST NOT accept Goal, Action, Expected State, or Planner Context (scenario "forbidden inputs are not accepted"); Vector Index MUST return a candidate, not a Fact and not a decision (scenario "vector index does not decide", T4); the flow MUST have no retry loop and no reasoning (scenario "no retry loop or reasoning"); Runtime MUST NOT write Vector and no auto-learning MUST be created (scenario "no runtime vector write"); Semantic MUST NOT directly set CurrentContainer (scenarios "scrolled container receives candidate evidence" T9 and "old container identity requires runtime validation" T5); Slow Semantic MUST NOT be implemented (scenario "slow semantic not implemented", T6-adjacent); Agent and Resolver behavior MUST be unchanged (scenarios "agent unchanged" T7, "resolver unchanged" T8); confidence MUST NOT equal Truth (scenario "confidence not truth", T10).

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- APPLY gate `PROJECT_LEADER_APPLY_FAST_SEMANTIC_CONTAINER_IDENTITY` — recorded as `NEXT_GATE` by proposal.md "Required output" and `docs/decisions/fast-semantic-container-identity-baseline.md` §13 (see WARNINGS: tasks.md also records APPLY-plan tasks A1–A5 and a 12/12 `FastSemanticContainerIdentity` test run as complete, while proposal.md and the change README record this change as baseline-only with APPLY pending).
- Vector Database, Embedding, LLM Semantic, Real Semantic Provider, Fast Semantic Provider production code (proposal.md Non-goals).
- Slow Semantic implementation — future async LLM checkpoint (proposal.md Non-goals; design.md §7; spec R6).
- Vector write path and auto-learning, including the future Vector Memory pipeline Trace → Post Processing → Semantic Pattern → Validation → Vector Memory (proposal.md Out of scope/forbidden; design.md §5; spec R4).
- Container Resolver replacement (CreateMultiPageResolver / ContainerIdentityResolver) and Runtime Belief modification (proposal.md Non-goals; design.md §9).
- Agent / Vision / Belief Authority changes — explicitly forbidden for any future APPLY too (proposal.md Out of scope/forbidden; design.md §9).

## 5. Final conclusion

**GRADUATED.** The frozen Fast Semantic Container Identity Recovery baseline — provider and vector-index abstractions, Fast flow, read-only Vector Memory, Runtime-owned Container Identity Validation, Fast/Slow boundary, no-authority-change constraints, and the T1–T10 APPLY test matrix — is human-authorized and backed by the evidence recorded in the change's own files (tasks.md: `openspec validate` PASS, `scripts/check-consistency.sh` ALL PASS, Runtime and test project builds 0 warnings / 0 errors, `dotnet test --filter FastSemanticContainerIdentity` 12/12 PASS; pairing decision `FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_FROZEN` in `docs/decisions/fast-semantic-container-identity-baseline.md`). Archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.