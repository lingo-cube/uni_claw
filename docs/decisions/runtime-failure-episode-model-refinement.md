# Runtime Failure Episode Model Refinement

> Date: 2026-08-12
> Role: Project Leader / Model Refinement
> Lane: `SEMANTIC_DISCOVERY`
> Input: `docs/decisions/runtime-failure-episode-independent-validation.md` (findings I1–I7)
> Result: `RUNTIME_FAILURE_EPISODE_MODEL_REFINEMENT_RESULT`
> FailureEpisode ownership: `HARNESS_ARTIFACT_ONLY`
> Implementation authority: **NOT GRANTED**

## 0. Refinement mandate

Resolve the seven non-blocking findings from independent validation. Five are
substantive model refinements; two (I5, I6) are deferred or absorbed into the
refined model structure.

No implementation. No FailureEpisode code. No Runtime changes.

---

## 1. Structural receipt ordering

> Resolves: **I1** — simultaneous receipt tiebreaker unspecified.

### 1.1 Problem

When two typed receipts share the same Frame or execution step and both qualify
as episode-start candidates, "earliest directly recorded receipt" is
underspecified. The model needs a deterministic ordering that does not depend on
timestamps, filenames, or diagnostic text.

### 1.2 Refinement: receipt precedence order

Define a total order over receipt kinds based on semantic dependency, not
temporal arrival:

```text
Receipt precedence (earliest first, within same Frame/step):

  IntentCompilationResult
      Intent projection exists before any run; pre-run boundary.

  < Frame
      Container-level state snapshot containing Observations.

  < Observation
      World state is prerequisite for any action decision.

  < AuthorizationReceipt
      Authorization check is a precondition for dispatch.

  < BindingReceipt
      Interaction grounding is a precondition for dispatch.

  < ActionResult
      The dispatch itself — Rejected, TimedOut, or Dispatched.

  < PostDispatchObservation
      Fresh observation explicitly scoped as post-dispatch evidence.

  < VerificationReceipt
      Explicit criterion evaluated against fresh evidence.

  < Trap
      Structured missing-trusted-world-belief evidence.

  < RecoveryResult
      Bounded recovery verification result.

  < SemanticRunResult
      Agent-adjudicated terminal semantic outcome.

  < HarnessCaptureResult
      Capture lifecycle completion/failure.

  < HarnessPersistenceResult
      Append-only store completion/failure.
```

This is a fixed total order. It is structural — every receipt kind has a
determinate position. It requires no timestamp comparison.

### 1.3 Start boundary rule (refined)

For a given episode type, the start boundary is the earliest receipt (lowest in
the precedence order) that satisfies the start condition for that episode type
and falls within the bounded run/capture scope.

| Episode type | Start condition | Start receipt (first in precedence order that matches) |
|---|---|---|
| Pre-run insufficiency | Intent cannot produce executable Goal | `IntentCompilationResult.Insufficient` |
| Pre-dispatch refusal | Authorization denial OR binding unresolved OR insufficient evidence, zero dispatch | `AuthorizationReceipt`, `BindingReceipt`, or Observation with UNKNOWN state — whichever is earliest in precedence order |
| Dispatch rejection | Dispatch returned Rejected | `ActionResult.Rejected` |
| Dispatch uncertainty | Dispatch returned TimedOut | `ActionResult.TimedOut` |
| World no-effect | Pre/post observations show no expected change | `ActionResult` (the action that produced no effect), followed by `PostDispatchObservation` as evidence |
| Recovery failure | Trap emitted, Recovery attempted, verification failed | `Trap` (the antecedent, not the Recovery attempt) |
| Harness failure | Capture/listener/projection/store fault | Earliest Harness receipt in precedence order that records a fault |

When two receipts of different kinds share the same Frame, the precedence order
resolves the tie deterministically. When two receipts of the *same* kind share
the same Frame (e.g., two Observations in one Frame), the Frame's internal
sequence order (as recorded by the Capture/Replay asset) resolves the tie. This
sequence order is a structural field, not a timestamp.

### 1.4 Terminal boundary rule (refined)

The terminal boundary is the latest receipt (highest in the precedence order)
that satisfies the terminal condition for the episode type:

| Episode type | Terminal condition | Terminal receipt |
|---|---|---|
| Pre-run insufficiency | Confirmed zero dispatch, no Run created | `IntentCompilationResult.Insufficient` (start = terminal; atomic episode) |
| Pre-dispatch refusal | Confirmed zero dispatch, Agent terminal receipt | `SemanticRunResult` with zero-dispatch disposition |
| Dispatch rejection | Propagation through Traversal, Agent terminal | `SemanticRunResult` (terminal authority) |
| Dispatch uncertainty | Fresh observation + Agent adjudication | `SemanticRunResult` (terminal authority) or `PostDispatchObservation` + `VerificationReceipt` if verification was performed |
| World no-effect | Verification or Agent adjudication of the no-effect finding | `VerificationReceipt` or `SemanticRunResult` |
| Recovery failure | Recovery verification failed, Agent terminal, no resume | `SemanticRunResult` (terminal authority, no-resume) |
| Harness failure | Harness fault recorded, independent Runtime outcome preserved | `HarnessPersistenceResult` or `HarnessCaptureResult` |

When required terminal evidence never arrives, the last available receipt in
precedence order within the bounded scope ends the window. The assessment
stance is `INSUFFICIENT`. Absence is not replaced by fabrication.

### 1.5 Within-window receipt ordering

Receipts within an evidence window are ordered by:
1. Frame/step sequence position (structural, from Capture/Replay asset).
2. Within the same Frame: precedence order as defined in §1.2.

This is deterministic. Two episodes constructed from the same receipts produce
the same internal order every time.

---

## 2. Evidence inclusion rule

> Resolves: **I2** — no exclusion rule for temporally-in-window but
> semantically-unrelated receipts.

### 2.1 Problem

The extraction defines what the evidence window *contains* (five inclusion rules)
but not what it *excludes*. A receipt that is temporally within the window
boundaries but semantically unrelated could be misread as evidence.

### 2.2 Refinement: explicit exclusion principle

```text
A receipt enters the evidence window IF AND ONLY IF it satisfies at least one
inclusion rule. Temporal position within the window's Frame/step boundaries is
neither necessary nor sufficient for inclusion.
```

### 2.3 Inclusion rules (carried forward, now exclusive)

A receipt is included in the evidence window only if it satisfies one of:

| Rule | Condition | Example |
|---|---|---|
| **R1 — Boundary** | The receipt IS the start or terminal boundary receipt. | `ActionResult.TimedOut` as start; `SemanticRunResult` as terminal. |
| **R2 — Structural dependency** | The receipt is a stable typed reference necessary to establish the selected boundary. | The `Trap` referenced by a Recovery episode; the `ActionResult` referenced by a dispatch episode. |
| **R3 — Explicit relation** | The receipt carries an explicit predecessor/next, action/observation, Trap/Recovery, or propagation relation to a receipt already in the window. | A `TraversalStepResult.Failed` that references the `ActionResult.Rejected` already in the window via its `ActionResultId`. |
| **R4 — Separate outcome** | The receipt is a Runtime, dispatch, Recovery, or Harness outcome that is relevant to the episode condition and must be preserved separately. | `SemanticRunResult` when the episode involves a dispatch failure — the Agent outcome is preserved independent of the dispatch outcome. |
| **R5 — Declared gap** | The absence of a receipt is explicitly declared as contradictory or missing evidence. | "Post-dispatch Observation was expected but not produced" — the gap is recorded; no receipt is fabricated. |

### 2.4 Explicit exclusion

A receipt that satisfies none of R1–R5 is excluded, even if it occurs within the
Frame/step sequence between the start and terminal boundaries.

Examples of correctly excluded receipts:

| Receipt | Why excluded |
|---|---|
| A successful Traversal step 2 when step 4 fails with no relation to step 4 | Satisfies no rule — no structural dependency, no explicit relation to step 4, no separate outcome relevant to the step-4 episode |
| An Observation of an unrelated UI element in the same Frame as the failed dispatch | Satisfies no rule — not the boundary, not a dependency, no relation to the dispatch |
| A span from a different component with no parent/child relation to the failed span | Satisfies no rule — no propagation relation, no shared outcome |

### 2.5 Cross-boundary inclusion

A receipt that occurs *outside* the Frame/step sequence boundaries (e.g., a
Harness persistence failure that occurs after the Runtime run completes) may be
included by R2 (structural dependency) or R4 (separate outcome). The window is
defined by typed references, not by temporal containment.

### 2.6 Inclusion determinism

For any given set of receipts and a selected episode type, the evidence window
is uniquely determined by applying R1–R5. Two independent constructions from the
same receipts and episode type produce identical window membership. This is
testable and falsifiable.

---

## 3. Correlation model

> Resolves: **I3** — optional `RunId` on `TraceRun` creates correlation gap.

### 3.1 Problem

`TraceRun` carries `TraceRunId`, optional `TraceId`, and optional `RunId`. When
`RunId` is absent, there is no documented path to correlate `TraceRun` to the
correct Run or CaptureSession. The model must acknowledge this honestly without
fabricating edges or changing Runtime ownership.

### 3.2 Refinement: three correlation states

Every `TraceRun` in an episode context is in exactly one of three correlation
states:

```text
CORRELATION_STATE
  ∈ { RUN_CORRELATED, SESSION_CORRELATED, UNCORRELATED }
```

#### RUN_CORRELATED

**Condition:** `TraceRun.RunId` is present and references a valid `Run.Id`.

**Correlation path:**
```text
TraceRun.RunId → Run.Id → Run
                         → CaptureSession (if Run was captured)
```

**Episode construction:** The episode carries both `RunId` and `TraceRunId`.
All Run-scoped receipts (Observations, Actions, GoalEvidence) are reachable
through `Run.Id`.

#### SESSION_CORRELATED

**Condition:** `TraceRun.RunId` is absent, BUT the `TraceRun` was produced within
a `CaptureSession` whose lifecycle owns the trace recording.

**Correlation path:**
```text
CaptureSession owns TraceRun recording lifecycle
  → CaptureSession.Id
  → CaptureSession.Frames / Observations / Actions
  → TraceRun spans/events (co-located in same CaptureSession)
```

**Episode construction:** The episode carries `CaptureSessionId` and
`TraceRunId`. Correlation is through the session container, not through `RunId`.
This is a weaker correlation — the `CaptureSession` may contain multiple runs,
and without `RunId`, the episode cannot distinguish which run produced which
spans without additional structural evidence (e.g., span timing relative to
Frame sequence).

**Known limitation:** If a `CaptureSession` contains multiple runs and
`TraceRun.RunId` is absent, the correlation between a specific span and a
specific run is unavailable. The episode records this as a correlation gap, not
an error.

#### UNCORRELATED

**Condition:** `TraceRun.RunId` is absent AND no `CaptureSession` owns the
`TraceRun` lifecycle.

**Correlation path:** None available in the current production model.

**Episode construction:** The episode carries `TraceRunId` only. The
`TraceRun` facts (spans, events, outcomes) are available as operational evidence
but cannot be definitively correlated to a specific Run or CaptureSession.

**This is honest, not an error.** It means the trace system recorded operational
facts without run-scoped or session-scoped identity. The episode must not
fabricate a correlation edge.

### 3.3 Correlation in episode construction

When constructing an episode:

1. If `RunId` is present → `RUN_CORRELATED`. All Run-scoped receipts are
   reachable.
2. If `RunId` is absent but a `CaptureSession` owns the trace lifecycle →
   `SESSION_CORRELATED`. Session-scoped receipts are reachable; run-scoped
   correlation is unavailable.
3. If neither → `UNCORRELATED`. `TraceRun` facts stand alone.

The correlation state is recorded in the episode's provenance metadata. A
downstream consumer can decide whether `SESSION_CORRELATED` or `UNCORRELATED`
episodes meet their evidence threshold.

### 3.4 What this does NOT do

- Does not add a `TraceRun → Run` edge where none exists.
- Does not require Runtime to emit `RunId` on `TraceRun`.
- Does not fabricate correlation from timestamps or diagnostic text.
- Does not change `TraceRun` schema, ownership, or authority.
- Does not close the `UNCORRELATED` gap — it names it.

### 3.5 Future correlation edge (not authorized)

A future explicit correlation edge (e.g., `TraceRun.RunId` becoming non-optional,
or a `CaptureSession.TraceRunIds` collection) would close the
`SESSION_CORRELATED` → `RUN_CORRELATED` gap without changing ownership. This
refinement documents the gap; it does not authorize closing it.

---

## 4. Fact vs assessment separation

> Resolves: **I4** — `OBSERVED_NO_EFFECT` / `VERIFICATION_NOT_SATISFIED`
> subsumption direction underspecified.

### 4.1 Problem

The extraction treats all taxonomy entries as classification boundaries without
distinguishing which are direct structural receipts (evidence facts) and which
are derived classifications (assessments). This creates ambiguity about whether
`OBSERVED_NO_EFFECT` and `VERIFICATION_NOT_SATISFIED` are alternatives or can
co-exist.

### 4.2 Refinement: two-layer taxonomy

The taxonomy splits into two layers. Every assessment MUST cite at least one
evidence fact. An assessment without a cited evidence fact is unsupported and
its stance is `INSUFFICIENT`.

#### Layer 1: Evidence facts

Evidence facts are direct structural receipts from their existing authoritative
owners. They are immutable and are never rewritten by Harness.

| Evidence fact | Owner | Meaning |
|---|---|---|
| `ActionResult.Rejected` | Environment | External dispatch was explicitly rejected. |
| `ActionResult.TimedOut` | Environment | External dispatch timed out; delivery/effect unknown. |
| `ActionResult.Dispatched` | Environment | External dispatch was accepted. |
| Observation (state = S) | Environment / Capture | World was observed in state S at this Frame. |
| Pre/post observation pair (Δ = none) | Environment / Capture | Two observations of the same target show no state change. |
| Pre/post observation pair (Δ = expected) | Environment / Capture | Two observations show the expected state change. |
| Pre/post observation pair (Δ = unexpected) | Environment / Capture | Two observations show an unexpected state change. |
| `VerificationReceipt.NotSatisfied` | Traversal | An explicit criterion was evaluated against fresh evidence and was not satisfied. |
| `VerificationReceipt.Satisfied` | Traversal | An explicit criterion was evaluated and satisfied. |
| `SemanticRunResult.BindingUnresolved` | Agent | Required interaction grounding could not be resolved. |
| `SemanticRunResult.StateEvidenceRequired` | Agent | Required state evidence is UNKNOWN; cannot proceed. |
| `SemanticRunResult.ExecutionFailed` | Agent | Execution could not complete. |
| `SemanticRunResult.Satisfied` | Agent | Goal evidence is satisfied. |
| `SemanticRunResult.Contradiction` | Agent | Evidence supports incompatible conclusions. |
| `Trap` | Agent | Structured evidence that trusted world belief is missing. |
| `RecoveryResult.Failed` | Recovery | Bounded recovery verification failed. |
| `RecoveryResult.Succeeded` | Recovery | Bounded recovery verification succeeded. |
| `IntentCompilationResult.Insufficient` | Intent compiler | Caller intent cannot produce an executable Goal. |
| Authorization receipt (denied) | Traversal / Agent | Authorization check returned denial. |
| Authorization receipt (unknown) | Traversal / Agent | Authorization check could not determine status. |
| `TraceSpan.Outcome` | Runtime (Harness-recorded) | Observed operation termination. |
| `HarnessCaptureResult.Failed` | Harness | Capture lifecycle fault. |
| `HarnessPersistenceResult.Failed` | Harness | Append-only store fault. |

#### Layer 2: Assessments

Assessments are classifications that cite evidence facts. They are Harness-owned
derived labels, not authoritative Runtime outcomes.

| Assessment | Cites (minimum) | Meaning |
|---|---|---|
| `DISPATCH_REJECTED` | `ActionResult.Rejected` | The dispatch was rejected by the external Environment. Does not prove world relation or root cause. |
| `DISPATCH_UNCERTAIN` | `ActionResult.TimedOut` | The dispatch outcome is uncertain. Fresh evidence required to narrow world-effect question. |
| `OBSERVED_NO_EFFECT` | Pre/post observation pair (Δ = none) AND explicit comparison scope | The world did not change between two observations of the same target within a declared scope. Does not prove non-delivery or dispatch failure. |
| `VERIFICATION_NOT_SATISFIED` | `VerificationReceipt.NotSatisfied` AND explicit expected-effect criterion | An expected-effect criterion was evaluated against fresh evidence and was not satisfied. |
| `BINDING_UNRESOLVED` | `SemanticRunResult.BindingUnresolved` | Required interaction grounding could not be resolved. Does not identify perception/root cause. |
| `EVIDENCE_INSUFFICIENT` | Observation (state = UNKNOWN) OR missing required evidence fact | Available evidence cannot support the required state or decision. Not OFF, not rejection, not transport failure. |
| `AUTHORIZED_REFUSAL` | Authorization receipt (denied or unknown) AND zero dispatch | A safety/authority boundary intentionally prevented dispatch. Not an execution fault. |
| `RECOVERY_VERIFICATION_FAILED` | `Trap` + `RecoveryResult.Failed` + Agent terminal (no resume) | Bounded recovery failed verification. Antecedent Trap remains linked. |
| `NON_EXECUTABLE_INPUT` | `IntentCompilationResult.Insufficient` | Caller intent cannot produce an executable Goal. Pre-run; no Run exists. |

### 4.3 The `OBSERVED_NO_EFFECT` / `VERIFICATION_NOT_SATISFIED` relationship (resolved)

These are assessments at different semantic layers. They are not alternatives.

**`OBSERVED_NO_EFFECT`** is a world-relation assessment. It cites:
- A pre-observation (evidence fact: Observation at time T₁).
- A post-observation (evidence fact: Observation at time T₂).
- An explicit comparison scope (what was expected to change, over what target).
- The finding: Δ = none.

It answers: "Did the world change in the expected way?"

**`VERIFICATION_NOT_SATISFIED`** is a criterion-adjudication assessment. It cites:
- An explicit expected-effect criterion (declared before or at dispatch).
- A `VerificationReceipt.NotSatisfied` (evidence fact from Traversal).
- The finding: the criterion was not met.

It answers: "Did the explicit verification criterion pass?"

**Co-existence rule:**

| Scenario | `OBSERVED_NO_EFFECT` | `VERIFICATION_NOT_SATISFIED` |
|---|---|---|
| No verification criterion declared; pre/post show no change | **Present** (world-relation assessment citing observation pair) | **Absent** (no criterion exists to evaluate) |
| Verification criterion declared; pre/post show no change; verification returns NotSatisfied | **May be present** (supporting world-relation assessment, citing the same observation pair) | **Present** (primary assessment citing criterion + `VerificationReceipt.NotSatisfied`) |
| Verification criterion declared; verification returns NotSatisfied; world DID change but not to the expected state | **Absent** (Δ ≠ none) | **Present** (criterion not satisfied; world change was wrong direction/magnitude) |
| Verification criterion declared; verification returns Satisfied | **Absent** | **Absent** (verification passed; no failure assessment) |

An episode may carry both assessments. They describe different semantic layers.
When both are present, `VERIFICATION_NOT_SATISFIED` is the primary
classification; `OBSERVED_NO_EFFECT` is supporting world-relation evidence.

### 4.4 Assessment stance

Every assessment carries one of three stances:

| Stance | Condition |
|---|---|
| `SUPPORTED` | All cited evidence facts are present in the episode and consistent with the assessment. |
| `AMBIGUOUS` | Cited evidence facts are present but support multiple incompatible assessments with no distinguishing evidence. |
| `INSUFFICIENT` | One or more cited evidence facts are missing from the episode, OR the available evidence facts contradict the assessment. |

No numeric confidence. No ranking. No recommended action.

---

## 5. Hypothesis falsifiability

> Resolves: **I7** — hypothesis falsifiability asserted but mechanism unspecified.

### 5.1 Problem

The extraction states diagnostic hypotheses are "falsifiable" but provides no
structure for falsification. Without a falsification condition, a hypothesis is
indistinguishable from an assertion.

### 5.2 Refinement: hypothesis structure

Every diagnostic hypothesis MUST carry five fields:

```text
Hypothesis
  = HypothesisStatement
  + SupportingEvidence[]
  + ContradictingEvidence[]
  + MissingEvidence[]
  + FalsificationCondition (at least 1)
```

#### HypothesisStatement

A single declarative sentence stating what caused the episode. Must be specific
enough that counter-evidence would disprove it. Must reference at least one
evidence fact in the episode.

**Valid:** "The dispatch timed out because the target element was not in the
expected location at the time of dispatch, and the Environment's retry policy
exhausted without acknowledgment."

**Invalid:** "Something went wrong with the dispatch." (too vague to falsify)

**Invalid:** "The system failed." (no reference to episode evidence)

#### SupportingEvidence[]

Explicit references to evidence facts within the episode that are consistent
with the hypothesis. Each reference must cite a specific evidence fact by its
stable identifier.

```text
SupportingEvidence:
  - ActionResult.TimedOut (id: action-42) — dispatch was not acknowledged
  - Pre-dispatch Observation (id: obs-17) — target element present at T₁
  - Post-dispatch Observation (id: obs-18) — target element absent at T₂
```

#### ContradictingEvidence[]

Explicit references to evidence facts within the episode that are inconsistent
with the hypothesis. If none exist, this field is explicitly `[none identified]`,
not omitted.

```text
ContradictingEvidence:
  - [none identified]
```

Honesty constraint: a non-empty `ContradictingEvidence` does not automatically
reject the hypothesis — the hypothesis may still be the best available
explanation. But the contradiction must be visible to the consumer.

#### MissingEvidence[]

Explicit statement of what evidence WOULD distinguish this hypothesis from
alternatives but is not present in the episode. This is the gap that prevents
definitive classification.

```text
MissingEvidence:
  - Transport-layer acknowledgment receipt — would confirm delivery
  - Target element post-condition check by Environment — would confirm
    whether the action was attempted at the correct coordinates
  - Environment internal retry log — would show whether retries occurred
```

#### FalsificationCondition (at least 1)

A concrete statement of the form: "This hypothesis would be falsified if
evidence X were present in the episode," where X is a specific typed receipt
that could exist but doesn't.

```text
FalsificationConditions:
  1. Would be falsified if TransportAcknowledgment receipt were present
     showing successful delivery to target.
  2. Would be falsified if Environment post-dispatch diagnostic showed
     the action was dispatched to coordinates (x,y) and the target was
     confirmed present at (x,y) throughout the dispatch window.
```

A hypothesis with zero `FalsificationCondition` entries is not a diagnostic
hypothesis — it is an unfalsifiable assertion. The episode MUST label it as
`UNFALSIFIABLE_ASSERTION` and exclude it from the hypothesis set.

### 5.3 Hypothesis set validity

A set of hypotheses is valid for an episode when:

1. Every hypothesis has at least one `FalsificationCondition`.
2. Every hypothesis cites at least one `SupportingEvidence` reference.
3. No two hypotheses are logically identical (differ only in wording).
4. The hypotheses collectively cover the plausible explanations compatible with
   the available evidence.

The set may be empty — the episode remains valid with zero hypotheses. An empty
hypothesis set means the available evidence does not support any falsifiable
explanation. This is an `INSUFFICIENT` stance at the hypothesis layer.

### 5.4 Hypothesis stance

The hypothesis set carries its own stance, independent of individual assessment
stances:

| Stance | Condition |
|---|---|
| `SUPPORTED` | Exactly one hypothesis has supporting evidence, no contradicting evidence, and all other hypotheses in the set have contradicting evidence that eliminates them. |
| `AMBIGUOUS` | Two or more hypotheses have supporting evidence, and none has contradicting evidence that eliminates it. Missing evidence is acknowledged for all surviving hypotheses. |
| `INSUFFICIENT` | No hypothesis reaches the threshold of having at least one supporting evidence reference, OR required evidence facts are missing from the episode that would enable any hypothesis to be formulated. |

### 5.5 Cross-layer stance consistency

The four stances — evidence fact presence, assessment stance, hypothesis stance,
and overall episode stance — are independent. They may differ:

```text
Example: A timeout + world-unchanged episode

  Evidence facts:     PRESENT (ActionResult.TimedOut, pre/post observations)
  Assessment stance:  SUPPORTED (DISPATCH_UNCERTAIN + OBSERVED_NO_EFFECT)
  Hypothesis stance:  AMBIGUOUS (two hypotheses survive:
                       "not delivered" and "delivered but ineffective")
  Episode stance:     AMBIGUOUS (classification is supported but
                       cause is ambiguous)
```

No layer's stance is promoted to override another layer. The episode preserves
all four.

---

## 6. Absorbed and deferred findings

### 6.1 I5 — `BOUNDED_NON_CONVERGENCE` boundary fuzziness

**Disposition: ABSORBED into fact/assessment separation (§4).**

`BOUNDED_NON_CONVERGENCE` is an assessment (Layer 2) that cites:

- N ≥ 2 consecutive `VerificationReceipt.NotSatisfied` evidence facts (Layer 1),
  each for the same expected-effect criterion.
- A terminal receipt of `BudgetExhausted` or equivalent bounded-stop outcome
  from Traversal or Agent.

The precise N is policy-defined, not model-defined. The model requires only that
N ≥ 2 — a single `VERIFICATION_NOT_SATISFIED` is not non-convergence. The
boundary between "repeated verification failure" and "non-convergence" is crossed
when the Traversal/Agent terminates with a bounded-stop outcome after N ≥ 2
consecutive failures on the same criterion.

This is consistent with the assessment structure in §4: the assessment cites
evidence facts (the N verification receipts + the budget-exhausted terminal
receipt) and carries a stance.

### 6.2 I6 — `OBSERVATION_UNAVAILABLE` scenario citation missing

**Disposition: DEFERRED to evidence research.**

The distinction between `OBSERVATION_UNAVAILABLE` (no observation produced) and
`EVIDENCE_INSUFFICIENT` (observation exists but contains UNKNOWN) is
semantically valid and the model correctly separates them. However, the specific
scenario that exercises observation unavailability must be cited from the
existing Scenario corpus before `OBSERVATION_UNAVAILABLE` can be admitted as a
candidate boundary.

This is a research task, not a model refinement. The admission criteria from the
independent validation (§4.4) stand: cite the specific scenario, then minimize
into a Harness-only episode fixture.

---

## 7. Refinement summary

| # | Finding | Severity | Disposition | Section |
|---|---|---|---|---|
| I1 | Simultaneous receipt tiebreaker | LOW | Resolved — structural precedence order | §1 |
| I2 | Missing exclusion rule | MEDIUM | Resolved — explicit inclusion/exclusion with R1–R5 | §2 |
| I3 | Optional RunId correlation gap | MEDIUM | Resolved — three correlation states, UNCORRELATED named honestly | §3 |
| I4 | Fact vs assessment underspecified | LOW | Resolved — two-layer taxonomy, co-existence rule for OBSERVED_NO_EFFECT / VERIFICATION_NOT_SATISFIED | §4 |
| I5 | BOUNDED_NON_CONVERGENCE fuzziness | MEDIUM | Absorbed — assessment citing N ≥ 2 verification failures + budget-exhausted terminal | §6.1 |
| I6 | OBSERVATION_UNAVAILABLE scenario citation | LOW | Deferred — research task, not model refinement | §6.2 |
| I7 | Hypothesis falsifiability unspecified | MEDIUM | Resolved — five-field hypothesis structure with mandatory FalsificationCondition | §5 |

All seven findings are addressed. Zero require implementation. Zero change
Runtime ownership or authority.

---

## 8. Refined model invariants

These replace the extraction's candidate expected requirements (§8 of the
extraction) with the refined versions incorporating all resolutions:

1. **Receipt ordering:** Receipts within an episode are ordered by Frame/step
   sequence and, within the same Frame, by the structural precedence order
   defined in §1.2. No timestamp, filename, or diagnostic text is used for
   ordering.

2. **Evidence window membership:** A receipt enters the evidence window if and
   only if it satisfies at least one of R1–R5 (§2.3). Temporal containment is
   neither necessary nor sufficient.

3. **Correlation honesty:** Every `TraceRun` in an episode context carries
   exactly one correlation state: `RUN_CORRELATED`, `SESSION_CORRELATED`, or
   `UNCORRELATED` (§3.2). Missing correlation edges are preserved as missing.
   No correlation is fabricated from timestamps or diagnostic text.

4. **Fact/assessment separation:** Every assessment MUST cite at least one
   evidence fact (§4.2). An assessment without a cited evidence fact is
   unsupported (`INSUFFICIENT`). Evidence facts remain owned by their existing
   authoritative sources and are never rewritten by Harness.

5. **Assessment composability:** Assessments describe different semantic
   dimensions and may legitimately co-exist in one episode (§4.3). Taxonomical
   co-occurrence is compositional, not ambiguous. No forced mutual exclusion.

6. **Hypothesis falsifiability:** Every diagnostic hypothesis MUST carry at
   least one `FalsificationCondition` (§5.2). A hypothesis without a
   falsification condition is an unfalsifiable assertion and is excluded from
   the hypothesis set.

7. **Stance independence:** Evidence fact presence, assessment stance,
   hypothesis stance, and overall episode stance are independent (§5.5). No
   layer's stance overrides another.

8. **Separate outcomes:** Runtime semantic, dispatch, Recovery, observability,
   and Harness outcomes remain separate (§4.2, Layer 1). No outcome rewrites
   another.

9. **Timeout uncertainty:** `DISPATCH_UNCERTAIN` remains the classification for
   `ActionResult.TimedOut`. Fresh observation decides only supported
   world-effect claims. Timeout alone does not establish action success or
   failure.

10. **Safe refusal:** `AUTHORIZED_REFUSAL` and `EVIDENCE_INSUFFICIENT` are not
    classified as attempted action failure. Zero dispatch is preserved as a
    structural fact.

11. **Harness isolation:** Harness operational/conformance failure is a separate
    outcome and SHALL NOT rewrite Runtime outcome (§3.2, §4.2).

12. **Missing evidence:** Missing or contradictory evidence yields
    `AMBIGUOUS` or `INSUFFICIENT` stance, not invented causation, authority, or
    desired action (§4.4, §5.4).

13. **Structural equivalence:** Exact duration, generated IDs, filenames,
    diagnostic strings, and private method order SHALL NOT define behavioral
    equivalence or episode identity (§1.2, §3.2).

14. **No authority expansion:** A FailureEpisode or assessment SHALL NOT
    authorize dispatch, retry, Recovery, replanning, GoalEvidence, or completion.

---

## 9. Refinement status

```text
REFINEMENT_STATUS
  = COMPLETE

ALL_FINDINGS_ADDRESSED
  = YES (5 resolved, 1 absorbed, 1 deferred)

EXTRACTION_INTEGRITY
  = PRESERVED

NEW_ARCHITECTURE_PRESSURE
  = NONE

RUNTIME_DELTA
  = NONE

OWNERSHIP_DELTA
  = NONE

AUTHORITY_DELTA
  = NONE
```

The refined model is ready for:
- Incorporation into the Reality Model Extraction document (or as a standalone
  amendment).
- FI-05 through FI-08 fixture design using the refined assessment and hypothesis
  structures.

## 10. Explicit non-actions (reaffirmed)

- No production or test code changes.
- No classifier, engine, Provider, registry, Brain, Planner, Graph, or FSM.
- No Runtime `FailureEpisode` type, enum, field, interface, or mutable state.
- No retry, Recovery, dispatch, planning, or completion authority.
- No ownership, authority, dependency, or architecture-invariant change.
- No Reality Model corpus admission and no new canonical pressure.
- No `TraceRun` schema change to make `RunId` non-optional.
- No closing of the `UNCORRELATED` correlation gap.

`FAILURE_EPISODE = HARNESS_ARTIFACT_ONLY`

STOP.
