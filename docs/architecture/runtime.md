# Runtime

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md), [Greenfield Runtime Charter](../system/greenfield-runtime-charter.md)

## RuntimeAgent

Architecture v1 identifies RuntimeAgent as the runtime execution authority.
RuntimeAgent accepts bounded directives, runs the closed loop, reconciles
evidence, and produces execution outcomes. Its external role is defined by
[Architecture v1](uniagent-architecture-v1-core-development-guide.md).

## Execution loop

The RuntimeAgent loop observes, reconciles, decides, executes, observes,
verifies, updates, and continues within the runtime boundary. Completion is
grounded in GoalEvidence rather than dispatch alone.

Sources: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md), [Greenfield Runtime Charter](../system/greenfield-runtime-charter.md).

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
