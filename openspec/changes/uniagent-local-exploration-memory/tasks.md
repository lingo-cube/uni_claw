## 1. Implementation Human Gate

- [ ] 1.1 Obtain explicit Human approval to apply this change with owner `UniAgent-local Memory` and first buyer `UniAgent pre-Run Exploration Plan advisory`; artifact completion alone MUST NOT authorize implementation.
- [ ] 1.2 Decide whether `UNIAGENT_PRIVATE_CROSS_SESSION` is approved. If it is not approved, revise the spec and tasks to a narrower lifecycle before any implementation begins.
- [ ] 1.3 Freeze the approved UniAgent owner identity, environment-scope dimensions, retention/privacy policy, and initial immutable FactReference source allowlist.
- [ ] 1.4 Freeze a persistence-neutral implementation approach and rollback boundary without adding a public API, database requirement, Runtime dependency, or shared Memory service.

## 2. Owner-local Semantic Model

- [ ] 2.1 Add an immutable internal FactReference representation that preserves producer identity, source identity, Session/Run correlation when available, event time, environment scope, evidence kind, and integrity provenance without copying producer-owned facts.
- [ ] 2.2 Add an immutable versioned KnowledgeClaim representation that cites one or more valid FactReferences and carries explicit scope, derivation time, freshness, contradiction, supersession, and invalidation semantics.
- [ ] 2.3 Keep policy, action, GoalEvidence, completion, WorldBelief, ExplorationLedger, Runtime state, FSM commands, and Strategy mutation types out of the Memory model.
- [ ] 2.4 Add ownership/dependency guards proving the Memory model is UniAgent-local and has no RuntimeAgent, Agent, FSM, Traversal, or mutable Session-state dependency.

## 3. Admission and Derivation Boundary

- [ ] 3.1 Implement bounded admission of approved immutable FactReferences with deterministic invalid-provenance, invalid-scope, unavailable-source, and unsupported-content rejection outcomes.
- [ ] 3.2 Implement KnowledgeClaim derivation that preserves every source reference and cannot re-originate a producer fact or create an executable policy.
- [ ] 3.3 Preserve contradictory historical references without silently selecting, clamping, deleting, or rewriting their meaning.
- [ ] 3.4 Test that unprovenanced assertions, mutable Runtime snapshots, action requests, completion claims, and policy commands are rejected with no fallback record.

## 4. Retrieval, Freshness, and Invalidation

- [ ] 4.1 Implement scope-first, read-only retrieval requiring consumer, pre-Run purpose, as-of time, freshness policy, allowed knowledge category, and finite result budget.
- [ ] 4.2 Implement truthful `FOUND`, `NOT_FOUND`, `STALE_ONLY`, `CONTRADICTED`, `INVALID_SCOPE`, `SOURCE_UNAVAILABLE`, and `MEMORY_UNAVAILABLE` dispositions without scope widening or fabricated fallback knowledge.
- [ ] 4.3 Implement claim validity evaluation for scope/version mismatch, expiration, source unavailability, contradiction, supersession, and explicit invalidation.
- [ ] 4.4 If and only if task 1.2 approves `UNIAGENT_PRIVATE_CROSS_SESSION`, enforce same-UniAgent and compatible-environment scope with no global or shared fallback.
- [ ] 4.5 Test bounded retrieval, contradictory results, stale-only results, invalid scope, unavailable sources, unavailable Memory, and deterministic same-input behavior.

## 5. Pre-Run UniAgent Advisory Consumer

- [ ] 5.1 Add a UniAgent-local pre-Run consumer that can inspect retrieval candidates without automatically generating a Plan or StrategyDirective.
- [ ] 5.2 Ensure any supervisory decision influenced by Memory still produces only an already-authorized start-time contract and that the accepted StrategyDirective remains immutable for the Run.
- [ ] 5.3 Prove retrieved history cannot mark nodes Visited, prove coverage, satisfy GoalEvidence, select dynamic depth, inject routes/actions, mutate an active Run, or create a successor Run.
- [ ] 5.4 Prove the existing UniAgent/Runtime path behaves unchanged when Memory is absent, unavailable, stale-only, contradicted, or rejected.

## 6. Authority and Failure-Isolation Proofs

- [ ] 6.1 Add a test proving Memory unavailable does not affect Runtime admission, execution, verification, or terminal behavior.
- [ ] 6.2 Add a test proving stale or active KnowledgeClaims cannot bypass Runtime fresh observation, grounding, verification, Visited semantics, or GoalEvidence.
- [ ] 6.3 Add a negative authority proof that KnowledgeClaim cannot become WorldBelief, Runtime truth, Action authorization, completion evidence, or ExplorationLedger state.
- [ ] 6.4 Add architecture guards proving retrieval does not change Agent authorization, FSM lifecycle, Traversal execution, Runtime reconciliation, GoalEvidence, or terminal authority.
- [ ] 6.5 Add guards proving the change introduces no Runtime-facing Memory hook, public API, policy enforcement, Dynamic Planner, mid-Run Strategy mutation, Dynamic Depth, or Multi-Run orchestration.

## 7. Validation and Lifecycle Review

- [ ] 7.1 Run targeted Memory admission, retrieval, freshness, invalidation, plan-neutrality, failure-isolation, and authority suites.
- [ ] 7.2 Run affected UniAgent/Strategy/Runtime regression suites, architecture guards, consistency checks, and strict OpenSpec validation.
- [ ] 7.3 Independently verify the four required falsifiers: Memory unavailability is Runtime-neutral; stale knowledge cannot bypass fresh observation; KnowledgeClaim cannot become Runtime truth; retrieval cannot change authority ownership.
- [ ] 7.4 Record any approved cross-session retention/privacy evidence and verify no data or lifecycle scope exceeded the Human decision.
- [ ] 7.5 Only after all approved requirements and validation gates pass, prepare a separate graduation decision; do not infer graduation, archive eligibility, or Phase 4 authorization from checked tasks.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|---|---|
| UniAgent-local exploration Memory boundary | `openspec/changes/uniagent-local-exploration-memory/design.md` |
| Phase 2 authority and lifecycle baseline | `docs/decisions/runtime-exploration-phase2-capability-baseline-freeze.md` |
| Governing architecture and protocol | `docs/architecture/uniagent-architecture-v1-core-development-guide.md` and `docs/architecture/uniagent-protocol-v1-consolidation-design.md` |
