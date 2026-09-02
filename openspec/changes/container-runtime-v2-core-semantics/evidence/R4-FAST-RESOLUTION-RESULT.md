# R4 Fast Container Resolution Result

Date: 2026-09-01

## STATUS

`CONTAINER_RUNTIME_V2_CORE_PURCHASE_IN_PROGRESS`

The synchronous Fast resolution seam passed the Leader Gate as a controlled architecture hypothesis. It is executable in isolation but is not integrated into Agent behavior and is not graduated.

## PURCHASED

- A bounded action-prior vocabulary: unknown, may-enter, may-return, strong-same and may-external.
- Revision-bound `SAME_CONTAINER / NEW_CONTAINER / TRANSIENT / AMBIGUOUS` working assessments.
- Fresh independent-boundary evidence, fresh same-Container continuity, existing validated semantic evidence, trigger/destination semantic support, authority-free Graph candidates and hard conflict as explicit inputs.
- Derived Fast Trust requiring independent-boundary support, semantic support and no hard conflict.

## HYPOTHESIS

- Fast resolution remains a controlled hypothesis. Its mandatory Runtime value must be demonstrated by false-trust, abstention, blocker-reduction and latency evidence against the existing deterministic/Fast-provider baseline.

## IMPLEMENTED

- `FastContainerResolver` is a stateless pure World function; no new interface was needed because there is one deterministic implementation and no replacement backend buyer.
- `FastContainerResolutionRequest` consumes `ValidatedSemanticEvidenceResult`, not raw provider evidence, preserving `ISemanticEvidenceFusion` as the admission boundary.
- `FastContainerAssessment.FastTrusted` is computed and has no mutable backing state.
- Existing Fast provider/vector retrieval and fusion code were reused unchanged.

## VALIDATED

- Leader rejected the intermediate raw-`SemanticEvidence` input because it bypassed the existing fusion boundary. Final request accepts only the validated result and filters support by fresh observation sequence and scope.
- Targeted Fast resolver, existing Fast semantic, V2 core and legacy transition tests: 95 passed, 0 failed.
- `dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj --no-restore -t:Rebuild -v:minimal`: 0 errors; no warning originates from `FastContainerResolver.cs`; existing repository warnings remain.
- Tests prove action prior alone, Graph prior alone and semantic/vector candidate alone do not establish boundary truth; hard conflict wins; stale/scope-mismatched semantic evidence cannot produce Fast Trust; abstention does not mutate Graph or CurrentContainer.
- The existing Fast provider latency test remains green, and the new resolver performs no I/O or async work.
- `git diff --check`: passed.
- `AuthorityDelta = NONE`; `BehaviorDelta = NONE`; `NET_NEW_MUTABLE_TRUTH = 0`.

## DEFERRED

- Mapping concrete dispatched `DeviceAction` values into the typed prior at the Agent integration boundary.
- Production consumption of Fast assessment, node bind/fold/reject and CurrentContainer commit.
- New embedding/vector/backend purchase and any Fast-derived action authorization.

## RISKS

- Boolean boundary/semantic inputs are assumed to come from accepted Runtime evidence at the future integration boundary; Agent integration must bind their exact evidence refs rather than fabricate support.
- `FAST_TRUSTED` is working interpretation only. Persisting it or using it for action/completion would violate this purchase.
- Real-device false-trust and abstention rates remain unknown.

## NEXT_WORKITEM

Add a provider-neutral Slow Advisor contract with Disabled and Shadow consumption, exact request/result evidence binding, async stale-result handling, and zero Graph/Current/action/recovery/completion effect. Use a test fake only; do not select or add a concrete provider/backend.
