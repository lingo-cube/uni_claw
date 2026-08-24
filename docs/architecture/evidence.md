# Evidence

> DocumentType: CURRENT_STATE_PROJECTION
> Authority: NONE
> CanonicalSources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Protocol v1](uniagent-protocol-v1-consolidation-design.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md)

## Evidence route

The current evidence route is
Raw observation → canonical occurrence → typed semantic candidate → reconciled
belief → GoalEvidence. Environment acquires raw observation; the Runtime
source-grounding normalizer projects source-qualified observation occurrences
into immutable canonical occurrences (Vision occurrences are primary and
independently groundable; ADB hierarchy is optional auxiliary corroboration
only); an external Semantic Capability emits typed candidate evidence that
Runtime admits, fuses, and reconciles; the Agent evaluates GoalEvidence.
These labels describe stages of evidence handling; this document introduces no
new evidence schema or contract.

Sources: [Architecture v1](uniagent-architecture-v1-core-development-guide.md), [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md), [runtime-external-semantic-capability-boundary change](../../openspec/changes/runtime-external-semantic-capability-boundary/).

## Observation and belief

Observation is evidence, not semantic truth. RuntimeAgent forms WorldBelief by
reconciling fresh observation and evidence; historical state does not substitute
for fresh observation.

Sources: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md), [Architecture v1](uniagent-architecture-v1-core-development-guide.md).

Observation is produced by the Environment/perception boundary and reconciled
by RuntimeAgent. Facts are append-only assertions; WorldBelief is a revisable
projection and does not replace the Fact history.

Source: [Agent Concept Model v1](agent-concept-model-v1.md).

## Dispatch and truth

Physical dispatch is not truth or completion. It is followed by observation,
verification, and reconciliation within the RuntimeAgent boundary.

Source: [Runtime Architecture Contract](../system/constitution/runtime-architecture-contract.md).

## GoalEvidence

GoalEvidence is RuntimeAgent-owned, kernel-only completion evidence. The Agent
decides completion from GoalEvidence; external consumers receive only
producer-derived information and do not re-originate truth.

Source: [Protocol v1](uniagent-protocol-v1-consolidation-design.md).

Runtime Outcome and UniAgent Goal Evaluation remain distinct: Goal Evaluation
may assess completion and satisfaction without rewriting Runtime Outcome.

Source: [Agent Concept Model v1](agent-concept-model-v1.md).

## Harness capture and reviewed replay

The current Harness implementation can observe the unchanged `IEnvironment`
boundary, preserve external call order, attach physical screenshot/perception
evidence, and publish immutable capture bundles through an append-only atomic
store. Runtime and capture outcomes remain separate.

Reviewed regression assets are admitted through explicit Scenario IDs. Deep
catalog loading validates manifest and referenced artifact paths, hashes,
schema versions, provenance, and references before replay. Existing ad-hoc
trace files remain untyped source evidence; they are not converted into
Runtime facts or completion evidence.

For explicitly registered finalized runs, the DriverHost now exposes an
in-process trace summary and stable cursor-paged span read model. Initial
placeholder traces remain unavailable; a finalized zero-span trace remains a
valid diagnostic value. Span outcomes and diagnostics are returned as recorded
and are never promoted into Runtime Result, GoalEvidence, or Goal Evaluation.

The Harness can also reconstruct one explicitly named published capture by
`CaptureSessionId`, validating its structural JSON, artifact integrity,
checksum coverage, and optional TraceRun before returning an immutable bundle.
The reader neither scans for correlation nor repairs, rewrites, catalogs, or
replays persisted evidence. No new DriverHost wire surface is introduced.

Sources: [Trace Capture architecture gate](../decisions/trace-capture-scenario-catalog-architecture-gate.md), [capture foundation change](../../openspec/changes/trace-capture-scenario-catalog-foundation/), [trace/span read-model change](../../openspec/changes/trace-span-read-model/).
