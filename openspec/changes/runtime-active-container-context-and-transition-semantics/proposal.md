## Why

Runtime already owns the required Container execution state, but the current location belief, active execution Container, method-local parent stack, ancestry set, progress evidence, and string-shaped transition outcomes are updated through separate seams. The r5 incident exposes the resulting semantic gap: a fresh observation grounded `SettingsRoot` while the accepted WorldBelief and active execution state remained on `Display` because the location-change branch returned before reconciliation.

This change purchases state consolidation and typed transition semantics, not a new Container management or navigation system. The hard acceptance boundary is that the design must replace existing mutable state, keep `NET_NEW_MUTABLE_TRUTH = 0`, and preserve normal-path control flow.

## What Changes

- Introduce one Agent-owned, Run-local, non-persistent `ActiveContainerContext` containing only the active execution Container and its ordered active ancestor path.
- Keep `CurrentObservedLocation` in the existing fresh `WorldBelief`; do not copy it into the execution context.
- Replace `_activeContainer`, the method-local parent stack, and the separately maintained current-ancestry set with the context and a derived ancestry view; retain the run-local visited evidence separately because historical coverage is not current execution truth.
- Introduce an immutable `ContainerTransition` result with a closed transition vocabulary, evidence/completeness references, and a closed disposition vocabulary. Do not add an Agent `_latestTransition` field.
- Define one validation-before-commit reconciliation seam that atomically accepts fresh WorldBelief, permitted execution-context changes, existing progress updates, and an immutable transition event, or commits none of them.
- Require honest r5 representation: observed `SettingsRoot`, active execution obligation `Display`, `PREMATURE_RETURN_TO_ACTIVE_PARENT`, incomplete Display subtree, no automatic completion, recovery, or re-entry.
- Add a read-only projection for observed location, active execution Container, active ancestor path, latest transition event, existing completeness reference, EvidenceRef, and AssetRef linkage.
- Preserve all existing action authorization, completion, branch-progress, verified-return, external-boundary, GoalEvidence, Recovery, and normal-path semantics.
- Keep implementation and Apply explicitly unauthorized until the final Human Gate accepts the state-replacement proof.

## Capabilities

### New Capabilities

- `runtime-active-container-context`: Defines the minimal Agent-owned Run-local execution context, strict observed-versus-execution separation, state replacement rules, and non-graph active ancestor path.
- `runtime-container-transition-reconciliation`: Defines immutable typed transition classification, the closed vocabulary, atomic prepare/commit behavior, fail-closed rollback, completeness references, and the seven buyer cases.
- `runtime-container-context-read-model`: Defines the authority-free Runtime/DriverHost/Debug Toolchain projection and EvidenceRef/AssetRef correlation contract.

### Modified Capabilities

- `open-world-traversal-identity-safety`: Replaces the separately maintained current-ancestry set with an ancestry view derived from `ActiveAncestorPath` plus `ActiveExecutionContainer`; the distinct run-local visited evidence and all cycle/duplicate fail-closed behavior remain unchanged.

## Impact

- Future implementation scope: `src/UniClaw.Runtime/Agent/`, minimal immutable models under `src/UniClaw.Runtime/Model/`, existing Container observation acceptance seams, `src/UniClaw.Runtime.DriverHost/` read-only projections, Runtime/Harness observability projection, focused Unit/Scenario/Architecture tests, and a separately gated Runtime Debug Toolchain slice.
- No production code, tests, CLI, TUI, wire schema, persistence, or Runtime behavior is changed by this proposal-only change.
- `AuthorityDelta = NONE`; `OwnershipDelta = NONE`; top-level architecture and Contract invariants remain unchanged. The only architecture delta proposed is a subordinate Agent-internal consolidation seam replacing scattered Agent-owned state.
- Non-goals: NavigationGraph, ContainerManager, world topology, route planning/search, cross-session graph reuse, automatic recovery/re-entry, new completion authority, new action authority, copied completeness state, or persistent transition execution state.
