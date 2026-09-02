# R3 Evidence-only Graph Seam Result

Date: 2026-08-31

## STATUS

`CONTAINER_RUNTIME_V2_CORE_PURCHASE_IN_PROGRESS`

The evidence-only Graph seam passed the Leader Gate. It remains a Run-local controlled architecture experiment and is not a planner, current-world authority, action authority, persistent memory, or graduated capability.

## PURCHASED

- `ContainerGraphSnapshot` is the immutable read contract.
- `ContainerRuntimeV2Reducer` is the validate-before-replace record contract.
- `ContainerGraphQuery` produces revision-bound derived relation assessments without storing maturity/trust state.
- Historical relation evidence remains append-only; a newer accepted completed occurrence with the same Source and entry-affordance evidence but a different Destination can challenge the historical prior.
- Only aggregate-accepted occurrence evidence and recorded relation refs can participate in an assessment.

## HYPOTHESIS

- The Run-local Graph remains a controlled hypothesis. Graduation requires measured value for multi-entry verification, correction and reduced reconstruction cost without false-model/action-authority leakage.

## IMPLEMENTED

- Reused the existing V2 snapshot and reducer rather than creating `IContainerGraphReader`, `IContainerGraphRecorder`, a Store, a Service, or another state owner.
- Added immutable `ContainerGraphRelationAssessment` and static `ContainerGraphQuery` in the existing model file.
- Added deterministic evidence for multi-entry, repeated occurrence support, equal trigger non-identity, fresh-over-historical challenge, abnormal occurrence retention, and query immutability.

## VALIDATED

- Leader rejected intermediate results that allowed an arbitrary uncommitted occurrence to challenge a relation and allowed arbitrary relation values to be assessed. Final behavior rejects both fail-closed.
- `dotnet test tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj --filter 'FullyQualifiedName~ContainerRuntimeV2Core|FullyQualifiedName~ContainerTransition' --no-restore`: 54 passed, 0 failed.
- `dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj --no-restore -t:Rebuild -v:minimal`: 0 errors; no warning originates from `ContainerRuntimeV2.cs`; existing repository warnings remain.
- `git diff --check`: passed.
- `AuthorityDelta = NONE`; `BehaviorDelta = NONE`; `NET_NEW_MUTABLE_TRUTH = 0`.

## DEFERRED

- Stateful Graph service, Agent-owned mutable slot, persistence and cross-Run memory.
- Action/route/return/recovery/completion APIs.
- Relation challenge consumption by Agent or any provider.

## RISKS

- `ContainerGraphRelationAssessmentKind` is a derived classification only; future code must not persist it as a relation maturity FSM.
- Entry-affordance correlation remains an opaque evidence ref. It is not display text or permanent ontology, and later consumers must preserve that distinction.
- Existing old `visited` and ActiveContainer semantics are not migrated by this behavior-neutral stage.

## NEXT_WORKITEM

Construct a provider-neutral, synchronous Fast Container resolution seam that reuses accepted semantic evidence and the V2 immutable Graph/current inputs. It must remain a pure assessment, derive working trust without mutable state, abstain on insufficient evidence, and never authorize action or update the Graph/CurrentContainer.
