## Context

SC-P3-CAND-006 isolates one remaining S0 Capstone pressure: a fresh bounded Settings Observation can expose physically actionable candidates that were not pre-authorized as fixed PlanSteps. The current Runtime can prove observation and execution, but it has no per-candidate authorization semantic between those facts. Fixed Plan omission provides static safety only; Plan presence would preauthorize a candidate and erase the Scenario pressure.

Agent already owns Goal/intent, Plan, WorldBelief, candidate-level high semantic decisions, active Container transitions, GoalEvidence consumption, and final RunState. Container owns the current page-local Observation/candidates. Traversal owns local Select → Check → Execute → Observe → Verify and its journal. Environment owns external evidence and dispatch outcomes.

The approved Gate purchases exactly one immutable two-field authorization-evidence value and one optional immutable Goal evaluator field. Model types +1, total fields +3, enums/interfaces/components/new mutable owners +0, Ownership Delta NONE, and Authority Delta NONE.

## Goals / Non-Goals

**Goals:**

- Keep observed candidate existence, semantic authorization, dispatch, world effect, required work, and Goal completion distinct.
- Evaluate one fresh bounded Settings candidate set deterministically under an Agent-owned read-only criterion.
- Represent authorized, positively rejected, and unresolved outcomes with an explicit reason.
- Allow at most one authorized safe navigation candidate to enter existing Tap mechanics.
- Record rejected and unresolved candidates before dispatch using existing Agent Trace.
- Prove zero matching journal dispatch and ActionHistory entries for rejected/unresolved candidates.
- Prevent visible rejected/unresolved candidates from becoming fabricated required safe work.
- Preserve existing fixed-Plan behavior when the optional evaluator is absent.
- Replay candidate outcomes, evidence, actions, journal, GoalEvidence, and final state deterministically.

**Non-Goals:**

- General candidate discovery, autonomous task planning, multi-page route construction, or arbitrary UI understanding.
- Universal interception of every action source or a general safety/policy/rule engine.
- SafetyManager, RiskEngine, SafeActionExecutor, authorization manager, RiskLevel, Confidence, policy hash, coordinates, Fingerprint, Vision/VLM judgement, navigation graph/stack, or mutable safety owner.
- New action variants, Trace/journal/Trap fields, Trap kinds, completion semantics, or Recovery behavior.
- Capstone implementation, Harness change, Runtime refactor, S1/S2/S3 work, or Phase completion.

## Decisions

### Add one immutable three-way authorization-evidence value

Add `CandidateAuthorizationEvidence` with exactly two immutable fields:

```csharp
bool? Authorized
string Reason
```

`Reason` must be non-empty. `true` means the supplied fresh Observation/candidate positively satisfies the bounded read-only criterion; `false` means fresh evidence positively rejects the candidate; `null` means evidence is insufficient and grants no authorization.

The value is consumed immediately by Agent control flow and recorded through existing Trace. It is not persistent mutable authorization state, a policy rule, a risk score, or an execution result.

Alternative rejected: `bool`. It cannot distinguish positive rejection from insufficient evidence.

Alternative rejected: `bool?` alone. It cannot explain why no dispatch occurred without hardcoding Scenario rules into Agent.

Alternative rejected: reuse `GoalEvidence`. That value has whole-Goal completion meaning and cannot be reinterpreted as per-candidate authorization.

Alternative rejected: reuse `TraversalStepResult.Failed`. A semantic rejection occurs before a candidate enters Traversal and is not a local execution failure.

### Carry one optional bounded criterion on Agent-owned Goal

Add exactly one optional immutable field:

```csharp
Func<Observation, ObservedElement, CandidateAuthorizationEvidence>?
    Goal.CandidateAuthorizationEvaluator
```

Goal is the smallest existing immutable Agent-owned intent surface. The evaluator must be deterministic, side-effect-free, and depend only on the supplied fresh Observation and an `ObservedElement` contained in that Observation. It cannot inspect or mutate Runtime owners, call Environment, dispatch an action, or set RunState.

An absent evaluator means no newly discovered non-preauthorized candidate receives authorization; existing fixed-Plan behavior remains unchanged.

Alternative rejected: attach the criterion to PlanStep. A PlanStep already names a preauthorized target/action and would collapse the distinction being proven.

Alternative rejected: add a policy string to Goal. It would require an unpurchased parser/interpreter.

Alternative rejected: inject a component/interface. The bounded deterministic delegate requires no new owner or architectural layer.

### Agent remains the unique semantic authorization authority

When the optional evaluator is present, Agent performs one bounded classification pass over the active Container's fresh current Observation before the fixed Plan loop. It evaluates candidates in stable Observation order and records every `false` or `null` result in Agent Trace with candidate text/index, source sequence, outcome, and the supplied reason. Those events have no Action or ActionId.

Agent may deterministically nominate the first `true` candidate as one safe navigation Tap and send a transient local step through the existing Container/Traversal execution path. This bounded nomination is ordinary Agent deviation from a Plan hypothesis under I-5; it is not a route planner, candidate registry, or mutable plan.

Traversal does not re-evaluate semantic safety. It continues to own mechanical selection, grounding, dispatch, post-action observation, and verification. It may reject mechanically, but it cannot turn a rejected/unresolved candidate into an allowed one.

Alternative rejected: evaluate safety independently in both Agent and Traversal. That would create duplicate semantic authority and violate I-3.

Alternative rejected: let Traversal own the criterion. Traversal is a local execution kernel and cannot interpret the Run's read-only semantic intent.

### Bound the authorized execution to existing Tap mechanics

Within SC-P3-CAND-006, `Authorized=true` means the candidate is eligible as one safe navigation Tap. No other discovered action type is purchased. State-bearing candidates are rejected by the Scenario evaluator; destructive and unresolved candidates are not sent to Container/Traversal.

If an authorized candidate is nominated, existing Traversal semantics still require Select → Execute → Observe → Verify. Authorization does not imply dispatch success, world effect, or Goal completion. Agent evaluates existing GoalEvidence from the fresh post-action Observation.

If no candidate is authorized, Agent returns an explicit existing non-completion/failure outcome after recording all rejected/unresolved evidence. It does not dispatch, does not fabricate completion, and does not convert visible denied candidates into required work.

### Reuse Trace and journal/action absence as denial proof

Agent Trace is the existing append-only semantic evidence surface. A deterministic denial event uses existing RunId, ContainerId, and Reason; it leaves StepId, ActionId, and Action absent. The reason carries the source Observation sequence, candidate text/index, `rejected` or `unresolved`, and the evaluator's non-empty reason.

Because rejected/unresolved candidates never enter Traversal:

- no Traversal journal entry dispatches an action for their index;
- Environment ActionHistory contains no matching action;
- no post-action Observation or ActionResult is fabricated for them.

Alternative rejected: add a new audit component, Trace field, journal result, or Trap kind. Existing append-only evidence is sufficient for this bounded proof.

### Keep authorization separate from required-work and completion evidence

Observation membership means only that a candidate is visible. Authorization `true` means eligibility, not required-work membership. Existing Agent-owned Goal/approved scope and evidence-backed branch progress continue to determine required safe work. Rejected or unresolved candidates are never added to approved branch inventory merely because they were observed.

Only satisfied GoalEvidence may set `RunState.Completed`. Authorization, denial, zero dispatch, Plan exhaustion, and local Traversal success cannot independently complete the Run.

## Risks / Trade-offs

- [Risk] A delegate can capture mutable or nondeterministic state. → Formal replay uses equal inputs and requires equal outcomes/reasons; the normative contract forbids external state access and side effects.
- [Risk] A free-form Reason could drift into a policy vocabulary. → The bounded value requires only a deterministic non-empty explanation; no stable global rule taxonomy or parser is purchased.
- [Risk] Agent control flow gains another bounded pre-loop branch. → Keep it opt-in through the optional Goal field, allow at most one authorized Tap, record structural pressure, and defer refactor.
- [Risk] Text/SwitchState cannot classify arbitrary UI controls. → `null` is the required outcome when evidence is insufficient; Vision/VLM and generalized semantics remain deferred.
- [Risk] An authorized safe candidate could be mistaken for required work. → Authorization is explicitly necessary eligibility only; Goal/progress evidence remains authoritative for required work and completion.
