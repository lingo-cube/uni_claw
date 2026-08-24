## Why

Runtime production paths currently embed Android Settings interpretation across
Environment adapters, World analysis, Agent execution helpers, PhysicalHost
composition, and Semantic provider infrastructure. This prevents Runtime
scenario neutrality and also lets the optional ADB UI hierarchy source appear
equivalent to the primary visual perception path even though UIAutomator dump
can be unavailable, incomplete, stale, or device-dependent.

## What Changes

- Record an explicit human apply gate because the extraction requires a new
  versioned Runtime consumer contract (`Semantic Evidence Protocol V2`). This
  is an additive authority-preserving contract boundary, but it is still a
  contract-boundary change and MUST be approved before production edits begin.

- Introduce an external Scenario Knowledge Package and Semantic Capability
  Binding boundary that emits typed candidate evidence into generic Runtime
  admission, fusion, and reconciliation.
- Introduce a versioned Semantic Evidence Protocol V2 with manifest-resolved
  symbols and typed identity, affordance, and relation candidates; prohibit
  free scenario strings, selectors, routes, actions, completion flags, FSM
  commands, and callbacks into Agent.
- Separate coverage requirements from observation evidence so an external
  capability cannot acquire completion authority.
- Classify screenshot/Vision perception as the primary perception path and ADB
  UI hierarchy dump as optional auxiliary evidence only.
- Require ADB absence or capture failure to be represented as auxiliary-source
  unavailability rather than equivalent primary-perception failure.
- Require Runtime fusion to preserve source, freshness, frame alignment, and
  contradiction provenance. ADB-only evidence must never authorize an action,
  establish verified Container identity, prove coverage, or satisfy
  GoalEvidence.
- Extract Settings classifiers, page relations, Preference-row rules, locale
  labels, scenario corpora, launch assumptions, and validation baselines from
  generic Runtime production paths.
- Preserve Agent, FSM, Traversal, GoalEvidence, Recovery, OpenWorld DFS,
  RuntimeAgent Phase 1-4, and Strategy Contract ownership and behavior.
- Replace the remaining structured-element-only DFS grounding seam with a
  source-neutral canonical occurrence contract. Fresh primary Vision
  occurrences MUST be independently groundable; auxiliary ADB occurrences MAY
  corroborate them but MUST NOT be a grounding prerequisite or be promoted to
  primary truth.
- **BREAKING (internal):** raw ADB-derived structured elements and free-string
  semantic candidates will no longer enter Runtime as unqualified first-class
  semantic evidence. Producers and consumers must use source-qualified typed
  contracts.

## Gate Classification

- `AuthorityDelta`: `NONE`
- `ArchitectureInvariantDelta`: `NONE`
- `ContractDelta`: `SEMANTIC_EVIDENCE_PROTOCOL_V2_REQUIRED`
- `ApplyAuthorization`: `HUMAN_APPROVAL_REQUIRED`

The current inventory found no external semantic producer calling Agent,
Traversal, FSM, Recovery, GoalEvidence, or Run-start authority. The gate exists
because V1 cannot express source-qualified typed affordance and relation
evidence without retaining free scenario strings; it is not evidence of an
authority transfer.

## Capabilities

### New Capabilities

- `runtime-external-semantic-capability-boundary`: Defines external scenario
  knowledge ownership, Semantic Capability Binding, Semantic Evidence Protocol
  V2, evidence admission, primary/auxiliary perception source tiers, and the
  authority-preserving handoff to generic Runtime and Agent.

### Modified Capabilities

- `environment`: Qualifies ADB UI hierarchy output as optional auxiliary
  observation evidence, defines non-authoritative failure behavior, and forbids
  ADB-only evidence from becoming action, identity, coverage, or completion
  authority.

## Impact

- `src/UniClaw.Runtime/Capabilities/Perception/Semantic/`: provider contract,
  evidence admission, Fast provider and corpus placement.
- `src/UniClaw.Runtime/World/`: generic evidence reduction without Settings or
  Android selector knowledge, plus source-neutral canonical occurrence
  normalization.
- `src/UniClaw.Runtime/Agent/`: consumption of admitted typed evidence while
  preserving all execution and completion authority.
- `src/UniClaw.Runtime/Traversal/`: typed authorized control binding without
  free provider labels.
- `src/UniClaw.Runtime.Adapters/Device/`: raw ADB acquisition and explicitly
  auxiliary source-qualified evidence only.
- `src/UniClaw.Runtime.PhysicalHost/`: capability discovery/composition without
  Settings defaults; scenario preparation moves to Harness.
- External semantic capability/package projects and Semantic evaluation assets
  introduced by the later approved implementation.
- Architecture, semantic, SETTINGS-TREE-01, OpenWorld, Strategy, and authority
  regression tests.

## Vision-first grounding revision

The human-approved continuation on 2026-08-22 resolves the implementation
pressure discovered during V2 apply: `InteractionAffordanceAnalyzer` and
`Agent.OpenWorld` still grounded through `Observation.StructuredElements`,
which made optional auxiliary hierarchy evidence a de facto prerequisite.

This revision authorizes an internal evidence-representation change only:

```text
Observation source occurrence
  -> SourceGroundingNormalizer
  -> CanonicalObservationOccurrence
  -> admitted typed semantic evidence
  -> Agent-owned DFS authorization
```

`AuthorityDelta` remains `NONE`. Agent continues to own discovery acceptance,
authorization, ordering, verification, GoalEvidence, and terminal outcome.
