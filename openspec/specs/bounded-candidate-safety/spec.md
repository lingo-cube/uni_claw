# bounded-candidate-safety Specification

## Purpose
TBD - created by archiving change phase3-bounded-candidate-safety. Update Purpose after archive.

## Requirements

### Requirement: Represent bounded candidate authorization evidence
The Runtime SHALL add exactly one immutable `CandidateAuthorizationEvidence` production value with `bool? Authorized` and non-empty `string Reason` fields. `true` SHALL mean fresh evidence positively authorizes the supplied candidate under the bounded read-only intent, `false` SHALL mean fresh evidence positively rejects it, and `null` SHALL mean the evidence is insufficient and grants no authorization. The value SHALL NOT represent dispatch, world effect, required-work membership, or Goal completion.

#### Scenario: Three authorization outcomes remain distinct
- **WHEN** bounded candidate evidence positively permits, positively rejects, or cannot determine safe authorization
- **THEN** the evidence value represents the outcomes as `true`, `false`, or `null` respectively and supplies a non-empty deterministic reason

### Requirement: Carry one optional authorization criterion on Goal
The Runtime SHALL add exactly one optional immutable `Goal.CandidateAuthorizationEvaluator` field with semantic shape `Func<Observation, ObservedElement, CandidateAuthorizationEvidence>?`. The evaluator SHALL be deterministic, side-effect-free, depend only on its supplied fresh Observation and a candidate contained in that Observation, and SHALL NOT read or mutate Runtime owners, call Environment, dispatch actions, or set RunState.

#### Scenario: Existing fixed-Plan Run remains compatible
- **WHEN** a Goal has no candidate authorization evaluator
- **THEN** the existing fixed-Plan execution behavior remains unchanged and no newly discovered non-preauthorized candidate receives authorization

#### Scenario: Criterion consumes fresh candidate evidence
- **WHEN** the evaluator receives a fresh Observation and one of its contained ObservedElements
- **THEN** it produces one deterministic `CandidateAuthorizationEvidence` result without performing external work

### Requirement: Keep Agent as the sole semantic authorization authority
Agent SHALL evaluate the bounded Goal criterion and decide whether a newly observed candidate may enter local execution. Traversal SHALL NOT independently decide semantic authorization and SHALL receive only a candidate already authorized by Agent. Traversal MAY still reject local grounding, precondition, or execution protocol failures, but such mechanical rejection SHALL NOT authorize another candidate or override an Agent denial.

#### Scenario: No duplicate authorization authority
- **WHEN** Agent rejects or cannot authorize a candidate
- **THEN** Traversal never receives that candidate and no lower scope independently reverses the outcome

### Requirement: Record rejected and unresolved candidates before dispatch
For every candidate whose authorization result is `false` or `null`, Agent SHALL append existing Trace evidence identifying the candidate, source Observation sequence, outcome, and non-empty reason before any action dispatch. The Trace event SHALL have no Action or ActionId for that candidate. Agent SHALL NOT create a Trap merely for ordinary bounded authorization denial.

#### Scenario: Destructive evidence overrides navigation-like evidence
- **WHEN** a navigation-like candidate also carries destructive text/evidence and the bounded evaluator returns `false`
- **THEN** Agent records rejected pre-dispatch Trace evidence and the candidate produces zero Traversal dispatches and zero Environment actions

#### Scenario: State-changing evidence needs no dangerous keyword
- **WHEN** an observed candidate has state-changing evidence such as non-null SwitchState and the bounded read-only evaluator returns `false`
- **THEN** Agent records rejection and dispatches no action for that candidate

#### Scenario: Insufficient evidence defaults to non-execution
- **WHEN** the evaluator returns `null` for an observed candidate
- **THEN** Agent records unresolved non-authorization, dispatches no action, and does not fabricate success or completion

### Requirement: Permit one authorized safe navigation candidate through existing execution
For the bounded SC-P3-CAND-006 classification round, Agent MAY deterministically nominate the first candidate whose result is `true` as one safe navigation Tap. The authorized candidate SHALL enter the existing Container/Traversal Select → Execute → Observe → Verify path. Authorization SHALL NOT itself prove dispatch success, world effect, local completion, or Goal completion.

#### Scenario: Authorized safe navigation executes normally
- **WHEN** fresh bounded evidence authorizes safe navigation candidate S
- **THEN** Agent may send S through existing Tap execution and Runtime obtains fresh post-action Observation before any completion judgement

### Requirement: Keep observed candidates separate from required safe work
Observation membership and authorization SHALL NOT independently add a candidate to approved required-work inventory. Rejected or unresolved candidates SHALL NOT become unfinished approved safe branches merely because they were visible. Required work SHALL remain defined by Agent-owned Goal/approved scope and evidence-backed progress, and final Run completion SHALL remain dependent only on satisfied GoalEvidence.

#### Scenario: Denied dangerous candidate does not block honest completion
- **WHEN** D is visible but rejected while all actually approved safe work has valid completion evidence
- **THEN** D remains accounted as rejected evidence rather than unfinished safe work and does not independently prevent or cause Goal completion

### Requirement: Replay bounded candidate safety deterministically
The Runtime SHALL produce deterministic SC-P3-CAND-006 evidence when RunId, fresh Observation, candidate values, bounded evaluator, Goal, world input, and action sequence are equal.

#### Scenario: Equal bounded inputs replay equally
- **WHEN** safe, destructive, state-changing, or unresolved branches run twice with equal inputs
- **THEN** authorization outcomes/reasons, Trace evidence, Traversal journal, ActionHistory, Observations, GoalEvidence, and final RunState are equal

### Requirement: Preserve the approved production and architecture boundary
SC-P3-CAND-006 SHALL add exactly one immutable production type and exactly three production fields total: two fields on `CandidateAuthorizationEvidence` and one optional field on Goal. It SHALL add no enum, interface, component, mutable-state owner, Trace/journal/Trap field, action variant, or Recovery behavior. Ownership and authority SHALL remain unchanged.

#### Scenario: Deferred safety frameworks remain absent
- **WHEN** all SC-P3-CAND-006 branches pass
- **THEN** no SafetyManager, RiskEngine, policy/rule engine, SafeActionExecutor, authorization manager, RiskLevel, Confidence, policy hash, coordinates, Fingerprint, Vision/VLM judgement, navigation graph/stack, dynamic planner, candidate-discovery framework, universal interceptor, mutable safety owner, Capstone implementation, Harness change, or Runtime refactor has been introduced
