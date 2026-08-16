# Design: Semantic Run Unexpected Navigation Reconciliation

## Existing F5 Mechanism

`ReconcilePostScrollContinuityFailure` handles:
1. Different known page → CreateContainer + Bind + refresh + continue
2. Unknown page → SemanticContradiction
3. Same page but continuity cannot be proven → SemanticContradiction

## Gap

The post-action path (after SetSwitch/Tap) returns SemanticContradiction when `TryVerifyLocalContinuity` fails, even if the fresh Observation resolves to a different KNOWN page.

## Solution

Extract the generic known-page reconciliation into `ReconcileKnownPageTransition`:

```csharp
private SemanticRunResult? ReconcileKnownPageTransition(
    Observation freshObs,
    WorldBelief freshBelief,
    RuntimeContainer oldContainer,
    StartupResult.Ready ready,
    string runId,
    string context)
```

This method:
1. If freshBelief.SemanticPage is different known page:
   - Create new Container for that page
   - Bind fresh Observation
   - Refresh evidence
   - Return null (continue)
2. If unknown: return SemanticContradiction
3. If same page but continuity failed: return SemanticContradiction

Both the scroll path and post-action path call this shared method.
