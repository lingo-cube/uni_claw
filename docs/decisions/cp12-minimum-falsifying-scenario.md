# CP12_MINIMUM_FALSIFYING_SCENARIO_CONTRACT

> Scenario: `SC-CP12-MVS-001`
> Capability: `GC-03 — Hypothesis-with-Fresh-Verification`
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Status: `VALIDATED`

## Accepted Semantic Envelope

- CP-12 is explained by accepted RM-10 ER-25..ER-28.
- GC-03 is the selected minimum capability candidate.
- A target hypothesis is provisional, cites observable support and limitation,
  and is tested against fresh post-action world evidence.
- Qualitative evidence sufficiency is enough for this bounded slice; GC-03 does
  not require a numeric confidence value or numeric threshold model.
- Existing Agent-owned candidate safety authorization independently gates any
  hypothesis dispatch.
- Grounding evidence is not GoalEvidence, world truth, or stable element
  identity and cannot complete a Run.

## Given

1. One fresh Settings Observation exposes two safe navigation candidates:
   `Wi-Fi` and `Wi-Fi Calling`.
2. Both candidates match the target description and have navigation-compatible
   observable properties; text alone is insufficient identity proof.
3. The current PlanStep carries one deterministic two-phase grounding criterion:
   candidate evidence from the current Observation and an expected-effect
   evaluator for the first fresh post-action Observation.
4. Candidate evidence is three-way: sufficiently supported, positively
   inconsistent, or insufficient/ambiguous, always with a deterministic reason
   stating observable support and limitations.
5. Agent supplies an immutable snapshot of its existing safety-authorization
   evidence without selecting a target.

## Positive Oracle

```text
Traversal receives current candidates + immutable Agent safety receipts
→ Traversal evaluates PlanStep-local grounding evidence
→ exactly one text-matching candidate is qualitatively supported
→ that candidate has Authorized=true safety evidence
→ Traversal selects and dispatches exactly one Tap
→ Traversal obtains the first fresh Observation
→ PlanStep expected-effect evidence confirms Wi-Fi Settings
→ Traversal returns Succeeded with journal evidence
→ Agent reconciles and continues the existing GoalEvidence path
→ only separate satisfied GoalEvidence may complete the Run
```

Required evidence:

- candidate membership, grounding sufficiency, target selection, preconditions,
  dispatch, freshness, and expected-effect verification all occur inside the
  Traversal step protocol;
- exactly one Tap reaches Environment;
- post-action Observation sequence strictly advances;
- grounding support and confirmation reasons are observable through existing
  journal/result/Trace surfaces;
- grounding confirmation itself does not set `RunState.Completed`.

## Falsifying Oracle

```text
same supported and safety-authorized hypothesis
→ Traversal dispatches exactly one Tap
→ first fresh Observation contains Wi-Fi Calling Settings
→ expected Wi-Fi Settings effect is contradicted
→ Traversal returns structured Failed(REJECTED reason) with fresh journal evidence
→ Container forwards the result unchanged
→ Agent retains final Run failure authority
→ no fabricated identity, Goal success, completion, or blind redispatch
```

The implementation fails this Scenario if Agent selects the element, if
Traversal dispatches before both grounding and safety gates pass, if
contradictory fresh evidence is treated as success, if another Tap is dispatched
automatically, or if grounding evidence directly completes the Run.

## Ambiguity / Safety Controls

- No candidate has `Supported=true` → Traversal returns pre-dispatch failure;
  zero Tap.
- More than one candidate has `Supported=true` → ambiguity remains; Traversal
  returns pre-dispatch failure; zero Tap.
- Selected candidate safety evidence is `false`, `null`, or absent → Traversal
  returns pre-dispatch failure; zero Tap.
- Fresh expected-effect evidence is `null` → structured UNCONFIRMED failure;
  zero redispatch.
- Equal inputs replay equal grounding evidence, safety receipts, journal,
  actions, Observations, GoalEvidence, Trace, and final RunState.

## Architecture Boundary Reconciliation

| Responsibility | Canonical owner/authority | Reconciled CP12 behavior |
|---|---|---|
| Goal, Plan, world interpretation, safety authorization, final RunState | Agent | Agent creates only immutable safety receipts and consumes the Traversal result; it does not select candidates, dispatch, or verify local expected effects. |
| Current Observation/candidates/local progress | Container | Container retains state and forwards step input/result without deciding grounding. |
| Candidate membership, local target selection, preconditions, dispatch, fresh Observe, expected-effect verification, journal | Traversal | All GC-03 per-element execution mechanics stay inside the existing `Select → Check → Execute → Observe → Verify → Branch` authority. |
| Dispatch outcome and external Observation | Environment | Unchanged; Environment makes no grounding or Goal decision. |

The prior Agent-shaped proposal was not architecture-compatible because it moved
candidate membership validation, grounding sufficiency, target nomination, Tap
dispatch coordination, and first fresh expected-effect verification into Agent.
That would duplicate Traversal authority and pressure Agent toward local
per-element bookkeeping.

The reconciled shape removes that conflict without moving ownership, authority,
or dependency direction.

`ARCHITECTURE_FIT_CONFIRMED`

## Proven-Minimal Semantic Purchase

### 1. One immutable `TargetGroundingEvidence` value

Exactly two immutable fields:

1. `Supported: bool?`
   - during candidate evaluation: `true` = sufficiently supported,
     `false` = positively inconsistent, `null` = insufficient/ambiguous;
   - during post-action verification: `true` = confirmed, `false` = rejected,
     `null` = unconfirmed.
2. `Reason: string` — non-empty deterministic observable support, expected
   effect, contradiction, and/or limitation as appropriate to the phase.

This is qualitative evidence sufficiency. It does not purchase numeric
confidence, a threshold, ranking, policy, or stable identity.

### 2. One immutable `TargetGroundingCriterion` value

Exactly two immutable fields:

1. `CandidateEvaluator:
   Func<Observation, ObservedElement, TargetGroundingEvidence>`
2. `PostActionEvaluator:
   Func<Observation, TargetGroundingEvidence>`

Both evaluators are deterministic, side-effect-free, Observation-only evidence
functions. Grouping them prevents a partially configured one-phase grounding
path and binds pre-action hypothesis evidence to its explicit falsifier.

### 3. One optional immutable PlanStep field

```text
TargetGroundingCriterion: TargetGroundingCriterion?
```

The criterion belongs to PlanStep because target description, intended action,
and expected effect are step hypotheses. It does not expand Goal or GoalEvidence
responsibility.

### 4. Existing-layer control-flow adjustments only

- Agent:
  - for a grounded fixed PlanStep, evaluate the frozen
    `Goal.CandidateAuthorizationEvaluator` over the current candidate snapshot
    and pass immutable index-keyed authorization receipts downward;
  - do not nominate a candidate;
  - do not activate the older SC-P3-CAND-006 transient discovered-candidate Tap
    insertion for that grounded fixed PlanStep;
  - consume the returned Traversal result and retain final Run/Goal authority.
- Container:
  - forward the immutable authorization snapshot with the PlanStep;
  - preserve all state and decision boundaries.
- Traversal:
  - when the criterion is absent, preserve frozen selection behavior;
  - when present, evaluate only current text-matching candidates;
  - require exactly one `Supported=true` candidate and an Agent receipt with
    `Authorized=true` before dispatch;
  - own Tap construction/dispatch, first fresh Observe, sequence validation,
    post-action criterion evaluation, journal, and structured result;
  - return Failed for rejection/unconfirmed/ambiguity without redispatch.

## Production Delta Budget

```text
New immutable model types: 2
New immutable production fields: 5
  - TargetGroundingEvidence: 2
  - TargetGroundingCriterion: 2
  - PlanStep.TargetGroundingCriterion: 1
New numeric Confidence/Threshold fields: 0
New Goal fields: 0
New enums/interfaces/components: 0
New mutable-state fields/owners: 0
Agent behavior: immutable safety-receipt preparation only
Container behavior: immutable forwarding only
Traversal behavior: one opt-in local grounding/verification path
Environment/Recovery behavior: 0
Ownership delta: NONE
Authority delta: NONE
Dependency delta: NONE
Invariant delta: NONE
```

## Allowed Implementation Scope After Human Authorization

- `src/UniClaw.Runtime/Model/TargetGroundingEvidence.cs`
- `src/UniClaw.Runtime/Model/TargetGroundingCriterion.cs`
- `src/UniClaw.Runtime/Model/Plan.cs` — one optional PlanStep field only
- `src/UniClaw.Runtime/Agent/Agent.cs` — immutable safety-receipt preparation,
  grounded-step routing, and result consumption only
- `src/UniClaw.Runtime/Container/Container.cs` — immutable receipt forwarding only
- `src/UniClaw.Runtime/Traversal/Traversal.cs` — bounded local
  select/check/execute/fresh-verify path only
- deterministic fixtures plus unit/formal/replay/regression tests
- CP12 gate/freeze documentation reconciliation

## Forbidden / Deferred Boundary

- no numeric Confidence or Threshold;
- no `Goal.TargetGroundingHypothesisEvaluator` or other Goal/GoalEvidence
  expansion;
- no Agent-owned candidate selection, target resolve, Tap construction/dispatch,
  or post-action local effect verification;
- no GroundingEngine/Manager, ranking framework, confidence framework,
  candidate registry, alternative-candidate retry, recovery, FSM, new action or
  status enum, perception/coordinate/spatial model, Fingerprint, Vision/VLM/Host
  integration, CP-11 correction, unsafe/irreversible hypothesis dispatch, or
  S1/S2/S3 work;
- no changes to frozen `CandidateAuthorizationEvidence` semantics;
- no grounding result may become GoalEvidence, Goal completion, world truth, or
  stable element identity.

## Gate Resolution

The bounded purchase was authorized by
`human-authorize-cp12-minimum-vertical-slice-implementation.md`, implemented,
and independently validated by
`cp12-minimum-vertical-slice-fast-loop-result.md`.

Canonical CP-12 terminal state: `VALIDATED`.
