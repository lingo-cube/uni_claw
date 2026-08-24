# Change: RuntimeAgent Pre-Terminal Cycle Contract

## Why

RuntimeAgent Phase 1-4 already owns bounded runtime interpretation through Directive, ExecutionHypothesis, RuntimeDecision, and HypothesisAdaptation. Those reasoning records currently describe post-run reasoning and must remain internal to RuntimeAgent.

The missing capability is an Agent-owned checkpoint inside one active Run. After Agent accepts fresh evidence, updates WorldBelief and DFS progress, and before it authorizes another action, RuntimeAgent must be able to evaluate an immutable snapshot and return a passive continuation proposal. Agent must then validate the proposal and independently decide whether the same Run continues, completes, or fails.

Without this boundary, either RuntimeAgent reasoning cannot influence the same Run before terminal state, or Agent becomes coupled to RuntimeAgent's internal RuntimeDecision and HypothesisAdaptation records. The latter would move reasoning semantics into Agent and violate the frozen authority model.

## What Changes

- Add an immutable `PreTerminalReasoningSnapshot` created by Agent at an eligible pre-action checkpoint.
- Add a passive `PreTerminalContinuationProposal` returned by RuntimeAgent. It communicates only whether continuation is supported, supported after a reasoning revision, or not supported.
- Keep RuntimeDecision and HypothesisAdaptation as RuntimeAgent-internal reasoning records; Agent never consumes them as lifecycle commands.
- Add transactional reasoning revision semantics: RuntimeAgent evaluates accepted reasoning revision N without mutating it and proposes N+1. Agent may commit N+1 only after freshness, correlation, and authority validation.
- Require rejected, stale, duplicate, timed-out, cancelled, terminal, or unknown proposals to be discarded with zero action and no accepted reasoning-history mutation.
- Keep checkpoint timing, cycle sequence, validation, proposal acceptance, same-Run continuation, action authorization, recovery, GoalEvidence, RunState, and terminal outcome under Agent ownership.
- Keep the seam optional. When disabled, existing Agent execution behavior is unchanged.

## Capabilities

### New Capabilities

- `runtime-agent-pre-terminal-cycle`: Defines the Agent-owned snapshot/proposal seam and transactional reasoning revision contract for bounded RuntimeAgent participation before terminal state within one Run.

### Modified Capabilities

- None.

## Impact

- **Agent:** gains an optional validation seam at a precisely bounded checkpoint. Agent authority does not move.
- **RuntimeAgent:** gains a pre-terminal adapter over its existing internal Phase 2-4 reasoning records. It gains no action, lifecycle, completion, or Multi-Run authority.
- **RuntimeAgent reasoning layer:** owns proposed and accepted reasoning revisions, but no mutable Run, WorldBelief, DFS, FSM, or execution state.
- **FSM, Traversal, Environment, GoalEvidence, DFS engine:** unchanged.
- **Regression boundary:** no checkpoint is created when the seam is absent or disabled; existing execution remains behaviorally identical.

## Authority Proof

The change creates no path from RuntimeAgent or its proposal to DeviceAction, Traversal, FSM transition, RunState mutation, GoalEvidence completion, recovery command, or another Run. A proposal is passive evidence for Agent validation. Only Agent may accept a reasoning revision and independently choose an existing lifecycle path.
