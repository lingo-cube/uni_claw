# Runtime Internal Componentization Challenge

> Date: 2026-08-11
> Baseline: `RUNTIME_INTERNAL_CONSOLIDATION_VALIDATED` + `AGENT_SEMANTIC_CLOSED_LOOP — VALIDATED`
> Mode: Architecture challenge only; no production behavior change

## 1. Verdict

`READY_FOR_EXTRACTION`

The macro architecture remains correct:

```text
Agent -> Container -> Traversal -> Environment
```

Centralized authority and state ownership remain frozen. The current implementation nevertheless
contains a small set of pure algorithms that can be separated without creating another authority or
mutable owner. No new interface is approved by this challenge: the current immutable contracts are
enough for pure-function extraction, while the two apparent replacement seams still have unresolved
contract/safety debt that must not be hidden inside a refactor.

## 2. Cohesion test

### 2.1 Agent

- **ResponsibilityCount: 8**
  1. run lifecycle/orchestration;
  2. semantic outcome adjudication;
  3. capability selection;
  4. action-authorization commit;
  5. GoalEvidence completion/failure commit;
  6. recovery admission, rebind and resume coordination;
  7. bounded open-world policy and cross-Container progress;
  8. trace/journal result translation and shared bookkeeping.
- **MutableStateOwned: 12 fields** — trace, action counter, recovery counter, RunState,
  WorldBelief, active Container, reason, RecoveryAnchor, last Trap, branch-progress snapshot,
  suspended step index, suspended Container.
- **AuthorityDecisions: 8** — lifecycle transition, terminal state, world-belief acceptance,
  active/rebound Container, semantic satisfaction/failure, capability acceptance, action authorization,
  recovery/open-world continuation policy.
- **ReplaceableAlgorithmsInline: 1 material candidate** — exact capability matching. The semantic
  decision ladder and completion commit are authority, not replaceable mechanisms.
- **ExtractionCandidates:** none beyond the already extracted `ActionAuthorizer`; keep
  `ResolveSemanticObject` and `SelectCapability` private until their policies become non-trivial.
- **KeepTogether:** `RunSemanticGoalAsync` decision ladder, `Complete`/`Fail`, recovery adjudication,
  open-world parent/sibling/completion policy.

Classification:

| Responsibility | Class | Decision | Reason |
|---|---|---|---|
| Run orchestration | D — Owner/coordinator | KEEP | Owns RunState and trace commits. |
| SemanticDecisionPolicy | D — Owner/coordinator | KEEP | Extracting the outcome ladder would move Agent authority. |
| CapabilitySelector | A — Pure function | KEEP PRIVATE | Current exact matching is small and has no second policy. |
| ActionAuthorizer | B — Internal stateless component | KEEP EXTRACTED | Immutable validation only; Agent commits the result. |
| CompletionEvaluator | A — Pure function candidate | REJECT NOW | Current comparison is trivial; `Complete` authority stays Agent-owned. |
| Mechanical evidence refresh | A/B private mechanism | KEEP PRIVATE | It composes existing analyzers and Container atomic apply without owning state. |

### 2.2 Container

- **ResponsibilityCount: 9** — observation holding, page identity/belief, object-binding state,
  object-state belief, continuity acceptance, viewport/progress state, step delegation, step-result
  bookkeeping, atomic semantic snapshot mutation.
- **MutableStateOwned: 7 fields** — observation, executed steps, viewport observations, local-complete
  flag, local page belief, object bindings, object-state beliefs.
- **AuthorityDecisions: 4** — local observation acceptance, local continuity, local progress mutation,
  atomic replacement of the page-local semantic snapshot.
- **ReplaceableAlgorithmsInline: 2** — observation/binding-to-state-belief reduction and continuity
  predicates. Only the first has a clean immutable boundary today.
- **ExtractionCandidates:** `StateBeliefReducer` as a pure function.
- **KeepTogether:** `Bind`, `RefreshSemanticSnapshot`, continuity acceptance/mutation, progress and
  step-result commits.

Candidate decisions:

| Candidate | Class | Input | Output | State | Authority | Decision |
|---|---|---|---|---|---|---|
| StateBeliefReducer | A — Pure function | Observation + immutable ObjectBindings | immutable object-state-belief dictionary | none | none | EXTRACT |
| BindingReconciler | A — Pure function | SemanticEvidence + SemanticObjects | immutable ObjectBindings | none | none | EXTRACT from `BindingAnalysis` |
| ContinuityEvaluator | B candidate | current/candidate evidence + identity predicate | qualitative continuity result | none | none | DEFER; current evaluation and atomic acceptance are tightly paired |
| SemanticSnapshotBuilder | D-like facade | observation + all semantic evidence | composite snapshot | would blur ownership | risks duplicate decision | REJECT |

`Container` remains the sole owner that atomically applies the reducer output.

### 2.3 Traversal

- **ResponsibilityCount: 9** — legacy target selection, criterion grounding, precondition/receipt
  checking, protocol-token lowering, semantic-action lowering, dispatch, retry/re-observe, fresh-result
  verification, journal bookkeeping.
- **MutableStateOwned: 2 fields** — journal and step counter. Environment and retry limit are immutable
  dependencies/configuration.
- **AuthorityDecisions: 5** — local target selection, retry boundary, execution-token validity,
  freshness/post-action verification, step result/journal commit.
- **ReplaceableAlgorithmsInline: 3** — target grounding, semantic action lowering, string protocol
  parsing. Freshness checks are protocol-specific and not yet one common algorithm.
- **ExtractionCandidates:** `SemanticActionLowerer`; later `TargetGrounder` after its safety debt is
  resolved.
- **KeepTogether:** dispatch -> observe -> verify -> journal protocol, retry count/state, TimedOut
  observation behavior.

Candidate decisions:

| Candidate | Class | Input | Output | State | Authority | Decision |
|---|---|---|---|---|---|---|
| TargetGrounder | B — Internal stateless | step, fresh observation, candidates, criterion, immutable authorization receipts | selected index or explicit failure | none | no business authority | EXTRACT ONLY AFTER safety gate |
| SemanticActionLowerer | B — Internal stateless | SemanticAction + ObjectBinding + Observation | SemanticActionResult | none | none; Agent already authorized | EXTRACT |
| ResultVerifier | A candidate | source/post observations + criterion | local verification evidence | none | Traversal commits result | KEEP INLINE; path-specific checks and messages differ |
| Plan action parser | A candidate | string action token + selected target | DeviceAction? | none | none | DEFER until string protocol has an approved typed replacement |
| Traversal protocol | D — Owner/coordinator | authorized local execution input | journal-backed TraversalStepResult | journal/counter | local protocol authority | KEEP |

### 2.4 Environment

- **ResponsibilityCount: 2** — observe the external world and dispatch a physical action.
- **MutableStateOwned:** none in `IEnvironment`; each external adapter may own its device/backend state.
- **AuthorityDecisions: 1 bounded authority** — report observation and dispatch outcome, never task or
  semantic success.
- **ReplaceableAlgorithmsInline:** none in the port.
- **ExtractionCandidates:** none.
- **KeepTogether:** backend-specific observation/action implementation in each external adapter.

`IEnvironment` is the one existing justified C-class replaceable port.

## 3. World and semantic analysis

`PageAnalysis` and `BindingAnalysis.Analyze` already follow the coherent contract:

```text
fresh Observation + immutable criteria -> immutable SemanticEvidence
```

They are stateless, deterministic for deterministic inputs, own no cache, mutate no Container, and
hold no semantic authority. `SemanticReconciliation` is also a pure evidence-to-belief function.

Two remaining pressures are distinct:

1. Object-state belief reduction is embedded in `Container.RefreshObjectStateBeliefs`. Extracting a
   pure `StateBeliefReducer` lets state-source rules change without modifying Container mutation
   lifecycle.
2. `BindingAnalysis.ReconcileBindings` extracts element indices from human-readable evidence `Reason`
   strings. This is compatibility debt. It should be isolated as `BindingReconciler`, but the parsing
   contract must not be promoted into a public replacement port.

Future `StateAnalysis` remains an upstream evidence producer. A rule detector, device accessibility
source, or VLM may populate observation/evidence, but Container retains belief ownership.

## 4. Planning

`IntentCompiler` and `IntentExecution` are two real responsibilities, not merely two filenames:

- `IntentCompiler`: deterministic `BusinessIntent -> IntentCompilationResult`; Model-only, stateless,
  and independent of current-world evidence.
- `IntentExecution`: validates/destructures an already resolved representation and forwards it to the
  existing Agent entry. It does not parse, plan, ground, observe, or complete a Goal.

No Planner, compiler engine, or `IIntentCompiler` is purchased. A future LLM-backed compiler can emit
the existing `IntentCompilationResult` and reuse `IntentExecution` without changing Agent.

## 5. Recovery

The decision/mechanism boundary is already explicit:

- Agent owns drift scope, recovery admission, rebind/resume, revalidation, and terminal RunState.
- `Recovery` owns the bounded mechanism session: recipe actions and cursor, physical dispatch through
  `IEnvironment`, fresh observation, verification invocation, and recovery-action resolution.

`Recovery` owns exactly two mutable mechanism fields (`_recipeActions`, `_recipeIndex`) and no Agent or
Container state. Its three injected functions are narrow replacement seams. New Recovery Planner,
policy interface, or state owner is rejected.

## 6. Replacement falsifiers

| Falsifier | Current result | Decision |
|---|---|---|
| R1 rule BindingAnalysis -> alternate analysis | **PARTIAL**: Agent authority need not change, but direct static invocation and Reason-string reconciliation mean replacement still touches composition and relies on a brittle contract. | Extract `BindingReconciler`; defer a port until structured binding proposals/evidence are approved. |
| R2 grounding algorithm -> alternate grounding | **BLOCKED BY SAFETY EVIDENCE**: candidate evaluators are injectable, but criterion retry currently falls back to legacy `Select`. | Add falsifier first; resolve through Safety Semantic Gate before TargetGrounder extraction. |
| R3 deterministic -> future LLM intent compilation | **PASS**: both can emit `IntentCompilationResult`; Agent consumes only SemanticGoalInput. | No interface now. |
| R4 environment backend replacement | **PASS** through `IEnvironment`. | Existing port sufficient. |
| R5 state detector/VLM evidence replacement | **PASS for Observation.SwitchState producers**: source can change upstream while Container remains owner. A direct SemanticEvidence VLM path remains future semantic work. | No Runtime port now. |

## 7. Safety debt discovered during challenge

When `TargetGroundingCriterion` is present and initial grounded selection returns no candidate,
`Traversal.ExecuteStepCoreAsync` enters retry and invokes legacy `Select(step, retryObs.Elements)`.
That retry does not re-run `CandidateEvaluator` or re-check the immutable authorization receipt for the
new candidate. Existing tests cover criterion grounding and retry independently, but not their
combination.

This is not an ownership or macro-architecture contradiction. It is an unproven safety edge and must
not be repaired silently as component extraction. The first continuation slice is therefore a
falsifier-only test and, if confirmed, a separate Safety Semantic Gate.

## 8. Coupling test

| Coupling | Classification | Evidence / decision |
|---|---|---|
| Agent constructor: 8 parameters, including 3 delegates and 2 optional semantic criteria | DEFER | Real pressure, but grouping into a facade/config object would hide authority and recreate a context object. |
| Container identity and executor delegates | KEEP | Narrow ownership-preserving seams; no interface needed. |
| Recovery parser/resolver/verifier delegates | KEEP | Real mechanism replacement seams with no state/authority transfer. |
| `PlanStep.ActionDescription` string protocol | DEFER | Typed replacement is a semantic Model purchase, not component extraction. |
| Recovery recipe/verification strings | DEFER | Frozen Phase-2 semantics; no evidence for a new policy model. |
| Binding indices encoded in `SemanticEvidence.Reason` | DEPRECATE | Isolate in `BindingReconciler`; require a future structured-evidence semantic gate before removal. |
| Agent direct access to Traversal journal tail | DEFER | Replacing it with a typed execution receipt changes cross-layer contracts. |
| `_resolveSemanticPage`, Container identity rule and PageAnalysis coexist | DEPRECATE | Legacy compatibility paths; do not remove until production evidence path fully replaces them. |
| Freshness checks in lowered and Plan-step paths | KEEP | Same predicate, but distinct protocol results, messages and verification scopes. |
| Traversal failure string -> SemanticRunResult translation | DEFER | A typed failure union is a semantic contract purchase. |
| Static global state | KEEP | None found. |
| Hidden Environment access | KEEP | Agent uses explicit observation delegate; Traversal/Recovery use `IEnvironment`. |

## 9. Component target map

```text
Planning
  IntentCompiler                     B existing stateless component
  IntentExecution                    B existing bridge

Agent                                D owner/coordinator
  private CapabilitySelector         A keep private
  ActionAuthorizer                   B existing internal stateless component
  SemanticDecisionPolicy             D remains Agent code
  Completion authority               D remains Agent code

World
  PageAnalysis                       B existing stateless component
  BindingAnalysis                    B existing stateless evidence producer
  BindingReconciler                  A extract pure function
  StateBeliefReducer                 A extract pure function
  StateAnalysis                      DEFER future evidence source

Container                            D state owner/coordinator
  atomically applies immutable analysis/reducer output

Traversal                            D protocol microkernel
  TargetGrounder                     B extract after safety gate
  SemanticActionLowerer              B extract now
  ResultVerifier                     A keep protocol-local

Recovery                             D bounded mechanism coordinator

Environment
  IEnvironment                       C existing replaceable port
  device backends                    E external adapters
```

## 10. Purchase and rejection decisions

### Replaceable ports to purchase now

**NONE.**

- `IEnvironment` already exists.
- `IBindingAnalyzer` would freeze the current Reason-string binding contract and likely require an
  async contract for VLM implementations; defer until structured binding output is approved.
- `ITargetGrounder` is unnecessary because immutable evaluator delegates already express strategy;
  first fix the criterion+retry safety contract.
- `IIntentCompiler`, `ICapabilitySelector`, `IPageAnalysis`, `IStateAnalysis`, and `IResultVerifier`
  have no second executable implementation or strong external boundary today.

### Stateless components to extract now

1. `BindingReconciler` — evidence proposals to immutable bindings.
2. `StateBeliefReducer` — observation-local bindings to immutable state beliefs.
3. `SemanticActionLowerer` — authorized semantic action to `SemanticActionResult`.

`TargetGrounder` is approved only after its safety semantics are explicitly resolved.

### Responsibilities to keep inline

- Agent semantic decision ladder and lifecycle/terminal commits.
- capability selection and completion comparison while trivial.
- Agent recovery/open-world adjudication.
- Container atomic snapshot application and continuity mutation.
- Traversal dispatch/observe/verify/journal protocol and retry state.
- Recovery mechanism session.

### Interfaces rejected as premature

`IIntentCompiler`, `ICapabilitySelector`, `IActionAuthorizer`, `IPageAnalysis`, `IBindingAnalyzer`,
`IStateAnalysis`, `ITargetGrounder`, `IActionLowerer`, `IResultVerifier`, and Recovery policy/planner
interfaces.

## 11. Ordered extraction slices

1. **RC2-01 — Criterion Retry Safety Falsifier (tests only).** Prove whether retry with
   `TargetGroundingCriterion` can select/dispatch without renewed criterion and authorization evidence.
   If confirmed, stop at a Safety Semantic Gate; do not mix the repair with refactoring.
2. **RC2-02 — BindingReconciler.** Pure move, identical evidence parsing and immutable output; focused
   binding tests plus full regression.
3. **RC2-03 — StateBeliefReducer.** Pure function extraction; Container remains the only state applier;
   ambiguous/missing/stale toggle falsifiers required.
4. **RC2-04 — SemanticActionLowerer.** Move existing `Traversal.LowerAction` body intact; Traversal
   remains lowering/protocol authority and Agent still authorizes first.
5. **RC2-05 — TargetGrounder.** Only after RC2-01's safety boundary is resolved; preserve qualitative
   evidence and receipt enforcement on every observation/retry.
6. **RC2-06 — Replacement Port Re-evaluation.** Revisit an async binding-analysis port only after a
   structured binding proposal/evidence contract and a second production-shaped implementation exist.

Every extraction slice requires targeted tests, full regression, Architecture Guards, consistency,
OpenSpec strict validation, and a diff audit proving no authority/state migration.

## 12. Delta and gate

- **BehaviorDelta:** NONE
- **OwnershipDelta:** NONE
- **AuthorityDelta:** NONE
- **DependencyDelta:** NONE
- **MacroArchitecture:** UNCHANGED
- **HumanGateRequired:** YES — production extraction needs explicit authorization, and RC2-01 may expose
  a separate safety-semantic behavior purchase that this challenge does not authorize.
