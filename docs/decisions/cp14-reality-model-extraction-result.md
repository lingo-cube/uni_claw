# PROJECT_LEADER_CP14_REALITY_MODEL_EXTRACTION_RESULT

> Status: `READY_FOR_INDEPENDENT_VALIDATION` — candidate only, not admitted
> Date: 2026-08-10
> DevelopmentLane: `SEMANTIC_DISCOVERY`
> Role: Project Leader / Reality Model Author
> Scope: Reality Model extraction and admission preparation only

## Decision Boundary

This artifact extracts an implementation-independent candidate Reality Model
for CP-14. It does not purchase or design a Planner, IntentEngine, Task IR,
Compiler, FSM / State Machine, Graph, LLM/VLM/provider, or Runtime change.

Any mention of “Compiler” is an explicit boundary exclusion or Legacy Mechanism
Context only. It is not a proposed solution and is not required for this model
to remain valid.

The Reality Model Admission Contract was adopted by
`HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` on 2026-08-09, as recorded in
`docs/decisions/reality-model-admission-contract-gate.md`. This document remains
a B2 extraction result, not an admission receipt; the final admission authority
is `docs/decisions/b4-reality-model-admission-rm11-result.md`.

## Authoritative Inputs and Evidence Boundary

The extraction uses the repository-backed CP-14 pressure, the existing intent
transformation corpus, the traversal supplement, U1's explicit CP-14 boundary,
and the current Reality Model Admission Contract. The requested logical input
names `RESEARCH_CP14_INTENT_TRANSFORMATION_EVIDENCE_AND_FALSIFYING_SCENARIOS_RESULT`
and `FS-CP14-001..004` were not present as literal standalone artifacts in this
checkout. No claim below is sourced solely from those absent names; the four
falsification conditions are registered here for independent validation to
reconcile against the completed research result.

Repository-backed evidence used:

- CP-14 and RD-07 in `docs/decisions/unified-legacy-scenario-pressure-portfolio.md`;
- TE-01, TE-02, TE-03, TE-04, TE-05, TE-07, TE-08, and TE-09 in
  `docs/decisions/legacy-traversal-plan-abstraction-supplement.md`;
- E-05, E-14, E-15, and E-18 in
  `docs/decisions/legacy-high-value-evidence-set-step2.md`;
- normalized intent transformation boundaries in
  `docs/decisions/legacy-normalized-evidence-step3.md`;
- E-05 / E-14 / E-15 / E-16 / E-18 reality-distinction analysis in
  `docs/decisions/legacy-reality-distinction-step4.md`;
- U1's current structured Goal + Plan boundary and zero-work / work-required
  outcomes in `docs/decisions/u1-wifi-minimum-usable-agent-slice-result.md`;
- existing RM corpus and deduplication rules in
  `docs/decisions/b4-reality-model-admission-result.md` and
  `docs/system/reality-model-admission-contract.md`.

## RealityModelCandidate

```text
RM-11 — Intent / Execution Representation Separation
PrimaryCP: CP-14 — Task Intent Must Not Be Conflated With Execution Method
Status: CANDIDATE_READY_FOR_INDEPENDENT_VALIDATION
Admission: NOT_YET_ADMITTED
```

### Core Reality Distinction

The high-level description of desired work, its goal, scope, constraints, and
completion condition is not identical to the concrete actions, targets, route,
or work inventory that can be known or required in the current world.

The distinction is bidirectional:

```text
Intent / Goal / Scope / Constraints
        !=
Concrete method / route / targets / work inventory
```

This does not mean that concrete method constraints are invalid. An explicit
method may itself be part of the task boundary. The model must preserve both
legitimate classes:

- open-world representation: categories, scope, safety, depth, and completion
  constraints are known while concrete work is discovered from observation;
- closed-world representation: concrete targets and actions are explicitly
  prescribed because the method is part of the task.

## WorldFacts

All facts below are implementation-independent and are stated as claims about
the task/execution situation, not about a legacy component or a Runtime type.

| ID | Support | Temporal scope | World fact | Evidence |
|---|---|---|---|---|
| `WF-CP14-01` | `DIRECT` | Before execution | A task can specify interaction categories, boundaries, safety constraints, and a completion condition without enumerating every concrete page, element, target, coordinate, or route. | OB-CP14-01/03/04; TE-01 (E1), TE-03 (E1), TE-08 (E3), E-05 (E1) |
| `WF-CP14-02` | `DIRECT` | During observation sequence | Fresh observations can reveal concrete work items that were not present in the pre-execution representation. | OB-CP14-01/04/07; TE-01 (E1), TE-04 (E1), TE-08 (E3) |
| `WF-CP14-04` | `DIRECT` | Before and during execution | A closed-world task can explicitly contain concrete actions and targets; its prescribed route may be the method being requested, rather than merely an implementation choice. | OB-CP14-02; TE-02 (E2), E-16 (E1) |
| `WF-CP14-05` | `DIRECT` | Current world at task start | One U1 task-start observation shows the WiFi control ON; another task-start observation shows it OFF. | OB-CP14-06; U1 result (E1 deterministic production-shaped simulation) |
| `WF-CP14-06` | `INFERRED` | Across task transformations | A task's scope defines which discovered work is legitimate; it does not enumerate the complete work inventory that observation will reveal. | RI-CP14-02 (HIGH); OB-CP14-01/04/07; TE-04 (E1), TE-05 (E1), TE-08 (E3); TRD-02 |
| `WF-CP14-07` | `INFERRED` | At intent intake | A vocabulary-valid or structurally complete intent representation can still be semantically wrong or incomplete with respect to the user's desired scope, target, or completion condition. | RI-CP14-06 (MEDIUM); OB-CP14-08; E-15 (E1), E-18 (E0); legacy reality-distinction analysis |

`WF-CP14-03` was removed from the core World Fact set by B4 minimality review.
Its supporting record remains as `OB-CP14-05`; it does not independently prove
that an identical intent encountered a different current world.

`WF-CP14-07` is intentionally weaker than the other core facts because E-18 is
document-only and E-15's semantic-error cases are extraction evidence, not
direct external-world observations. It must not be promoted to a stronger
grade without independent validation.

## Legacy Mechanism Context (Non-Normative)

The source corpus uses legacy labels such as `AllVisited`, `IsEnd`,
`PlanCompiler`, `IntentExtractor`, `TraversalPlan`, `FSM`, and `Graph`. They are
retained only to identify evidence provenance and historical failure reports.
They are not World Facts, Reality Inferences, Expected Requirements, or a
proposed architecture for RM-11. The same candidate remains meaningful if all
of these mechanisms are replaced.

## Observations

Observation records preserve what the evidence artifact records. They do not
turn a legacy completion verdict into world truth. The legacy vocabulary remains
outside the candidate's normative facts and requirements.

| ID | Observation record | Evidence / strength |
|---|---|---|
| `OB-CP14-01` | The pre-execution representation contains type rules and completion/boundary conditions, while concrete pages, elements, coordinates, and total route are listed as unknown until observation. | TE-01 — E1 deterministic simulation |
| `OB-CP14-02` | A concrete representation contains explicit coordinates, expected page identities, and an action sequence; no new concrete candidate is discovered during execution. | TE-02 — E2 integration / emulator evidence |
| `OB-CP14-03` | An intent-shaped input is transformed into a type-level representation whose matching elements are supplied by later page observations. | TE-03, E-05 — E1 deterministic simulation; E-05 has opt-in integration variants |
| `OB-CP14-04` | A recorded run contains a type-level `plan.json` and observed element inventories of 16, 21, and 14 items across successive pages; the concrete inventory is not pre-enumerated in the plan artifact. | TE-08 — E3 recorded-reality-derived reproduction |
| `OB-CP14-05` | Two target-locate cases with the same type-level shape reach different page sets and step counts. | TE-07 — E1 deterministic simulation |
| `OB-CP14-06` | U1's two task-start observations show the WiFi control ON in one fixture and OFF in another. | U1 result — E1 deterministic production-shaped simulation |
| `OB-CP14-07` | In the multi-branch case, two in-scope branches are observed but only one is dispatched while the legacy record reports completion. | TE-04 / E-07 — E1 deterministic failing evidence; legacy verdict retained only as context |
| `OB-CP14-08` | Intent extraction validates allowed vocabulary but can produce a semantically wrong scope; the documented Python parsing path has no C# production equivalent. | E-15 — E1 production implementation evidence; E-18 — E0 document-only gap evidence |
| `OB-SYS-CP14-01` | The U1 ON fixture records zero action dispatches; the OFF fixture records navigation and switch dispatches before the final observation. | U1 result — E1 deterministic production-shaped simulation |

## RealityInferences

| ID | Inference | Method | Confidence | Alternatives | Materiality | Evidence |
|---|---|---|---|---|---|---|
| `RI-CP14-01` | Intent / Goal is not equivalent to concrete Execution Method. | deduction from observations | HIGH | A task may explicitly constrain method; that is a legitimate closed-world case, not evidence that all intent is method. | HIGH | OB-CP14-01..06; CP-14 / RD-07 |
| `RI-CP14-02` | TaskScope is a boundary over legitimate work, while ConcreteWorkInventory is populated by current observations within that boundary. | deduction from observations | HIGH | In a closed-world task, the requested inventory may already be explicit; the distinction still matters for open-world tasks. | HIGH | OB-CP14-01, 04, 07; TRD-02 |
| `RI-CP14-03` | TypeLevelTraversalSpecification is not a ConcreteFutureRoute. | deduction from observations | HIGH | A concrete route can be supplied when the task deliberately requests closed-world execution. | HIGH | OB-CP14-01..05; TRD-01 |
| `RI-CP14-04` | The same declared goal may yield zero or non-zero concrete work when current world observations differ. | deduction from observations | HIGH | Different work can still be required if the task includes an explicit method constraint. | HIGH | OB-CP14-06; OB-SYS-CP14-01; U1 (E1) |
| `RI-CP14-05` | The two representation classes are both legitimate and must not be collapsed into one mandatory representation. | deduction from observations | HIGH | A future product may expose one user-facing entry point, but its reality contract must preserve the distinction internally or at the boundary. | HIGH | OB-CP14-01..05; TE-02 (E2), TE-01/03 (E1), TE-08 (E3); portfolio CP-14 |
| `RI-CP14-06` | Structural validity or vocabulary validity of an intent does not prove that the intended scope, target, or desired state is correct. | AI output | MEDIUM | A separately supplied authoritative context may resolve the missing facts; the current evidence does not establish such a source for every case. | HIGH | OB-CP14-08; E-15 (E1); E-18 (E0) |
| `RI-CP14-07` | Completion cannot be inferred from representation exhaustion alone when concrete work is discovered during execution. | deduction from observations | HIGH | Closed-world tasks may have an explicit action list, but world-effect and goal evidence remain separate requirements. | HIGH | OB-CP14-07; TE-04 (E1); RM-02/RM-03 cross-cutting evidence |

## ExpectedRequirements

These are requirements for any future capability that crosses the CP-14
boundary. They do not authorize an implementation shape.

| ID | Expected requirement | Core / derived | Falsification oracle |
|---|---|---|---|
| `ER-CP14-01` | Preserve `Intent != ExecutionMethod`; a high-level desired task must not be treated as a fixed action sequence unless the task explicitly supplies that method constraint. | CORE | A valid open-world task is rejected because it lacks a pre-enumerated route, or a fixed route is silently treated as optional when method was explicitly requested. |
| `ER-CP14-02` | Preserve `Goal != ExecutionMethod`; current goal evidence may make the required concrete work empty, while an unsatisfied current world may require work. | CORE | Same goal and same method are always dispatched regardless of current goal evidence, or a method is treated as proof of goal satisfaction. |
| `ER-CP14-03` | Declared task scope must remain semantically distinct from observation-populated concrete work inventory. Constraint enforcement remains RM-06 / CP-07 responsibility. | CORE | The system treats declared scope as a complete pre-execution inventory, requires pre-enumeration, or ignores newly observed in-scope work because it was absent from the initial representation. |
| `ER-CP14-04` | Preserve `TypeLevelTraversalSpecification != ConcreteFutureRoute`; type/category/constraint information must remain distinct from the route eventually selected in the current world. | CORE | A type-level specification is rejected without a concrete route, or the specification is treated as a guarantee of a particular future route. |
| `ER-CP14-05` | Preserve both `OpenWorldMode` and `ClosedWorldMode`; explicit method constraints may legitimately select closed-world execution. | CORE | The system forces every task into discovery, or forces every task to provide a concrete route when the task intentionally leaves instances open. |
| `ER-CP14-06` | Permit the same intent to produce different concrete work, routes, or zero work in different current worlds, while preserving the same declared intent and constraints. | CORE | A representation is rejected because its action count, route, or inventory differs from another valid execution of the same intent. |
| `ER-CP14-07` | Ambiguous or incomplete intent must remain explicitly unresolved; it must not silently create missing authority, a missing target, a missing scope, or a desired world state. | CORE | Missing or ambiguous fields are filled by an unapproved default and execution proceeds as if the user had supplied the missing meaning. |
| `ER-CP14-08` | Completion must use evidence appropriate to the task's goal, scope, and world state, not merely exhaustion of a representation or action list. | DERIVED / cross-cutting | The system reports completion after representation exhaustion while in-scope work or required world evidence remains unproven. |

## Falsification Conditions / FS-CP14-001..004

These are minimum falsifying conditions for independent validation. They are
not implementation tasks and do not authorize a particular capability.

### FS-CP14-001 — Same Intent, Different Current World

- Keep the high-level intent and explicit constraints constant.
- Present one world where the goal is already evidenced and another where it is
  not evidenced.
- Expected reality distinction: concrete work may be zero in the first world
  and non-zero in the second.
- Falsify RM-11 if the model requires the same concrete work in both worlds or
  treats the method itself as goal evidence.
- Evidence anchor: U1 already-ON vs OFF → ON; CP-06 / RM-03 boundary.

### FS-CP14-002 — Open-World Discovery Without Pre-Enumeration

- Supply scope, type/category constraints, safety constraints, and completion
  conditions without listing concrete pages, elements, or route.
- Let fresh observations reveal previously unknown in-scope work.
- Expected reality distinction: discovered work is eligible and must affect the
  completion evidence without being outside the declared boundary.
- Falsify RM-11 if execution requires a concrete pre-execution inventory or
  ignores valid work because it was absent from the initial representation.
- Evidence anchor: TE-01, TE-03, TE-04, TE-08; TSP-01/TSP-02.

### FS-CP14-003 — Closed-World Method Constraint

- Supply a task whose explicit requirement includes concrete targets/actions or
  a concrete route.
- Expected reality distinction: the concrete method is a legitimate task
  constraint and may fail when current world layout does not correspond; it must
  not be silently converted into open-world discovery.
- Falsify RM-11 if every explicit route is treated as merely optional, or if
  route mismatch is hidden as successful goal completion.
- Evidence anchor: TE-02, E-16, Plan-mode evidence.

### FS-CP14-004 — Ambiguous or Incomplete Intent

- Omit or ambiguously specify target, scope, authority, completion condition, or
  desired world state.
- Expected reality distinction: ambiguity remains observable and unresolved;
  no missing authority or desired state is silently invented.
- Falsify RM-11 if the system chooses an unapproved target/scope/authority or
  executes toward an inferred desired state without evidence.
- Evidence anchor: E-15 vocabulary-valid semantic error; E-18 provenance gap;
  independent research artifact reconciliation required.

## ClosedWorldMode

`PRESERVED`.

An explicit method constraint is a valid part of the task boundary. A
closed-world representation may name concrete targets, actions, route, expected
page identity, or method-specific limits. It remains subject to world
correspondence and effect verification; method prescription is not proof that
the world already satisfies the goal.

## OpenWorldMode

`PRESERVED`.

An open-world representation may specify only categories, scope, safety,
depth/boundary constraints, target/completion semantics, and entry conditions.
Concrete work, coordinates, pages, and route can be supplied by fresh
observation. The representation must constrain what is permissible without
pretending to know every future instance.

## AmbiguousIntentBehavior

Ambiguous or incomplete intent is a distinct unresolved reality condition. The
system must preserve the ambiguity and must not silently manufacture a target,
scope, decision authority, permission, completion condition, or desired world
state. This requirement does not choose whether the eventual boundary response
is clarification, rejection, deferral, or another already-authorized policy;
that behavior is outside this Reality Model extraction.

## StateMachinePressure

```text
Classification: NO_STATE_PRESSURE
```

TE-01/TE-04/TE-08 and U1 show changing observations, discovered work, dispatch
records, and completion evidence. They do not establish a missing lifecycle
state, transition, persistent state semantic, or architecture boundary. Those
relations remain covered by the existing RM-02/RM-03 corpus and Runtime
semantics. The former `STATE_SEMANTIC_PRESSURE_CANDIDATE` is retained only as a
rejected, non-normative research hypothesis; it creates no FSM or State Machine
work.

## CP Boundaries and Duplicate Audit

### Primary and secondary pressure relation

- **Primary:** CP-14 — Task Intent Must Not Be Conflated With Execution Method.
- **Secondary / supporting:** CP-03 (Plan Validity != Execution Success), CP-06
  (Goal Satisfaction), CP-07 (declared boundary vs enforced boundary), CP-04
  (discovered branch completion), and CP-09 (unchanging-content termination).

The secondary relations are not new CP registrations. They identify existing
models whose requirements help validate RM-11's boundary.

### G5 duplicate audit

| Existing model | Shared material | Why RM-11 is not a duplicate | Outcome |
|---|---|---|---|
| RM-02 Multi-Branch Hub | discovered work, branch coverage, scope vs inventory | RM-02's primary world cluster is sibling-branch progress and revisit preservation; RM-11's primary cluster is representation independence across task modes and current worlds | No merge; cross-reference |
| RM-03 Goal Satisfaction | goal evidence independent of plan length | RM-03 covers recognizing an already-satisfied goal; RM-11 generalizes the distinct relation between goal/intent and concrete method across zero, variable, and explicit-method cases | No merge; cross-reference |
| RM-06 Depth Bound | type-level specification vs concrete future route | RM-06's primary oracle is depth-bound enforcement; RM-11's primary oracle is intent/method separation and preservation of open/closed modes | No merge; cross-reference |
| RM-05 Navigation Change | method dispatch vs observed page change | RM-05 concerns a concrete navigation effect; RM-11 concerns whether a concrete method is entailed by intent at all | No merge; cross-reference |

Existing RM-02/RM-03/RM-06 therefore provide partial evidence but do not serve
CP-14's independent primary oracle. Removing RM-11 would leave CP-14 without a
standalone model for the representation distinction.

### Novelty result

```text
Same world-fact cluster as an accepted RM: NO
Same primary CP/oracle as an accepted RM: NO
CP-14 coverage without candidate: NO
Candidate novelty test: PASS
```

## Evidence Strength and Deferred Evidence

### Current strength

- Core distinction: `HIGH` confidence, with E1 multi-source executable
  simulation plus E2 closed-world integration and E3 recorded-reality-derived
  type-level evidence.
- Open-world inventory facts: E3 is the strongest direct corpus anchor (TE-08),
  with E1 corroboration from TE-01/03/04.
- Closed-world method facts: E2 TE-02 plus E1 corroboration; the claim is that
  the task class exists, not that its route succeeds in every world.
- Zero-work vs non-zero-work relation: E1 current U1 evidence; it is a
  production-shaped composition result, not live external-world evidence.
- Ambiguity / semantic extraction risk: E1 for E-15 implementation behavior and
  E0 for E-18's documented missing path; `MEDIUM` confidence pending direct
  independent falsification.

### Deferred evidence register

1. Attach and independently verify the completed CP-14 research result and
   literal FS-CP14-001..004 artifacts, if they exist outside this checkout.
2. Upgrade TE-08 from E3 reconstructed recorded reality to E4 only if the
   source run artifacts are present and provenance is re-certified.
3. Add an independent executable scenario where identical intent yields zero
   vs non-zero work from different current worlds, rather than relying only on
   U1's current deterministic composition evidence.
4. Independently exercise incomplete/ambiguous intent with an explicit oracle
   that detects fabricated target, scope, authority, or desired state.
5. StateMachinePressure was resolved as `NO_STATE_PRESSURE`; no FSM design or
   state-semantic work is authorized by RM-11.
6. E-18 remains E0 and is not evidence that a deterministic parser is required;
   it records a provenance/capability gap only.

## AdmissionRecommendation

```text
ACCEPT_NEW_MODEL
```

Recommendation is conditional on independent validation, Dedup Arbiter review,
Architecture Neutrality review, and the governing Reality Model Admission
Contract being adopted. This result does not itself admit RM-11 or authorize
any capability implementation.

## NextTask

```text
PROJECT_LEADER_CP14_REALITY_MODEL_INDEPENDENT_VALIDATION
```

The validator must independently verify the four falsification conditions,
reconcile the named research/FS artifacts, rerun the G5 audit, check the
implementation-independence rewrite, and issue `PASS`, `CONDITIONAL_PASS`, or
`FAIL` without designing a Compiler, Planner, FSM, or Runtime change.

## Output

```text
PROJECT_LEADER_CP14_REALITY_MODEL_EXTRACTION_RESULT

RealityModelCandidate: RM-11
PrimaryCP: CP-14
WorldFacts: WF-CP14-01, WF-CP14-02, WF-CP14-04..07 (6; WF-CP14-03 removed by G4)
Observations: OB-CP14-01..08 + OB-SYS-CP14-01
RealityInferences: RI-CP14-01..07
ExpectedRequirements: ER-CP14-01..08
ClosedWorldMode: PRESERVED
OpenWorldMode: PRESERVED
AmbiguousIntentBehavior: unresolved; no missing authority or desired state may be silently invented
StateMachinePressure: NO_STATE_PRESSURE
AdmissionRecommendation: ACCEPT_NEW_MODEL
NextTask: PROJECT_LEADER_CP14_REALITY_MODEL_INDEPENDENT_VALIDATION
STOP.
```
