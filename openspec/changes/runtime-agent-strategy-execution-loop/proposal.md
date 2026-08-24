## Why

The accepted Strategy Contract and the Agent-owned pre-terminal checkpoint exist as separate capabilities, so RuntimeAgent cannot yet maintain a transactional, evidence-driven execution hypothesis throughout the same Run. A bounded Strategy execution loop is needed to connect those contracts without creating a second execution controller or moving action, lifecycle, recovery, or completion authority out of Agent.

## What Changes

- Add a run-scoped `StrategyExecutionReasoningSession` that binds one accepted `RuntimeExecutionIntent` to RuntimeAgent-owned hypothesis and reasoning revision history for one Agent-owned Run.
- Add an immutable, typed, scenario-neutral `StrategyExecutionEvidenceView` that projects only accepted evidence and correlation facts needed for bounded reconciliation.
- Define how RuntimeAgent evaluates each eligible Agent checkpoint using internal `ExecutionHypothesis`, `RuntimeDecision`, and `HypothesisAdaptation` records, then returns only the existing passive `PreTerminalContinuationProposal`.
- Require proposed reasoning revision N+1 to remain uncommitted until Agent validates freshness, correlation, parentage, and authority through the existing compare-and-accept seam.
- Define one reasoning mode per Run so the same accepted evidence cannot be reconciled through both pre-terminal and post-run paths.
- Separate RuntimeAgent reasoning convergence from Agent-owned execution completion.
- Preserve the existing generic discovery and traversal engine; the new capability supplies no concrete target, route, action, or branch ordering.
- Exclude Strategy replacement during an active Run, external semantic dependencies, scenario knowledge, DFS or Traversal redesign, recovery redesign, new Run creation, and Multi-Run orchestration.

## Capabilities

### New Capabilities

- `runtime-agent-strategy-execution-loop`: Defines the run-scoped reasoning session, bounded evidence projection, transactional checkpoint participation, convergence boundary, and authority-preserving integration of accepted Strategy execution with the existing Agent-owned Run.

### Modified Capabilities

- None.

## Impact

- Design scope: RuntimeAgent strategy reasoning, immutable evidence projection, reasoning revision transactions, and integration with the existing Agent-owned pre-terminal checkpoint.
- Dependencies: `uniagent-runtimeagent-strategy-contract` and `runtime-agent-pre-terminal-cycle-contract`.
- Unchanged authority: UniAgent owns Strategy generation and selection; Agent owns RunState, checkpoint acceptance, action authorization, recovery, GoalEvidence, and terminal outcome; FSM owns lifecycle transition protocol; Traversal owns concrete execution; Environment owns Observation acquisition.
- Compatibility: no existing external operation or Strategy payload changes; no active Run can replace its accepted Strategy.
- Delivery boundary: this change contains OpenSpec design artifacts only. Production code and tests remain unchanged until a separate apply approval.
