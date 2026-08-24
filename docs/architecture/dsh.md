# DSH Relationship

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md)
> ProjectionSources: [current gates](../work/active/current-gates.md)

## Position

DSH is an Architecture v1 implementation framework / host. `DSH !=
Architecture`: it composes and hosts implementation surfaces without becoming a
top-level architecture component.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Protocol surfaces

The current protocol model identifies the Directive, Outcome, Capability
Contract, and Session surfaces. DSH may host their implementation or transport
integration, while Protocol v1 defines their semantic relationship.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

The Supervisory Plan is UniAgent-local and the Runtime-local Plan is
RuntimeAgent-local; neither creates a current DSH protocol surface. DSH may
host their implementation context without acquiring execution or evaluation
authority. GoalEvaluation and UniAgent Decision have a frozen semantic contract
but no DSH DTO/store/UI realization; UniAgent Trace is not yet a current
contract, and non-terminal escalation transport remains reserved.

Sources: [Agent Concept Model v1](agent-concept-model-v1.md), [Decision + Goal Evaluation minimum contract](uniagent-decision-goal-evaluation-minimum-contract.md).

## Runtime authority boundary

RuntimeAgent-owned execution truth, physical execution, WorldBelief, and
GoalEvidence authority do not belong to DSH. DSH hosting does not transfer
RuntimeAgent authority.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Active DSH gates

Current DSH gate retrieval is provided by [current gates](../work/active/current-gates.md).
The [Active OpenSpec Lifecycle Matrix](../decisions/active-openspec-lifecycle-matrix.md)
is a historical snapshot and not a current projection source. This projection
does not classify, graduate, or modify gate records.
