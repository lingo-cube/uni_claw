## Context

See `proposal.md` for motivation. Phase 2 is graduated with a Run-bound ExplorationLedger, fresh-evidence Visited semantics, immutable depth control, and Agent-owned completion. Architecture v1 assigns Memory use and Supervisory Plan ownership to UniAgent, keeps Memory independent of RuntimeAgent, and prohibits storage ownership from implying semantic ownership. Protocol v1 currently marks Memory as `NOT_PURCHASED`.

The approved design direction for this draft is UniAgent-local Memory with one buyer: pre-Run Exploration Plan advisory. This proposal does not purchase an automated UniAgent Planner. `UNIAGENT_PRIVATE_CROSS_SESSION` remains disabled unless the next Human Gate authorizes that lifecycle scope.

## Goals / Non-Goals

**Goals:**

- Establish a UniAgent owner-local semantic boundary for retaining historical references and retrieving advisory knowledge.
- Preserve the distinction between producer-owned FactReference and Memory-owned KnowledgeClaim.
- Make scope, provenance, version, freshness, contradiction, invalidation, and truthful unavailability observable contract behavior.
- Isolate all Memory failures and stale results from Runtime execution.
- Permit only pre-Run advisory influence on UniAgent supervisory decisions.

**Non-Goals:**

- Select a database, persistence technology, serialization format, DTO, public API, or deployment topology.
- Add RuntimeAgent Memory, a Runtime hook, mutable Runtime state, or a Runtime wire operation.
- Add Policy enforcement, Action blocking/generation, Dynamic Planner behavior, Dynamic Depth, mid-Run Strategy mutation, or Multi-Run orchestration.
- Change Agent, FSM, Traversal, WorldBelief, ExplorationLedger, GoalEvidence, completion, or Phase 2 graduation.
- Implement or apply this change before the Implementation Human Gate.

## Decisions

### 1. Keep Memory owner-local to UniAgent and outside Session and Runtime

The Memory boundary belongs to UniAgent because the first buyer is a UniAgent pre-Run supervisory decision. Session may carry immutable correlation and evidence references but remains neither the Memory store nor a mutable knowledge owner. Runtime has no dependency on Memory.

Alternative considered: RuntimeAgent-owned cross-Run Memory. Rejected because Runtime current-world decisions must use fresh observation/evidence and because a persistent Runtime memory would create second-truth and lifecycle pressure.

Alternative considered: an independent shared Memory Capability. Deferred because the selected buyer is singular and no cross-Agent sharing buyer is approved. A future shared capability requires a separate architecture decision.

### 2. Admit immutable references, not copied Runtime state

Memory admission consumes semantic FactReferences that identify producer-owned historical records and preserve their provenance, time, environment scope, and integrity identity. Memory does not receive or retain mutable WorldBelief, FSM state, Agent state, Traversal state, or ExplorationLedger internals.

A FactReference may become unavailable without changing the original producer's fact. Claims depending on unavailable sources become unavailable, stale, or invalid according to the specification; Memory never reconstructs missing facts.

Alternative considered: copy normalized facts into a Memory-owned event store. Rejected because it would blur fact producer ownership and create a second history authority.

### 3. Separate historical facts from derived knowledge and policy

FactReference proves only that a producer-owned historical record can be referenced. KnowledgeClaim is a versioned, contestable derivation citing one or more references. Policy is excluded: a descriptive risk claim may be retained, but an enforceable rule such as action blocking belongs to a separately authorized policy owner and contract.

This separation allows claims to be contradicted, superseded, or invalidated without rewriting source history. Storage co-location, if later chosen, does not merge semantic ownership.

### 4. Define two semantic interactions without choosing an API

The future boundary has two interactions:

1. **Admission** validates a historical reference or derived claim input and returns an explicit accepted/rejected disposition.
2. **Retrieval** validates consumer, purpose, scope, as-of time, freshness policy, category, and finite budget, then returns candidate claims plus an explicit result disposition.

These are semantic interactions, not prescribed methods, endpoints, messages, or DTOs. OpenSpec apply may select an internal representation only after the Human Gate.

### 5. Make retrieval scope-first, bounded, and contradiction-preserving

Retrieval first validates owner/consumer and requested scope, then validity/provenance, then applies a finite result budget. It never widens to global scope. Matching contradictory claims remain visible and yield a contradiction disposition unless an explicit valid supersession resolves them.

The response is candidate information, not a truth answer. `NOT_FOUND`, `STALE_ONLY`, `CONTRADICTED`, `SOURCE_UNAVAILABLE`, and `MEMORY_UNAVAILABLE` are normal truthful results.

Alternative considered: return the highest-ranked claim as the answer. Rejected because ranking cannot acquire truth authority and would hide unresolved contradictions.

### 6. Gate private cross-session retrieval separately

The semantic model can carry explicit Session, owner, and environment scopes, but cross-session access is disabled by default. If the Implementation Human Gate approves `UNIAGENT_PRIVATE_CROSS_SESSION`, retrieval may cross Session boundaries only for the same UniAgent owner and compatible environment scope.

No shared/global fallback exists. This prevents a local Memory proposal from silently becoming a multi-Agent knowledge service.

### 7. Treat knowledge freshness as advisory validity, never observation freshness

Claim validity considers scope compatibility, version, derivation time, expiration, source availability, contradiction, supersession, and explicit invalidation. This yields active, stale, contradicted, superseded, or invalidated semantics without prescribing storage representation.

Even an active claim cannot satisfy Runtime grounding, Visited, coverage, GoalEvidence, or completion. A new Run must observe and verify reality independently.

### 8. Limit Plan influence to pre-Run UniAgent consideration

Memory retrieval occurs before Run admission and returns advisory claims to UniAgent. UniAgent may use them as one input to an already-authorized supervisory decision, but Memory does not generate a Plan or StrategyDirective. Any resulting start-time directive must use the existing authorized contract and remains immutable once accepted.

There is no Memory-to-Runtime edge. Mid-Run retrieval, replanning, dynamic depth, successor Run creation, and automatic strategy generation are separate unauthorized capabilities.

### 9. Preserve failure isolation mechanically and behaviorally

Memory availability cannot be a Runtime dependency. Failure, timeout, invalid scope, stale-only results, or malformed claims are surfaced only to the pre-Run UniAgent consumer. An already active Run is unaffected because it has no Memory dependency and continues under Agent/FSM/Traversal and fresh GoalEvidence semantics.

The apply phase must prove four forbidden effects:

| Forbidden effect | Required proof |
|---|---|
| Memory unavailable affects Runtime execution | Dependency isolation plus execution-neutrality scenario |
| Stale knowledge bypasses fresh observation | Freshness scenario showing new Run still requires fresh evidence |
| KnowledgeClaim becomes Runtime truth | Type/dependency and negative authority proof |
| Retrieval changes authority ownership | Architecture guard across Agent/FSM/Traversal/Ledger/GoalEvidence boundaries |

## Risks / Trade-offs

- **[Cross-session state becomes implicitly global]** → Keep cross-session disabled until the Human Gate; require exact owner and environment scope with no fallback.
- **[KnowledgeClaim is mistaken for current truth]** → Preserve claim provenance/status in every retrieval result and require fresh Runtime evidence for every Run.
- **[Memory becomes a hidden Planner]** → Restrict output to candidate claims and prohibit Plan, StrategyDirective, route, action, or depth output.
- **[Historical contradictions are hidden by ranking]** → Preserve contradiction states and return them explicitly.
- **[Unbounded retention or retrieval creates cost/privacy pressure]** → Require bounded retrieval now; retention/privacy policy must be approved before cross-session apply.
- **[Storage implementation captures semantic authority]** → Keep semantic ownership independent of persistence placement and verify no Runtime dependency.

## Migration Plan

There is no migration in the proposal phase. If apply is later approved, delivery must be additive and disabled by default until the selected owner scope is configured. Existing Runtime and UniAgent execution paths remain valid when Memory is absent. Rollback is removal or disablement of the owner-local Memory composition without Runtime, Phase 2 evidence, or wire migration.

## Open Questions

- Which explicit environment identity dimensions are required before `UNIAGENT_PRIVATE_CROSS_SESSION` can be enabled?
- What retention and privacy policy must accompany approved cross-session history?
- Which existing immutable producer references form the initial admission allowlist?

These questions affect configuration and implementation detail but do not relax the specified authority or failure boundaries.
