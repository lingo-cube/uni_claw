# DSH Relationship

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Active OpenSpec Lifecycle Matrix](../decisions/active-openspec-lifecycle-matrix.md)

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

## Runtime authority boundary

RuntimeAgent-owned execution truth, physical execution, WorldBelief, and
GoalEvidence authority do not belong to DSH. DSH hosting does not transfer
RuntimeAgent authority.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Active DSH gates

Current DSH gate retrieval is provided by the [Active OpenSpec Lifecycle
Matrix](../decisions/active-openspec-lifecycle-matrix.md). This projection does
not classify, graduate, or modify those gate records.
