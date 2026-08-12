# Runtime Failure Episode Evidence Research

> Date: 2026-08-12
> Role: Project Leader / Semantic Discovery
> Input gate: `docs/decisions/runtime-failure-intelligence-gate.md`
> Result: `RUNTIME_FAILURE_EPISODE_EVIDENCE_RESEARCH_RESULT`
> Decision: `FAILURE_EPISODE = HARNESS_ARTIFACT_ONLY`
> Production implementation authority: **NOT GRANTED**

## 1. Research result

```text
FAILURE_EPISODE
  = HARNESS_ARTIFACT_ONLY

RUNTIME_SEMANTIC_MODEL
  = NOT_REQUIRED

RUNTIME_DELTA
  = NONE

OWNERSHIP_DELTA
  = NONE

AUTHORITY_DELTA
  = NONE
```

A Failure Episode is a post-run or replay-time correlation artifact over already-authoritative facts. It is not a new Runtime outcome, current-world belief, Trap, Recovery result, GoalEvidence, or execution instruction.

The evidence supports option **A**. Option **B**, a Runtime semantic model, is rejected because:

1. Runtime owners already emit the authoritative facts needed for their scopes: `IntentCompilationResult`, `SemanticRunResult`, `TraversalStepResult`, `ActionResult`, `Trap`, `RecoveryResult`, `GoalEvidence`, and semantic `TraceEvent`.
2. `TraceRun`, capture lifecycle, persistence, Scenario assets, replay assets, and structural assertions are Harness-owned.
3. Correlation requires cross-stream historical context that no Runtime owner should acquire.
4. Making a Runtime FailureEpisode authoritative would duplicate Agent failure authority and invite diagnostic inference into retry, Recovery, or completion decisions.
5. No executable evidence requires Runtime to consume a correlated episode during execution.

## 2. Fact and inference boundary

Failure intelligence has four layers. Only the first two belong to the minimum Failure Episode artifact:

| Layer | Meaning | Normative boundary |
|---|---|---|
| Direct fact | An immutable recorded receipt from its existing owner | Never rewritten by Harness |
| Correlation | Immutable references grouping facts into one bounded episode | Correlation is not causation |
| Classification | A derived label supported by explicitly cited facts | May be supported, ambiguous, or insufficient |
| Diagnostic hypothesis | A falsifiable possible explanation | Never world truth or action authority |

Classification and hypotheses may be attached as separate immutable Harness assessments, but they must not be stored as if they were direct facts. A Failure Episode remains valid with zero assessments.

The names used in this research are semantic vocabulary for future specification work. They do not authorize a new enum, class, interface, classifier, or engine.

## 3. Correlation shape

The proposed chain:

```text
Scenario
-> TraceRun
-> Observation history
-> Action history
-> GoalEvidence
-> Outcome
```

is a useful correlation direction, but it MUST NOT be a mandatory linear schema. Real episodes legitimately omit links:

- Intent insufficiency occurs before a Runtime run and has no action, Observation, GoalEvidence, or TraceRun.
- Safe refusal and UNKNOWN evidence have observations but intentionally have no action.
- Action rejection may have no post-action Observation because no effect was accepted.
- A Harness persistence failure may occur after a complete Runtime outcome.
- A capture/listener failure can leave a partial TraceRun.
- A successful recovery can contain a failed antecedent operation without a failed final Runtime outcome.

Therefore a Failure Episode correlates a **partial evidence graph by stable references**. Missing evidence stays absent and must never be synthesized from diagnostic text or positional order.

## 4. Minimal immutable evidence set

The minimum evidence contract for one Harness-owned Failure Episode is:

### 4.1 Identity and provenance

- schema version;
- stable Failure Episode ID;
- provenance / evidence maturity;
- optional Scenario ID;
- optional Run ID, TraceRun ID, and CaptureSession ID exactly as observed.

Unknown correlation IDs remain absent. A filename, CLR type, message, timestamp, or array index must not become identity.

### 4.2 Bounded evidence window

- one required start evidence reference;
- one required end evidence reference, which may equal the start for an atomic episode;
- immutable typed evidence references within that window.

The window is defined by stable evidence identity and source order/sequence where available, not exact elapsed time. It may reference a caller-owned pre-run receipt or a Harness failure that occurred after the Runtime outcome.

### 4.3 Typed evidence references

The reference set may contain only existing authoritative receipts:

- intent input / `IntentCompilationResult` receipt;
- Scenario input/expected contract reference;
- TraceRun span or stable observability event reference;
- Observation sequence, Frame, or Observation artifact reference;
- action dispatch and `ActionResult` reference;
- `TraversalStepResult` / journal entry reference;
- Trap and Recovery result/event reference;
- GoalEvidence reference;
- Agent `SemanticRunResult` / final RunState reference;
- capture, listener, projection, or persistence result reference.

The episode stores references and minimal correlation metadata, not mutable copies of Runtime state, Observations, action history, or GoalEvidence.

### 4.4 Separate outcomes

An episode must preserve distinct optional terminal receipts:

- Runtime semantic outcome;
- external dispatch outcome;
- recovery verification outcome;
- Harness capture/projection/persistence outcome.

These cannot be collapsed into one boolean `Success` or `Failure`. A Runtime run may succeed while persistence fails; a timeout may be followed by verified world effect; a safe refusal may intentionally produce no dispatch.

### 4.5 Optional assessment envelope

A future assessment, if separately approved, requires:

- classification vocabulary version;
- classification boundary;
- supporting evidence references;
- contradictory or missing evidence references;
- stance: `SUPPORTED`, `AMBIGUOUS`, or `INSUFFICIENT`;
- zero or more falsifiable diagnostic hypotheses.

No numeric confidence, threshold, recommended action, or free-form-message parser is required.

## 5. Failure taxonomy falsification

The following research labels are deliberately about **boundaries**, not one universal severity ladder.

| Observed condition | Classification boundary | Direct evidence required | What it is not |
|---|---|---|---|
| Intent insufficiency | `NON_EXECUTABLE_INPUT / INSUFFICIENT` | caller Intent + `IntentCompilationResult.Insufficient` | Runtime failure, dispatch failure, Goal failure |
| Safe refusal | `AUTHORIZED_REFUSAL` | authorization evidence false/unknown + zero dispatch | failed attempt, target execution failure |
| UNKNOWN state evidence | `EVIDENCE_INSUFFICIENT` | fresh Observation with unknown state + `StateEvidenceRequired` + zero dispatch | OFF, action rejection, verification failure |
| Binding failure | `BINDING_UNRESOLVED` | binding evidence + `SemanticRunResult.BindingUnresolved` | perception certainty, action failure, root cause |
| State verification failure | `VERIFICATION_NOT_SATISFIED` | explicit expectation + fresh Observation + verification receipt | dispatch failure inferred from state alone |
| Action rejected | `DISPATCH_REJECTED` | exact action + Environment `ActionResult.Rejected` | world unchanged proof, semantic root cause |
| Action timeout | `DISPATCH_UNCERTAIN` | exact action + `TimedOut` + required fresh post-dispatch evidence when available | action success or definitive action failure |
| World unchanged | `OBSERVED_NO_EFFECT` | pre/post Observation references and an explicit comparison scope | proof that dispatch did not occur, global exhaustion, root cause |
| Recovery failure | `RECOVERY_VERIFICATION_FAILED` | antecedent Trap + recovery action/result + fresh Observation + failed verification | erasure of original Trap, automatic unrecoverability |
| Harness/storage failure | `HARNESS_CAPTURE_FAILED` or `HARNESS_PERSISTENCE_FAILED` | Harness result/diagnostic + independently preserved Runtime outcome | Runtime failure or causal explanation of it |

### 5.1 Taxonomy findings

- **Intent insufficiency is not a failure episode in the Runtime.** It may be a caller/Harness episode so tooling can explain why no run exists.
- **Safe refusal is successful enforcement of an authorization boundary.** The surrounding user task may remain incomplete, but no action failure occurred.
- **UNKNOWN is an evidence state.** It must not be normalized into OFF, failed verification, or binding failure.
- **Binding failure is a semantic precondition boundary.** It says interaction grounding is unsupported; it does not identify why perception/binding evidence was missing.
- **State verification failure requires a declared expectation.** `World unchanged` alone is merely a relation between observations.
- **Rejected and TimedOut are different.** Rejected is an external dispatch outcome; TimedOut is dispatch uncertainty that requires world evidence.
- **Recovery failure is normally a secondary episode or sub-boundary.** It must retain its antecedent Trap/failure evidence.
- **Harness failure is operational tooling failure.** It belongs outside Runtime semantic authority even when it prevents later diagnosis.

## 6. Evidence inventory and maturity

| Area | Current executable evidence | Research status |
|---|---|---|
| Intent insufficiency | `IntentCompilationModuleTests.P5/P13/P14/P15`; CP-14 insufficient envelope tests | EXECUTABLE |
| Safe refusal | `BoundedCandidateSafetyScenarioTests`; `BoundedCandidateSafetyBehaviorTests` | EXECUTABLE |
| UNKNOWN evidence | `AgentSemanticClosedLoopTests.P4`; `SimulationConformanceTests.H3`; golden/reality replay UNKNOWN cases | EXECUTABLE, including reality-seeded/recorded paths |
| Binding failure | `AgentSemanticClosedLoopTests.P7/P8/P9`; intent module G10 | EXECUTABLE |
| State verification / world unchanged | closed-loop budget tests; Simulation conformance H4/H5; Traversal and ScriptedEnvironment stuck-world fixtures | EXECUTABLE |
| Action rejected | Observation Replay rejection; Traversal/physical environment rejection tests | EXECUTABLE |
| Action timeout | SC-P3-001 formal tests and Simulation conformance H5/H7 | EXECUTABLE |
| Recovery failure | SC-P2-003 `RecoveryVerificationFailureTests`; `AgentRecoveryTests` | EXECUTABLE |
| Harness/storage failure | success persistence path exists; no dedicated injected storage/listener failure Scenario | PARTIAL / ASSET GAP |
| Cancellation | outcome vocabulary exists; no end-to-end cancellation evidence asset | EVIDENCE GAP |
| Multiple plausible causes | timeout + unchanged provides pressure, but no formal alternative-hypothesis oracle | EVIDENCE GAP |
| Diagnostic-text drift | several deterministic replay tests exist; no paired structured-evidence/scrambled-message acceptance proof | PARTIAL / ASSET GAP |

Passing existing tests does not promote partial/gap rows. FI-05 through FI-08 need the bounded fixtures described below before any classifier capability can be considered.

## 7. FI-01 through FI-08 falsifier execution design

### FI-01 — Environment rejection propagation

**Input evidence**

- Scenario and run correlation;
- exact dispatched action/action ID;
- Environment `ActionResult.Rejected`;
- Traversal failed result/journal entry;
- Agent terminal `ExecutionFailed` or RunState failure receipt;
- relevant Environment/Traversal/Agent spans if captured.

**Expected classification boundary**

- origin fact: `DISPATCH_REJECTED` at Environment;
- propagation: Traversal could not advance;
- terminal authority: Agent's existing outcome;
- one correlated episode with three boundary roles, not three root causes.

**Forbidden inference**

- rejection proves unsafe action;
- rejection proves world unchanged;
- the first/lowest failed span is automatically root cause;
- parse `Info` or `Reason` to choose classification.

**Execution asset**

- Start from the existing Observation Replay rejected-dispatch Scenario and add Harness correlation only in a future authorized test slice.

### FI-02 — Insufficiency and refusal are not execution failure

**Input evidence**

- Variant A: `IntentCompilationResult.Insufficient`, no Run ID, no dispatch;
- Variant B: authorization denial/unknown with fresh candidate evidence and zero dispatch;
- Variant C: fresh UNKNOWN switch state, `StateEvidenceRequired`, zero dispatch.

**Expected classification boundary**

- A = `NON_EXECUTABLE_INPUT / INSUFFICIENT`;
- B = `AUTHORIZED_REFUSAL`;
- C = `EVIDENCE_INSUFFICIENT`;
- all three remain distinct and contain no attempted action.

**Forbidden inference**

- classify any variant as action/recovery failure;
- invent a desired state or authority;
- treat zero dispatch as proof that Environment failed;
- collapse denied and unknown authorization.

**Execution asset**

- Compose existing Intent compilation, bounded candidate safety, and UNKNOWN semantic-loop fixtures without changing Runtime.

### FI-03 — Dispatch timeout uncertainty

**Input evidence**

- exact action and `ActionResult.TimedOut`;
- fresh post-action Observation;
- pre/post world evidence;
- Traversal journal and Agent GoalEvidence/outcome;
- dispatch count.

**Expected classification boundary**

- before fresh evidence: `DISPATCH_UNCERTAIN` only;
- world changed branch: expected effect may be verified from Observation, while timeout remains the dispatch report;
- world unchanged branch: `OBSERVED_NO_EFFECT` plus unresolved cause; no fabricated success.

**Forbidden inference**

- timeout equals success;
- timeout equals definitive failure;
- world unchanged proves action was never dispatched;
- blind redispatch or Goal completion from timeout.

**Execution asset**

- Reuse SC-P3-001 positive/negative deterministic fixtures and Simulation conformance H5/H7.

### FI-04 — Recovery verification failure

**Input evidence**

- antecedent Trap and source evidence;
- Recovery ID, dispatched recovery action, fresh Observation;
- `RecoveryResult.Failed` / verification event;
- Agent terminal outcome and no-resume evidence.

**Expected classification boundary**

- original failure/Trap remains the antecedent episode boundary;
- recovery verification failure is a linked secondary boundary;
- Agent remains terminal authority.

**Forbidden inference**

- recovery action dispatch equals recovery success;
- recovery failure erases or replaces the Trap;
- one failed recovery proves globally unrecoverable;
- Harness recommends another recovery.

**Execution asset**

- Reuse SC-P2-003 `RecoveryVerificationFailureTests`, which already proves ordering, no resume, exact action count, and deterministic replay.

### FI-05 — Harness or storage failure isolation

**Input evidence**

- Variant A: successful Runtime result and valid GoalEvidence plus injected store failure;
- Variant B: failed Runtime result plus independent listener/projection/store failure;
- capture/trace persistence result and any retained partial diagnostics.

**Expected classification boundary**

- Harness failure is a separate operational episode linked by Run/Capture IDs;
- Runtime outcome remains independently preserved;
- missing trace evidence is reported as missing, not reconstructed.

**Forbidden inference**

- persistence failure changes Runtime success/failure;
- listener failure caused the Runtime result without direct evidence;
- retry or redispatch to obtain a cleaner trace;
- incomplete trace is silently treated as complete.

**Execution asset**

- Add a future test-only failing `ITraceCaptureStore`/listener fixture. Current repository proves successful persistence only, so FI-05 is not yet executable end-to-end.

### FI-06 — Cancellation boundary

**Input evidence**

- caller cancellation receipt/token state;
- active span boundary and `CANCELLED` outcome;
- action/observation history up to cancellation;
- Agent outcome only if Agent independently emits one.

**Expected classification boundary**

- cancellation remains a control/lifecycle boundary distinct from rejection, timeout, evidence insufficiency, and semantic failure;
- completed facts before cancellation remain preserved.

**Forbidden inference**

- cancellation is `ExecutionFailed` by default;
- absence of post-cancellation evidence means world unchanged;
- cancelled child forces a fabricated parent semantic failure;
- resume/retry policy inferred from the episode.

**Execution asset**

- A future deterministic cancellation fixture is required. Current tests pass `CancellationToken.None`; no accepted end-to-end oracle exists.

### FI-07 — Multiple plausible causes

**Input evidence**

- one `TimedOut` dispatch;
- fresh world-unchanged Observation;
- no direct transport acknowledgment beyond timeout;
- no additional evidence distinguishing “not delivered” from “delivered but ineffective.”

**Expected classification boundary**

- fact classifications: `DISPATCH_UNCERTAIN` + `OBSERVED_NO_EFFECT`;
- diagnostic assessment: at least two plausible hypotheses or `INSUFFICIENT`;
- no forced ranking or numeric confidence.

**Forbidden inference**

- select transport loss, wrong target, rejected effect, or stale observation as the single root cause;
- use span order or message wording to break the tie;
- infer recovery/retry recommendation.

**Execution asset**

- Minimize the SC-P3-001 negative fixture and assert alternative preservation. This oracle does not yet exist.

### FI-08 — Deterministic structural replay

**Input evidence**

- two episodes with equivalent stable IDs/types/outcomes/observation sequences/action results;
- different exact timings, generated span IDs, and free-form `Reason`/`Info`/diagnostic wording.

**Expected classification boundary**

- equivalent correlation and classification results;
- direct diagnostic strings remain retained only as diagnostic payload if policy permits, never classification input.

**Forbidden inference**

- exact duration equality;
- diagnostic string equality;
- CLR/private method order;
- generated ID equality as behavior identity.

**Execution asset**

- Existing deterministic SC-P3-001 and SC-P2-003 replays are inputs; a future Harness-only perturbation fixture must vary diagnostic text/timing while keeping structured evidence stable.

## 8. Authority proof

Harness may:

- collect immutable public/capture evidence;
- correlate evidence by stable IDs and explicit sequence/relation fields;
- summarize direct facts without redefining them;
- classify episode boundaries when evidence supports the classification;
- preserve multiple diagnostic hypotheses or return `AMBIGUOUS/INSUFFICIENT`;
- persist and compare structural artifacts under existing Harness ownership.

Harness may not:

- dispatch or suppress an action;
- retry a Traversal step;
- start, repeat, or select Recovery;
- mutate Observation, Container belief, Traversal journal, Runtime Trace, GoalEvidence, or RunState;
- reinterpret an observability span outcome as semantic success/failure;
- alter Agent decisions or final result;
- choose a Planner, route, capability, target, or authorization;
- feed a classification/hypothesis back into Runtime without a separate semantic, architecture, and safety gate.

This is compatible with the frozen architecture because analysis remains outside:

```text
Agent -> Container -> Traversal -> Environment
```

The analysis direction is one-way and post-fact:

```text
Runtime facts + Scenario/Capture/Replay evidence
  -> Harness Failure Episode correlation
  -> optional non-authoritative assessment
```

No arrow returns to Runtime.

## 9. Deduplication boundary

A Failure Episode does not replace or duplicate:

- `IntentCompilationResult` — owns resolved/insufficient Intent projection;
- `SemanticRunResult` — Agent-owned semantic run outcome;
- `TraversalStepResult` — local execution protocol result;
- `ActionResult` — Environment dispatch report;
- `Trap` — structured missing-trusted-world-belief evidence;
- `RecoveryResult` — bounded recovery verification result;
- `GoalEvidence` — completion evidence;
- semantic `TraceEvent` — Agent semantic causal history;
- `TraceRun` — hierarchical operational observability;
- `TraceCaptureBundle` — capture lifecycle and persisted evidence.

It references these facts. It must not add fields to them merely to make Harness analysis convenient.

## 10. Evidence gaps and next gate

The semantic boundary is sufficiently researched to freeze `FailureEpisode = HARNESS_ARTIFACT_ONLY`. It is not sufficient to implement a classifier.

Required evidence before capability candidate generation:

1. FI-05 test-only Harness/storage failure isolation fixture;
2. FI-06 deterministic cancellation fixture;
3. FI-07 alternative-hypothesis/INSUFFICIENT oracle;
4. FI-08 structured-equivalence fixture with timing/message perturbation;
5. independent validation that the taxonomy does not collapse safe refusal, UNKNOWN, timeout, or world unchanged into failure/root cause.

Recommended next task:

```text
PROJECT_LEADER_FAILURE_EPISODE_REALITY_MODEL_EXTRACTION
```

That task should extract the Reality Model from the executable inventory and explicitly retain FI-05 through FI-08 as evidence gaps/falsifiers. It must not generate implementation candidates or add Runtime models.

## 11. Explicit non-actions

- No production code changes.
- No Runtime model, component, enum, field, interface, or mutable state.
- No Harness classifier implementation.
- No ownership or authority change.
- No numeric confidence or threshold.
- No diagnostic-string decision protocol.
- No automatic retry, Recovery, dispatch, replanning, or completion.
- No Provider framework, registry, Brain, Planner, Graph, or FSM.

`FAILURE_EPISODE = HARNESS_ARTIFACT_ONLY`

STOP.
