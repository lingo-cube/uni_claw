# Runtime Exploration Phase 2 Capability Baseline

DocumentType: `CAPABILITY_BASELINE_PROJECTION`  
Status: `GRADUATED / ACTIVE / NOT_ARCHIVED`  
Authority: projection of the approved graduation decision and approved Specs; this document adds no authority.  
Approved implementation base: `e2d8dd44214632f50777992d58fb4fe318ad45f0`

## Capability Summary

For an accepted Strategy Run, Phase 2 provides an immutable, Agent-bound exploration interpretation and an on-demand, read-only Exploration Ledger projection. The ledger derives identity-level `Discovered`, `Visited`, `Pending`, `Unresolved`, and overlapping `UnknownFrontier` evidence from accepted branch, revisit, boundary, observation-sequence, and optional structural-progress evidence.

The closed rule vocabulary is `ExpandContainer` and `RecordOnly`. `Visited` means the applied rule, its evidence requirement, and its rule-specific completion condition are all satisfied: fresh accepted observation for `RecordOnly`, or verified subtree return/verified boundary disposition for `ExpandContainer`. Authorization, dispatch, or click alone is not `Visited`.

Depth is Run-immutable and has no dynamic adjustment: depth 0 records the root scope; depth 1 expands root containers and records direct-child scope inventory; depth N≥2 uses the approved exhaustive cutoff or bounded-record boundary semantics. Exhaustive overflow remains fail-closed; bounded-record boundaries are record-only and annotated as unknown frontier.

Structural-progress facts contribute only validated canonical correlation/digest material. They cannot alter counts, assert exhaustion, create GoalEvidence, or complete a Run.

## Ownership Boundary

- Agent owns Run identity, accepted Strategy context, grounding, authorization, evidence collection, GoalEvidence evaluation, and terminal outcome.
- FSM owns protocol transition authority.
- Traversal owns concrete execution.
- Environment owns device/world effects.
- ExplorationLedger and its compiler are immutable/read-only evidence projections. They own no mutable state, source evidence, action, target, authorization, recovery, FSM, completion, or scenario authority.

## Evidence Boundary

`RecordOnly` requires a fresh accepted semantic observation and performs no dispatch or state mutation. `ExpandContainer` requires existing Agent authorization and verified subtree return or verified boundary disposition. A classification failure is unresolved, with zero inferred rule, authorization, and dispatch. Unknown frontier is an overlapping annotation on record-only visited identities, not a new primary disposition. Completion still requires Agent-owned GoalEvidence and the existing FSM authorization path.

## Non-goals

- No Strategy wire/schema, public protocol, Run lifecycle, or public request shape change.
- No Phase 3 Exploration Memory, Safety Knowledge, or Knowledge owner.
- No Phase 4 dynamic depth, mid-Run Strategy mutation, Planner, or automatic strategy generation.
- No new state system, evidence owner, completion fact, scenario knowledge, or Runtime hook.

## Future Extension Constraints

Any later Memory, dynamic-depth, Planner, owner, contract, lifecycle, scenario-knowledge, or authority change requires its existing Architecture/Human Gate and an approved OpenSpec. This baseline must not be used to infer a Memory schema, choose an owner, or treat the ledger as completion truth.

## Reality Analysis

### Expected Reality

The approved graduation decision and Specs describe the exact Phase 2 capability above, with Agent/FSM/Traversal ownership unchanged and no Phase 3 authorization.

### Observed Reality

The final graduation decision records Phase 2 as graduated and active, with real-path evidence for admission, ledger accounting, unresolved classification, depth 0/1/N, bounded frontier, structural correlation, authority guards, wire compatibility, and legacy isolation. It explicitly records Phase 3 Exploration Memory and Phase 4 dynamic depth as not authorized.

### Reality Gap

The capability implementation and graduation evidence align with the approved Phase 2 baseline. A separate roadmap consistency gap exists: the roadmap's depth examples use a different numbering convention from the graduated D1 table. This baseline follows the higher-authority graduated decision and approved Specs; it does not reinterpret the roadmap.

### Evidence Reference

- `docs/decisions/runtime-exploration-phase2-final-graduation-decision.md`, §§1–6.
- `openspec/changes/runtime-exploration-ledger-and-depth-control/specs/runtime-exploration-ledger-and-depth-control/spec.md`, ledger, visited, depth, completion, and neutrality requirements.
- `openspec/changes/runtime-exploration-semantic-admission-remediation/specs/runtime-exploration-semantic-admission-remediation/spec.md`, admission D1 table, provenance, identity accounting, and structural correlation requirements.
- `docs/system/constitution/runtime-architecture-contract.md`, I-1–I-14.

### First Divergence

No implementation divergence is asserted here. The first documented source-level inconsistency is the roadmap depth example: `docs/decisions/runtime-exploration-roadmap.md` §4 says `Depth = 1` is “Root only” and `Depth = 2` is “Root + children”, while the graduated D1 table defines `depth 0` as root record-only and `depth 1` as root expansion plus direct-child record-only.

### Owner

Baseline ownership remains with the existing approved Agent/FSM/Traversal boundaries. The roadmap inconsistency requires a human architecture/governance decision; this worker does not select an owner or revise a source.
