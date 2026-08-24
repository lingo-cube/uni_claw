# Runtime

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md), [Greenfield Runtime Charter](../system/greenfield-runtime-charter.md)

## RuntimeAgent

Architecture v1 identifies RuntimeAgent as the runtime execution authority.
RuntimeAgent accepts bounded directives, runs the closed loop, reconciles
evidence, and produces execution outcomes. Its external role is defined by
[Architecture v1](uniagent-architecture-v1-core-development-guide.md).

RuntimeAgent executes a bounded Execution Goal from a Directive; the
Session-level Primary Goal remains UniAgent-owned. RuntimeAgent may expand the
Directive through a revisable Runtime-local Plan, while the Supervisory Plan is
UniAgent-local and is not a Runtime wire field. The additive
`run.strategy.start` operation admits one already-resolved typed
StrategyDirective at Run start and interprets it into a non-action
RuntimeExecutionIntent. It does not enable mid-Run redirection or Multi-Run
continuation, and the frozen `run.start` contract remains unchanged.

Source: [Agent Concept Model v1](agent-concept-model-v1.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Execution loop

The RuntimeAgent loop observes, reconciles, decides, executes, observes,
verifies, updates, and continues within the runtime boundary. Completion is
grounded in GoalEvidence rather than dispatch alone.

Sources: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md), [Greenfield Runtime Charter](../system/greenfield-runtime-charter.md).

An Agent Loop is a bounded activity and is not identical to a Run. Repeated
cycles are pre-terminal within the same Run; after `Completed` or `Failed`, a
new dispatch requires a separately authorized Run model. Runtime Outcome
describes the Runtime lifecycle; Goal Evaluation is a separate UniAgent-owned
evaluation of Primary Goal completion and satisfaction.

Source: [Agent Concept Model v1](agent-concept-model-v1.md).

## Responsibility and ownership

The responsibility direction is Agent → Container → Traversal → Environment.
The Agent owns run-level intent and lifecycle; a Container owns page-local
runtime state; Traversal advances local work; Environment provides interaction
and observation boundaries. Runtime internal rules are defined only by the
[Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md)
and [Greenfield Runtime Charter](../system/greenfield-runtime-charter.md).

## Authority boundary

RuntimeAgent retains execution, physical, belief, and completion authority.
External capability outputs are evidence or advice and remain subject to
RuntimeAgent reconciliation.

Source: [Architecture v1](uniagent-architecture-v1-core-development-guide.md).

GoalEvaluation and UniAgent Decision have a frozen semantic contract but no
current DTO/store representation. UniAgent Trace remains a missing contract;
non-terminal escalation transport is reserved and is not a current Runtime
terminal-state contract.

Sources: [Agent Concept Model v1](agent-concept-model-v1.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Decision + Goal Evaluation minimum contract](uniagent-decision-goal-evaluation-minimum-contract.md).
