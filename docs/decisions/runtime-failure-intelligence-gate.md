# Runtime Failure Intelligence Gate

> Date: 2026-08-12
> Role: Project Leader
> Lane: `SEMANTIC_DISCOVERY`
> Input baseline: `CORE_RUNTIME_SEMANTIC_SPINE_GRADUATED` + `RUNTIME_OBSERVABILITY_TRACE_GRADUATED`
> Decision: `ENTER_FAILURE_INTELLIGENCE_SEMANTIC_DISCOVERY`
> Implementation authority: **NOT GRANTED**

## 1. Gate result

```text
RUNTIME_FAILURE_INTELLIGENCE_GATE
  = ENTER_FAILURE_INTELLIGENCE_SEMANTIC_DISCOVERY

ARCHITECTURE_PRESSURE
  = NONE_CONFIRMED

SEMANTIC_CONTRACT
  = INSUFFICIENT_FOR_IMPLEMENTATION

RUNTIME_DELTA
  = NONE
```

The repository has enough evidence to enter bounded semantic discovery, but not enough semantics to purchase a production capability. `Failure intelligence` is currently an umbrella phrase rather than one stable capability contract.

## 2. Repository-backed pressure

The current system already records several truthful but deliberately separate surfaces:

| Evidence surface | Current meaning | Current owner |
|---|---|---|
| `SemanticRunResult` | Agent-adjudicated terminal or insufficient semantic outcome | Agent |
| `TraversalStepResult.Failed` | Local execution protocol could not advance | Traversal, escalated unchanged |
| `RecoveryResult.Failed` | Bounded recovery mechanism was not verified | Recovery evidence; Agent retains final authority |
| `Trap` / `TrapKind` | Structured evidence that trusted world belief is missing | Agent emission/interpretation boundary |
| Environment `ActionResult` | External dispatch report | Environment |
| `TraceSpan.Outcome` | Observed operation termination (`SUCCEEDED/FAILED/CANCELLED/UNKNOWN`) | Runtime facts, Harness lifecycle |
| `TraceCaptureBundle` | Capture/runtime/trace evidence and artifacts | Harness |

These values intentionally do not mean the same thing. A failed span is not automatically a failed action, a failed step is not automatically a root cause, an UNKNOWN observation is not failure, a safe refusal is not an execution fault, and a Runtime failure may coexist with a successful trace capture.

The graduated observability foundation now makes cross-layer evidence available. It does not define:

- what constitutes one failure episode;
- which facts are causal, propagated, terminal, or merely correlated;
- whether a diagnosis is supported, ambiguous, or unknown;
- how multiple candidate causes are represented;
- which outcomes are expected refusal versus defect/fault;
- who consumes a diagnostic result and for what authorized purpose.

That missing distinction is the real semantic pressure.

## 3. Required decomposition

Failure intelligence MUST NOT be purchased as one undifferentiated engine. Semantic discovery shall distinguish at least:

1. **Failure facts** — immutable directly recorded results, observations, spans, capture faults, and identifiers. Existing owners remain authoritative for these facts.
2. **Failure episode correlation** — a Harness-side grouping of directly related facts for one run/action/observation/recovery chain. Correlation is not causation.
3. **Failure classification** — a stable statement of the kind and boundary of failure, including `UNKNOWN/INSUFFICIENT` when evidence cannot support a classification.
4. **Diagnostic hypothesis** — an evidence-referenced, falsifiable inference about cause. A hypothesis is not world truth or Runtime authority.

The following is a separate future capability and is explicitly excluded:

5. **Response recommendation or adaptation** — retry, recovery, replanning, suppression, capability selection, or policy change. It requires its own Scenario and authority/safety gate.

## 4. Semantic questions requiring resolution

Semantic discovery must answer without assuming an implementation mechanism:

1. What observable boundary starts and ends one failure episode?
2. How are origin, propagation, handling, and terminal outcome represented without selecting a root cause by call order?
3. Which existing outcomes are failures, expected refusals, insufficient evidence, cancellation, uncertainty, or successful handling?
4. What minimum structured evidence supports a classification, and when must the result remain `UNKNOWN/INSUFFICIENT`?
5. How are multiple plausible causes preserved without numeric confidence or forced ranking?
6. How are Runtime failure, Environment failure, capture/listener failure, and Harness conformance failure kept separate?
7. Which identifiers provide stable correlation without parsing `Reason`, `Info`, exception text, or diagnostic strings?
8. What data is safe and necessary to persist, redact, replay, and compare deterministically?
9. Is the intended consumer a human/operator, regression triage, or another non-authoritative tool? Runtime adaptation is not an accepted consumer in this gate.

## 5. Minimum falsifying scenarios

Semantic discovery requires behavior/evidence contracts for at least these cases:

### FI-01 — Environment rejection propagation

```text
Environment ExecuteAsync = Rejected
-> TraversalStepResult.Failed
-> Agent terminal failure
```

The episode must preserve the Environment origin, Traversal propagation, and Agent terminal adjudication without reporting three independent root causes or parsing diagnostic text.

### FI-02 — Evidence insufficiency is not execution failure

```text
switch state = UNKNOWN
-> StateEvidenceRequired
-> zero dispatch
```

The system must not classify the safe refusal as failed action execution or Runtime defect.

### FI-03 — Dispatch timeout uncertainty

```text
ExecuteAsync = TimedOut
-> fresh Observation
-> world changed OR unchanged
```

Timeout alone must not become action success or definitive failure. Classification must preserve the post-dispatch evidence branch.

### FI-04 — Recovery failure with antecedent trap

```text
Trap emitted
-> bounded Recovery attempted
-> RecoveryResult.Failed
-> Agent adjudicates terminal result
```

The antecedent world-belief failure and the recovery verification failure must remain distinguishable; recovery failure must not erase or rewrite the original evidence.

### FI-05 — Harness failure is not Runtime failure

```text
Runtime succeeds or fails
+ listener / projection / persistence failure
```

The Harness fault must remain a separate episode/boundary and must never change the Runtime outcome or become its root cause without evidence.

### FI-06 — Cancellation boundary

Cancellation must remain distinct from semantic failure, expected refusal, and unknown outcome, including when child spans close before their parents.

### FI-07 — Multiple plausible causes

One replay must contain evidence compatible with more than one causal explanation. The result must preserve alternatives or `INSUFFICIENT`; it must not manufacture a single root cause.

### FI-08 — Deterministic replay and diagnostic-text drift

Equivalent structured evidence with different free-form messages and exact timings must produce the same accepted classification/correlation result.

## 6. Ownership and authority freeze

- Agent remains the sole semantic Run outcome and completion/failure authority.
- Container retains page-local mutable state ownership.
- Traversal retains local execution, dispatch, fresh observation, and verification authority.
- Environment retains external observation and dispatch reporting.
- Recovery retains its bounded mechanism; Agent retains recovery admission, interpretation, resume, and terminal authority.
- Runtime emits structured facts only and owns no failure-intelligence lifecycle.
- Harness may own immutable episode/correlation/diagnostic artifacts and mechanism-local analysis buffers if a future Scenario purchases them.
- A failure classification or diagnostic hypothesis cannot authorize an action, retry, recovery, replan, completion, or semantic result.
- Existing `SemanticRunResult`, `TraversalStepResult`, `RecoveryResult`, `Trap`, `GoalEvidence`, `TraceEvent`, and `TraceRun` semantics remain unchanged.

## 7. Architecture assessment

No architecture change is currently proven necessary. A bounded post-run or replay-time Harness capability can plausibly consume immutable existing evidence while preserving dependency direction and authority.

This is a hypothesis to falsify during semantic discovery, not implementation approval. Return `ARCHITECTURE_GATE_REQUIRED` if an adequate contract would require any of:

- Runtime depending on Harness analysis;
- analysis output changing Agent decisions or final RunState;
- a new mutable state owner in the Runtime spine;
- reverse dependency from Environment/Traversal/Container to diagnostic analysis;
- parsing diagnostic strings as a decision protocol;
- automatic recovery/retry/planning authority;
- a Provider framework, registry, Brain, Planner, Graph, or FSM.

## 8. Explicit non-actions

- No Runtime modification.
- No Harness implementation.
- No `FailureIntelligenceEngine`, `RootCauseAnalyzer`, Provider, registry, Brain, Planner, Graph, or FSM design.
- No new failure enum/model/API yet.
- No numeric confidence or threshold.
- No automatic retry, recovery, replanning, or policy mutation.
- No change to graduated Runtime Observability Trace invariants.
- No capability candidate generation before semantic evidence is extracted and independently validated.

## 9. Required next task

```text
RESEARCH_RUNTIME_FAILURE_EPISODE_EVIDENCE_AND_FALSIFYING_SCENARIOS
```

The research must inventory current executable failure assets, separate direct facts from inference, minimize FI-01 through FI-08, identify duplication with existing Trap/Recovery/Result semantics, and return evidence suitable for Reality Model extraction. It must not design implementation.

`ENTER_FAILURE_INTELLIGENCE_SEMANTIC_DISCOVERY`

STOP.
