# Vision and Semantic Perception

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Semantic Perception Layer Baseline](../decisions/semantic-perception-layer-baseline.md)

## Position

Vision and Semantic Perception are subordinate capabilities within Architecture
v1. RuntimeAgent owns the perception contract and retains fusion, acceptance,
rejection, and reconciliation authority over capability output.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

## Alignment decision

`SUBORDINATE_TO_ARCHITECTURE_V1`

The Semantic Perception baseline is not an independent architecture. It does
not alter RuntimeAgent authority.

Source: [Semantic Perception Layer Baseline](../decisions/semantic-perception-layer-baseline.md).

## Apply status

`APPLY_NOT_AUTHORIZED`

This status does not assert implementation of Fast Semantic or Slow Semantic
capabilities and does not change the lifecycle of Vision or Semantic Perception.

Source: [Semantic Perception Layer Baseline](../decisions/semantic-perception-layer-baseline.md).
