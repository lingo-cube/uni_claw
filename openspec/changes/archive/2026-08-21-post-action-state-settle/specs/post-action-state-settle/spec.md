# Spec: post-action-state-settle

> BASELINE spec for a bounded post-action settle / fresh re-observation policy.
> No code in this change. Buyer:
> `docs/decisions/state-evidence-required-real-world-buyer.md`
> (G — REOBSERVATION_POLICY_BUYER_CONFIRMED; TRANSIENT_EVIDENCE_GAP = CONFIRMED,
> STRUCTURAL = FALSE). Owner precedent: `Traversal` execution-verification
> mechanics (B4 / SC-P2-002 step-scope retry).

## ADDED Requirements

### Requirement: Post-action settle owner is Traversal execution-verification mechanics

The post-action state settle MUST be owned by the Traversal execution-verification
mechanics (the existing owner of step-scope re-observe/re-resolve retry and the
Verify phase). It MUST NOT live in Agent semantic code, Environment, or
perception. The Agent keeps semantic authority; Environment stays passive.

#### Scenario: settle lives in the execution-verification mechanics

Given a state-changing action whose post-action state evidence is missing,
When the execution mechanics reach the Verify phase after Observe,
Then the bounded settle MAY run inside those mechanics — with the Agent's
semantic decision authority unchanged and no Environment change.

#### Scenario: no semantic-code ownership

Given the settle implementation,
When its placement is inspected,
Then it is NOT implemented in Agent semantic decision code and NOT in the
Environment (falsifier F1).

### Requirement: Eligibility is a generic, truthful predicate

A post-action settle MUST be eligible only when ALL hold: (1) an action was
actually dispatched in this step; (2) the action is state-changing /
verification-sensitive (expressed via the semantic action/capability shape, not
an action-type string match); (3) a fresh Observation exists for the step;
(4) the target binding/control remains identifiable in that observation;
(5) the required state evidence is temporarily unavailable (null), not
contradicting; (6) no contradiction proves failure (belief not Contradicted);
(7) the retry budget remains.

#### Scenario: generic over state-changing actions

Given any state-changing semantic action with missing post-action state evidence,
When eligibility is evaluated,
Then the predicate applies uniformly — it does NOT use an
`if action == <name> { sleep(...) }` policy style (falsifier F7).

#### Scenario: ineligible without a dispatched action

Given a step where no action was dispatched,
When the Verify phase evaluates settle eligibility,
Then no settle runs.

#### Scenario: ineligible while contradicting

Given post-action state evidence that is contradicting rather than missing,
When eligibility is evaluated,
Then no settle runs and the existing contradiction/failure semantics are
preserved (falsifier F4-adjacent: null is never converted to desired).

### Requirement: Settle semantics — immediate observe then bounded fresh re-observation

After dispatch the mechanics MUST observe immediately (existing path). When state
evidence is unavailable and eligibility holds, the mechanics MAY run a bounded
settle: small evidence-evaluating delay, then a strictly fresh Observation
(`SequenceNumber` strictly advances), then re-evaluate state evidence. The policy
MUST NEVER treat the action as succeeded without valid evidence, MUST NEVER
synthesize `SwitchState`, and MUST NEVER reuse prior SwitchState / binding /
GoalEvidence as current truth (falsifiers F2, F3).

#### Scenario: valid evidence closes the settle

Given an eligible settle with the required state evidence valid on the first
fresh re-observation,
When the evidence is re-evaluated,
Then the settle stops and verification continues with that evidence — the action
is verified truthfully, no assumed success.

#### Scenario: missing evidence continues until budget

Given an eligible settle whose fresh re-observations keep returning null,
When the retry budget is not yet exhausted,
Then the settle continues with strictly fresh observations (sequence advances on
each retry) until valid evidence or the budget (falsifiers F6, F10).

#### Scenario: no stale truth reuse

Given a settle retry,
When fresh evidence is evaluated,
Then no prior SwitchState, binding, or GoalEvidence is reused as current truth
(falsifier F3).

### Requirement: Stopping rule is D. HYBRID

The settle MUST stop on the FIRST fresh observation yielding valid state evidence
(True/False). Opposite-state evidence also stops and flows through the existing
contradiction/failure semantics. No stable-consecutive requirement is imposed for
the transient toggle-animation window.

#### Scenario: first valid frame stops

Given a toggle-animation window followed by a stable frame with valid state
evidence,
When the settle re-observes,
Then it stops at the first valid frame and does not over-purchase additional
temporal filtering.

#### Scenario: opposite evidence stops truthfully

Given a fresh observation yielding the opposite state,
When the settle evaluates it,
Then the settle stops and the existing contradiction/failure semantics are
preserved (falsifier F2: no assumed success).

### Requirement: Budget and timing are COMPOSITION_POLICY

The settle budget MUST be bounded and frozen as composition policy, not semantic
contract: maximum re-observation count, delay policy (evidence-evaluating, small,
initial values from the measured toggle-animation window, not a copy of the
navigation 500ms), and maximum additional verification duration (bounded by count
× delay). No unbounded retry; no interaction with `MaxAssistanceConsults`.

#### Scenario: budget is bounded

Given the settle policy,
When its re-observation count is inspected,
Then it has a finite maximum (initial 3) and a bounded maximum additional
verification duration.

#### Scenario: no assistance coupling

Given the settle policy,
When its dependencies are inspected,
Then it does not depend on Assistance and does not change `MaxAssistanceConsults`
(falsifier F8).

#### Scenario: policy tuning without contract change

Given COMPOSITION_POLICY classification,
When budget/delay values are tuned,
Then no semantic contract or spec change is required.

### Requirement: Action scope is B — state-changing actions with missing post-action state evidence

The settle MUST apply to state-changing SemanticActions whose post-action state
evidence is missing (narrowest repository-evidenced scope). Non-state-changing
actions (navigation, tap-only) MUST keep existing behavior.

#### Scenario: narrowest evidenced scope

Given the settle scope,
When it is inspected,
Then it covers state-changing actions with missing post-action state evidence and
nothing broader (falsifier F7-adjacent: no generalization to all actions).

### Requirement: Failure semantics — same truthful terminal

Budget exhausted MUST produce the SAME truthful `StateEvidenceRequired` terminal
as today. It MUST NOT be converted into assumed success, contradiction, model
consultation, or guessed state (falsifiers F2, F9).

#### Scenario: budget exhaustion is truthful

Given an eligible settle whose budget is exhausted without valid evidence,
When the settle ends,
Then the existing truthful `StateEvidenceRequired` terminal is produced — never
assumed success or a fabricated state (falsifier F9).

### Requirement: Freshness is guaranteed by the observation source

Every settle retry MUST call `ObserveAsync` and consume the strictly advanced
`Observation.SequenceNumber`. No retry MAY reuse a prior observation.

#### Scenario: sequence strictly advances

Given a settle retry sequence,
When the observations are compared,
Then each retry consumes an observation whose `SequenceNumber` strictly advances
(falsifier F10).

### Requirement: L1 relationship is frozen

`L1_ASSISTANCE_EXPANSION_NOT_JUSTIFIED`. The settle MUST close normal state
transitions locally (L0 closes locally). The repair MUST NOT route post-action
state-evidence gaps through Assistance, and MUST NOT force successful local
evidence recovery through L1.

#### Scenario: local closure without L1

Given a normal state transition with a transient evidence gap,
When the settle runs,
Then the transition closes locally via bounded re-observation — no Assistance
consultation is introduced for this gap (falsifier F8).

### Requirement: Behavior-preserving guarantees

The settle MUST NOT change navigation-settle behavior, `StateEvidenceRequired`
semantics, Trap/Recovery, completion/GoalEvidence, drift/popup handling, or the
wire contract; with no assistance provider and no settle budget the runtime
behaves exactly as today.

#### Scenario: navigation settle unchanged

Given the settle change,
When navigation transitions are exercised,
Then `NavigationTransitionSettle` behavior is unchanged (falsifier F5: no
time-as-evidence regression).

#### Scenario: baseline behavior preserved without the settle

Given the pre-settle runtime shape,
When no settle budget/eligibility applies,
Then runtime behavior is exactly as today (zero regression).

## MODIFIED Requirements

None. This change modifies no existing spec or implementation.
