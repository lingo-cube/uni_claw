## Why

Runtime production XML documentation currently names a concrete Android Settings
scenario in three otherwise generic Semantic Perception contracts. The literals
do not affect execution, but they violate the frozen scenario-neutral Runtime
boundary and fail the existing architecture guard, blocking independent Strategy
Contract verification.

## What Changes

- Remove the concrete scenario examples from `SemanticEvidence`,
  `SemanticCandidate`, and `SemanticCorpus` XML documentation.
- Preserve every public type, member, constructor, namespace, and runtime
  behavior unchanged.
- Audit `src/UniClaw.Runtime` for scenario-specific literals and stop if any
  executable scenario dependency is found.
- Keep scenario fixtures in tests or externally supplied knowledge assets; do
  not relocate them into another Runtime layer or rename them generically.

## Capabilities

### New Capabilities

None. This is a source-boundary cleanup governed by existing architecture
invariants and guards.

### Modified Capabilities

None. No semantic capability requirement or behavior changes. This change opts
out of delta specs through `skip_specs: true`.

## Impact

- Documentation-only edits in three files under
  `src/UniClaw.Runtime/Capabilities/Perception/Semantic/`.
- No API, binary shape, authority, lifecycle, Agent, Traversal, FSM,
  GoalEvidence, Strategy Contract, or execution behavior change.
- Validation covers the Runtime scenario-knowledge guard, Semantic and Strategy
  tests, deterministic Runtime regressions, consistency, and strict OpenSpec
  validation.

## Superseded

This change is **superseded by**
[`runtime-external-semantic-capability-boundary`](../runtime-external-semantic-capability-boundary/):
the boundary change goes beyond documentation cleanup — it migrates scenario
interpretation into external Semantic Capability packages and enforces Runtime
scenario neutrality with executable guards
(`ExternalSemanticCapabilityBoundaryGuardTests.RuntimeProductionSource_IsScenarioNeutral`).
This marker records the supersession fact only; it is not an archive action and
does not by itself constitute graduation of either change.
