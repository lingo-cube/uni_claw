# Runtime Failure Episode Independent Validation

> Date: 2026-08-12
> Role: Project Leader / Independent Validator
> Lane: `SEMANTIC_DISCOVERY`
> Input: `docs/decisions/runtime-failure-episode-reality-model-extraction.md`
> Result: `RUNTIME_FAILURE_EPISODE_INDEPENDENT_VALIDATION_RESULT`
> Status: `VALIDATED_WITH_FINDINGS`
> Implementation authority: **NOT GRANTED**

## 0. Validation mandate

This validation is adversarial. It attempts to falsify, not confirm. Every finding
that survives challenge is stronger for it. Every finding that doesn't is a
necessary correction.

Scope: challenge provenance, the non-linear episode boundary, taxonomy
composability/minimality, the five candidate missing boundaries, and FI-05
through FI-08 evidence gaps.

Out of scope: implementation, classifier design, Runtime model admission,
authority expansion.

---

## 1. Episode boundary validation

### 1.1 Can start boundary be determined without timestamps, filenames, diagnostic text?

**Verdict: YES, with one unresolved edge case.**

The extraction defines six start variants, each anchored to a typed receipt:

| Start variant | Anchor | Timestamp-free? | Filename-free? | Diagnostic-text-free? |
|---|---|---|---|---|
| Pre-run insufficiency | `IntentCompilationResult.Insufficient` | Yes | Yes | Yes |
| Pre-dispatch Runtime | Fresh observation, authorization receipt, binding receipt, contradiction, or startup verification receipt | Yes | Yes | Yes |
| Dispatch | Exact action dispatch/result pair (Rejected/TimedOut) | Yes | Yes | Yes |
| Post-dispatch | First fresh observation + explicit expected-effect comparison | Yes | Yes | Yes |
| Recovery | Antecedent Trap/failure receipt | Yes | Yes | Yes |
| Harness | Capture, projection, replay-conformance, listener, or store operation fault record | Yes | Yes | Yes |

Each anchor is a typed structural receipt with a stable identifier. No timestamp,
filename, or free-form diagnostic string is required to locate the boundary.

**Finding 1.1-A (UNRESOLVED): Simultaneous receipt ordering.**

When two typed receipts arrive in the same observation/execution cycle — e.g.,
a `TimedOut` dispatch and a fresh Observation that both qualify as episode starts
— the model provides no tiebreaker. "Earliest directly recorded receipt" is
underspecified when receipts share the same Frame or execution step.

**Recommendation:** Add an explicit tiebreaker rule: when receipts share the
same Frame/step, the dispatch receipt takes precedence for dispatch-boundary
episodes, and the observation receipt takes precedence for world-evidence
episodes. For Recovery episodes, the Trap always precedes the Recovery attempt
by construction, so no tiebreaker is needed.

### 1.2 Can terminal boundary be determined without timestamps, filenames, diagnostic text?

**Verdict: YES.**

The five terminal variants all anchor to typed structural receipts:

| Terminal variant | Anchor |
|---|---|
| Insufficient / refusal | Confirmed zero dispatch |
| Uncertain dispatch | Required fresh observation + verification / Agent adjudication |
| Recovery | Recovery verification result + Agent resume/terminal adjudication |
| Agent terminal | `SemanticRunResult` / final RunState |
| Harness | Harness capture/projection/persistence/conformance result |

Each receipt has a stable type and identifier.

**Finding 1.2-A (CONFIRMED): Absence handling is correctly bounded.**

The model states: "If required evidence never arrives, the bounded terminal
receipt ends the window while the assessment remains `INSUFFICIENT`; absence is
not replaced by `world unchanged`, rejection, success, or failure." This is a
critical honesty constraint. It prevents the model from fabricating a terminal
state when evidence is genuinely missing.

**Finding 1.2-B (CONFIRMED): Successfully handled antecedent failure.**

"A successfully handled antecedent failure may end at verified handling/resume
even when the overall run later succeeds." This prevents the model from
stretching the episode to the run boundary when the failure was already resolved.
Correct.

### 1.3 Can evidence window membership be determined without timestamps, filenames, diagnostic text?

**Verdict: YES, with one completeness concern.**

The five membership rules use only typed references and explicit relations:

1. Start and end receipts — typed, stable IDs.
2. Stable typed references necessary to establish the selected boundary.
3. Explicit predecessor/next, action/observation, Trap/Recovery, or propagation relations.
4. Relevant Runtime, dispatch, Recovery, and Harness outcomes kept separate.
5. Explicitly declared contradictory or missing evidence.

**Finding 1.3-A (CONCERN — COMPLETENESS): No rule for excluding redundant receipts.**

The model defines what the window *contains*. It does not define a rule for
excluding receipts that are temporally within the window but semantically
unrelated. Consider: a Run contains multiple Traversal steps. Only step 3 fails.
Steps 1, 2, 4, and 5 are within the temporal window but unrelated to the failure.
Without an exclusion rule, a naive reader might include all steps.

**Recommendation:** Add an explicit exclusion rule: a receipt is excluded from
the evidence window unless it satisfies one of the five inclusion rules. The
window is defined by explicit typed references, not by temporal containment.

**Finding 1.3-B (CONFIRMED): Shared evidence referencing is sound.**

"Shared evidence may be referenced by linked episodes without copying or
rewriting its authoritative content." This prevents evidence duplication and
ensures that episodes referencing the same fact don't create divergent copies.

### 1.4 Boundary validation summary

| Criterion | Status |
|---|---|
| Start boundary without timestamps | VALID (1 unresolved edge) |
| Terminal boundary without timestamps | VALID |
| Evidence window without timestamps | VALID (1 completeness concern) |
| No filename dependency | VALID |
| No diagnostic text dependency | VALID |

---

## 2. Partial evidence graph validation

### 2.1 Edge existence audit

The extraction proposes this graph:

```
Scenario ------------------------------+
  | optional ScenarioId                |
  v                                    |
Run / CaptureSession                    |
  | RunId / CaptureSessionId           |
  +--> TraceRun -- spans/events         |
  |       (operational facts)           |
  |                                    |
  +--> FrameSequence --> Frame          |
  |                       |             |
  |                       +--> Observation
  |                       +--> artifacts|
  |                                    |
  +--> Action dispatch --> ActionResult |
  +--> Trap -----------> RecoveryResult |
  +--> GoalEvidence ---> SemanticRunResult
  +--> Harness persistence/conformance result
                                           |
                                           v
                         Harness FailureEpisode
                         (references only)
```

**Finding 2.1-A (CONFIRMED): `TraceRun` → `Frame` edge is truthfully absent.**

The model states "There is no truthful mandatory edge from `TraceRun` to `Frame`
in the current production model." This is correct. `TraceRun` carries
`TraceRunId`, optional `TraceId`, and optional `RunId`. Frames belong to
Capture/Replay assets. The observability graduation document confirms:
"TraceRun remains distinct from the Harness TraceCaptureSession lifecycle."

**Finding 2.1-B (CONFIRMED): Linear chain correctly rejected.**

The linear form `Scenario -> TraceRun -> Frame -> Observation -> Action ->
Outcome -> FailureEpisode` would fabricate edges for:
- Pre-run insufficiency (no Run, no TraceRun, no Frame, no Observation, no Action)
- Zero-dispatch refusal (Observations exist, no Action)
- Rejected action (ActionResult exists, no post-action Observation may exist)
- Partial capture (some edges exist, others don't)
- Post-run persistence failure (Runtime outcome complete, Harness failed after)

Each of these is a legitimate episode shape. The linear chain would either
exclude them or fabricate missing evidence. Rejection confirmed.

### 2.2 Missing edge preservation

**Finding 2.2-A (CONFIRMED): Missing IDs stay absent.**

"Correlation may use explicit Run, Trace, CaptureSession, Frame, Observation
sequence, action, and GoalEvidence IDs where present. Missing IDs stay absent."
This is a necessary honesty constraint.

**Finding 2.2-B (CONCERN — CORRELATION GAP): Optional `RunId` on `TraceRun`.**

The model notes that `TraceRun` has an *optional* `RunId`. If a `TraceRun` exists
but its `RunId` is absent, correlating it to the correct Run/CaptureSession
requires an alternative correlation path that the model does not specify. The
`TraceId` field is also optional.

**Recommendation:** Either (a) document that `RunId`-less `TraceRun` instances
are correlated via `CaptureSessionId` (if present) or remain uncorrelated, or
(b) add a note that this is a known correlation gap in the current production
model that a future explicit correlation edge would close.

### 2.3 No inferred correlation

**Finding 2.3-A (CONFIRMED): Correlation is not causation.**

The model explicitly states "Correlation is not causation" and preserves this
distinction throughout. The episode references facts; it does not assert causal
relationships between them.

**Finding 2.3-B (CONFIRMED): No temporal-order inference.**

The model does not use temporal proximity or call order to infer edges. "An
unrelated earlier span or Frame is excluded merely because it occurred first."
This is stated in the boundary rules and holds throughout.

### 2.4 Graph validation summary

| Criterion | Status |
|---|---|
| Named edges are truthful | VALID |
| Missing edges remain missing | VALID (1 correlation gap) |
| Linear chain rejected | VALID |
| No temporal-order inference | VALID |
| No causation-from-correlation | VALID |

---

## 3. Taxonomy stress test

### 3.1 Individual category stress

#### DISPATCH_REJECTED

**Claim:** Exact external dispatch receipt is Rejected; does not prove world
relation or root cause.

**Stress:** Could `DISPATCH_REJECTED` be confused with `AUTHORIZED_REFUSAL` when
the rejection comes from an authorization check rather than the Environment?

**Verdict: NO AMBIGUITY.** `DISPATCH_REJECTED` requires an actual dispatch
attempt that returned `Rejected`. `AUTHORIZED_REFUSAL` is a pre-dispatch decision
with zero dispatch. The distinction is preserved by the presence/absence of an
`ActionResult`. An authorization check that prevents dispatch produces no
`ActionResult`; an Environment rejection produces `ActionResult.Rejected`.

#### DISPATCH_UNCERTAIN

**Claim:** Exact dispatch receipt is TimedOut; uncertainty survives until fresh
evidence narrows only the world-effect question.

**Stress:** Could `DISPATCH_UNCERTAIN` + world-changed fresh observation be
misread as `DISPATCH_REJECTED` was wrong?

**Verdict: NO AMBIGUITY.** The model explicitly preserves both: "world changed
branch: expected effect may be verified from Observation, while timeout remains
the dispatch report." The dispatch classification remains `DISPATCH_UNCERTAIN`;
the world-effect classification is separate. No category is rewritten.

#### OBSERVED_NO_EFFECT

**Claim:** Explicitly scoped pre/post observations show no expected change. Does
not prove non-delivery or failed dispatch.

**Stress:** In a scenario where an explicit verification criterion exists, should
`OBSERVED_NO_EFFECT` be subsumed by `VERIFICATION_NOT_SATISFIED`?

**Verdict: PARTIAL AMBIGUITY.** The extraction states: "`OBSERVED_NO_EFFECT`
becomes `VERIFICATION_NOT_SATISFIED` only when an explicit expected-effect
criterion and fresh evidence exist." This is a one-directional rule (when X,
then Y). It does not state whether `OBSERVED_NO_EFFECT` should *always* be
subsumed when the criterion exists, or whether both labels can co-exist.

**Recommendation:** Clarify: when an explicit verification criterion exists and
is evaluated against fresh evidence, `VERIFICATION_NOT_SATISFIED` is the
classification; `OBSERVED_NO_EFFECT` is the supporting world-relation evidence.
They are not alternatives — one is a classification boundary, the other is an
evidence fact. The episode may carry both.

#### VERIFICATION_NOT_SATISFIED

**Claim:** An explicit expected-effect criterion is evaluated against fresh
evidence and is not satisfied.

**Stress:** Could this be confused with `BINDING_UNRESOLVED` when the
verification target disappears?

**Verdict: NO AMBIGUITY.** `BINDING_UNRESOLVED` is a pre-condition boundary
(interaction grounding is unsupported). `VERIFICATION_NOT_SATISFIED` is a
post-condition boundary (an expectation was evaluated and failed). A binding that
disappears between dispatch and verification would produce:
1. `BINDING_UNRESOLVED` at the time of verification (the target is no longer
   interactable), and
2. potentially a prior dispatch fact with a different classification.

These are temporally and semantically distinct.

#### BINDING_UNRESOLVED

**Claim:** Required interaction grounding is unresolved; does not identify the
perception/root cause.

**Stress:** Could `BINDING_UNRESOLVED` be confused with `EVIDENCE_INSUFFICIENT`
when the binding failure is due to missing perception evidence?

**Verdict: NO AMBIGUITY.** The extraction correctly distinguishes: "known state
with unresolved target binding and unknown state with known binding are
separately reproducible." `BINDING_UNRESOLVED` means we know the state but can't
ground the interaction. `EVIDENCE_INSUFFICIENT` means we don't know the state.
These are orthogonal dimensions.

#### EVIDENCE_INSUFFICIENT

**Claim:** Evidence cannot support the required state/decision. It is not OFF,
rejection, or observation transport failure.

**Stress:** Is there a scenario where `EVIDENCE_INSUFFICIENT` and
`OBSERVATION_UNAVAILABLE` (candidate missing boundary) collide?

**Verdict: BOUNDARY EXISTS BUT IS CURRENTLY THEORETICAL.**
`EVIDENCE_INSUFFICIENT` means: we have an observation, but the evidence it
contains is UNKNOWN. `OBSERVATION_UNAVAILABLE` would mean: we have no observation
at all. The distinction is clear in principle but `OBSERVATION_UNAVAILABLE` has
no minimized accepted episode fixture. See §4.4.

#### AUTHORIZED_REFUSAL

**Claim:** A safety/authority boundary intentionally prevents dispatch. It can
co-occur with an evidence reason and is not an execution fault.

**Stress:** Can a single episode carry both `AUTHORIZED_REFUSAL` and
`EVIDENCE_INSUFFICIENT` without ambiguity?

**Verdict: NO AMBIGUITY.** The extraction explicitly allows co-occurrence:
"`AUTHORIZED_REFUSAL` is a handling disposition; `EVIDENCE_INSUFFICIENT` or an
authorization receipt states why dispatch was withheld." These are different
semantic layers: disposition vs. reason. An episode that carries both is more
informative, not more ambiguous.

#### RECOVERY_VERIFICATION_FAILED

**Claim:** Recovery action/result plus fresh evidence fail the Recovery
criterion; antecedent evidence remains linked.

**Stress:** Could `RECOVERY_VERIFICATION_FAILED` be confused with
`VERIFICATION_NOT_SATISFIED`?

**Verdict: NO AMBIGUITY.** `RECOVERY_VERIFICATION_FAILED` is scoped to a Recovery
attempt and always links to its antecedent Trap/failure. `VERIFICATION_NOT_SATISFIED`
is a general post-condition boundary without a Recovery context. The Recovery
variant carries the Trap → Recovery → Verification chain; the general variant
carries only expectation → evidence → not-satisfied.

### 3.2 Overlap matrix

| Category A | Category B | Overlap? | Resolution |
|---|---|---|---|
| DISPATCH_REJECTED | AUTHORIZED_REFUSAL | No | Dispatch presence distinguishes |
| DISPATCH_UNCERTAIN | OBSERVED_NO_EFFECT | Yes, compositional | Different dimensions (dispatch certainty vs. world state) |
| OBSERVED_NO_EFFECT | VERIFICATION_NOT_SATISFIED | Yes, when criterion exists | Recommendation: treat as evidence fact + classification boundary |
| BINDING_UNRESOLVED | EVIDENCE_INSUFFICIENT | No | Known-state/unknown-binding vs. unknown-state/known-binding |
| EVIDENCE_INSUFFICIENT | AUTHORIZED_REFUSAL | Yes, compositional | Disposition vs. reason at different layers |
| RECOVERY_VERIFICATION_FAILED | VERIFICATION_NOT_SATISFIED | No | Recovery-scoped vs. general |

**Finding 3.2-A (CONFIRMED): Mutual exclusion is not required.**

Every overlap is compositional — the categories describe different semantic
dimensions. Forcing mutual exclusion would lose the dispatch/world/verification/
authorization distinctions. The extraction's finding that "categories cannot
honestly be one mutually exclusive root-cause enum" is validated.

### 3.3 Taxonomy validation summary

| Criterion | Status |
|---|---|
| No false overlap (same dimension, different name) | VALID |
| Compositional overlap correctly identified | VALID (1 clarification needed) |
| Each category has a distinct anchor receipt | VALID |
| No category collapses safe refusal into failure | VALID |
| No category normalizes UNKNOWN into OFF/failure | VALID |
| `HARNESS_OPERATIONAL_FAILURE` is too broad | VALID — needs sub-boundaries |

---

## 4. Missing candidate validation

### 4.1 SEMANTIC_CONTRADICTION

**Extraction claim:** "Executable page/evidence contradiction exists. MISSING
from current taxonomy; retain as a distinct semantic adjudication boundary, not
an external fault cause."

**Validation:**

The `SemanticRunResult` vocabulary already encodes contradiction handling —
evidence supports incompatible conclusions about the same fact. This is
semantically distinct from:
- `VERIFICATION_NOT_SATISFIED`: one expectation failed (not two incompatible
  conclusions).
- `EVIDENCE_INSUFFICIENT`: evidence is absent or UNKNOWN (not contradictory).
- `BINDING_UNRESOLVED`: can't ground interaction (not contradiction between
  grounded facts).

**Verdict: CONFIRMED MISSING.** A contradiction between two pieces of evidence
cannot be honestly classified under any existing category. Adding
`SEMANTIC_CONTRADICTION` as a distinct boundary would close a genuine semantic
gap. It must remain a semantic adjudication boundary (Agent domain), not be
recast as an external fault.

**Admission requirement:** Minimize a scenario where two observations of the same
target produce incompatible but individually well-supported conclusions. The
existing executable contradiction evidence should be reducible to a Harness-only
episode.

### 4.2 BOUNDED_NON_CONVERGENCE

**Extraction claim:** "Executable stuck-world/budget exhaustion exists. MISSING;
distinguish repeated verified non-satisfaction from one `OBSERVED_NO_EFFECT`
event."

**Validation:**

The distinction is real:
- One `OBSERVED_NO_EFFECT` = single observation pair shows no change.
- `BOUNDED_NON_CONVERGENCE` = N attempts (N > 1) each verified, each not
  satisfied, bounded by budget or policy.

**Verdict: CONFIRMED MISSING, with a boundary fuzziness concern.**

The boundary between "repeated `VERIFICATION_NOT_SATISFIED`" and
`BOUNDED_NON_CONVERGENCE` is not well-defined. The extraction doesn't specify
at what N or under what policy the transition occurs.

**Recommendation:** Define `BOUNDED_NON_CONVERGENCE` as: the same expected-effect
criterion is evaluated against fresh evidence on N ≥ 2 consecutive attempts, each
resulting in `VERIFICATION_NOT_SATISFIED`, and the Traversal/Agent terminates
with `BudgetExhausted` or equivalent bounded-stop outcome. The episode must
reference the sequence of verification failures, not merely count them.

**Admission requirement:** Minimize a stuck-world scenario (existing in
Simulation H4/C1, closed-loop budget tests) into a Harness-only episode that
preserves the attempt sequence.

### 4.3 CANCELLED

**Extraction claim:** "Trace outcome vocabulary and cancellation-aware ports
exist; no accepted end-to-end episode. MISSING but EVIDENCE-GATED; do not
normalize into failure."

**Validation:**

The observability graduation confirms `CANCELLED` exists in the span outcome
vocabulary. The extraction correctly identifies:
- Cancellation is a control/lifecycle boundary, not a semantic failure.
- There is no end-to-end fixture proving cancellation produces a valid episode.
- The model must not normalize cancellation into `ExecutionFailed`.

**Verdict: CONFIRMED MISSING, CORRECTLY GATED.** The extraction's refusal to
admit cancellation as a failure category without evidence is correct. The
evidence gate is FI-06.

### 4.4 OBSERVATION_UNAVAILABLE

**Extraction claim:** "Architecture/scenario pressure exists; no minimized
accepted episode fixture. MISSING but EVIDENCE-GATED; must remain distinct from
an Observation containing UNKNOWN evidence."

**Validation:**

The distinction from `EVIDENCE_INSUFFICIENT` is critical:
- `OBSERVATION_UNAVAILABLE`: no observation was produced at all (Environment
  returned nothing, capture failed, or the observation stream was interrupted).
- `EVIDENCE_INSUFFICIENT`: an observation exists but contains UNKNOWN state
  evidence.

**Verdict: CONFIRMED MISSING, CORRECTLY GATED.** The absence of an observation is
a different terminal condition than an observation with UNKNOWN content. The
evidence gate is appropriate.

**Concern:** The extraction identifies "architecture/scenario pressure" but does
not cite which scenario. The admission criteria should require citing the
specific scenario that exercises observation unavailability.

### 4.5 HARNESS_CONFORMANCE_FAILURE

**Extraction claim:** "Replay divergence, asset exhaustion, invalid span
graph/outcome are executable. MISSING sub-boundary; distinguish behavior/evidence
mismatch from store/listener operational failure."

**Validation:**

The extraction correctly splits `HARNESS_OPERATIONAL_FAILURE` into:
- **Operational failure**: store, listener, projection, capture lifecycle failure
  (infrastructure/code fault).
- **Conformance failure**: replay divergence, asset exhaustion, invalid span
  graph/outcome, Scenario observability assertion failure (behavior/evidence
  mismatch).

These have different evidence shapes:
- Operational failure: the Harness mechanism itself broke.
- Conformance failure: the Harness mechanism worked, but the captured evidence
  doesn't match expectations.

**Verdict: CONFIRMED MISSING, WELL-SCOPED.** The sub-boundary is executable
(ObservationReplayTests, observability conformance tests) and semantically
distinct.

### 4.6 Missing candidate summary

| Candidate | Status | Admission gate |
|---|---|---|
| SEMANTIC_CONTRADICTION | MISSING, confirmed gap | Minimize contradiction scenario into Harness-only episode |
| BOUNDED_NON_CONVERGENCE | MISSING, confirmed gap (fuzzy boundary) | Minimize stuck-world scenario preserving attempt sequence |
| CANCELLED | MISSING, correctly gated | FI-06 fixture required |
| OBSERVATION_UNAVAILABLE | MISSING, correctly gated | Cite specific scenario; minimize fixture |
| HARNESS_CONFORMANCE_FAILURE | MISSING, well-scoped | Already executable; needs formal boundary definition |

---

## 5. FI-05 through FI-08 admission criteria

### 5.1 FI-05 — Harness failure isolation

**Current status:** Success-path only. `ObservabilityConformanceTests.
GoldenRun_RecordsObservabilityTrace` proves successful persistence. No injected
failure fixture exists.

**Minimum evidence required:**

1. **Variant A** — Runtime succeeds, Harness fails:
   - One Runtime run that completes with `SemanticRunResult` success.
   - One independently injected Harness failure (store write failure, listener
     exception, or projection error).
   - Runtime outcome independently preserved and verifiable as success.
   - Harness outcome independently preserved as failure with explicit failure
     reason.
   - Partial trace honest: reports exactly what was captured before failure, no
     fabricated completion.
   - No edge from Harness outcome to Runtime outcome.

2. **Variant B** — Runtime fails, Harness fails:
   - One Runtime run that completes with `SemanticRunResult` failure.
   - One independently injected Harness failure.
   - Runtime outcome independently preserved and verifiable as failure.
   - Harness outcome independently preserved as failure.
   - Neither outcome rewrites the other.

3. **Variant C** (stretch) — Runtime fails, Harness succeeds:
   - Already partially proven by existing tests where Runtime errors are captured
     in trace. Formalize as a named variant.

**Fixture requirement:**

- Test-only `ITraceCaptureStore` implementation with a configurable failure mode
  (throw on write, return failure result, simulate partial write).
- Test-only trace listener with configurable failure mode.
- Test-only trace projector with configurable failure mode.
- Each fixture must be injectable without changing Runtime production code.
- The fixture must not leak into the Runtime spine — it is Harness-owned.

**Oracle requirement:**

- For Variant A: assert `RuntimeOutcome == Success AND HarnessOutcome == Failed
  AND PartialTrace.IsHonest == true AND PartialTrace.IsComplete == false`.
- For Variant B: assert `RuntimeOutcome == Failed AND HarnessOutcome == Failed
  AND PartialTrace.IsHonest == true`.
- For all variants: assert `HarnessOutcome` cannot change `RuntimeOutcome`.
- For all variants: assert no fabricated trace completion, no synthesized
  evidence, no retry.
- For all variants: assert missing trace evidence is reported as missing, not
  reconstructed from diagnostic text or temporal proximity.

### 5.2 FI-06 — Cancellation boundary

**Current status:** Evidence gap. Cancellation-aware APIs and `CANCELLED`
vocabulary exist. No deterministic caller-cancelled run/capture episode or
terminal oracle.

**Minimum evidence required:**

1. One deterministic caller-initiated cancellation at a named public boundary
   (e.g., during `Traversal.ExecuteAsync`, during `Container.RefreshAsync`).
2. Prior facts preserved up to the cancellation point:
   - Completed spans with their actual outcomes.
   - Observations captured before cancellation.
   - Actions dispatched before cancellation with their `ActionResult`.
3. Child spans closed before parent span.
4. Parent span outcome = `CANCELLED`.
5. Runtime outcome independently preserved if Runtime emitted one before/aside
   from cancellation.
6. Zero fabricated world-relation claims: no "world unchanged" inferred from
   missing post-cancellation evidence.

**Fixture requirement:**

- A `CancellationTokenSource` that is cancelled at a deterministic point in the
  execution pipeline. The cancellation point must be at a named public boundary
  (not a private method or internal callback).
- The fixture must allow verification of what was recorded before cancellation.
- The fixture must not simulate cancellation by throwing an exception that
  bypasses the normal cancellation path — it must exercise the actual
  `CancellationToken` propagation.

**Oracle requirement:**

- `ParentSpan.Outcome == CANCELLED`, not `FAILED`.
- All child spans are closed (no orphan spans).
- Prior completed spans preserve their actual outcomes (not rewritten to
  `CANCELLED`).
- Zero world-relation claims derived from missing post-cancellation evidence.
- If Runtime independently emitted a terminal outcome, it is preserved as-is
  (not overwritten by cancellation).
- Cancellation is classified as `CANCELLED` boundary, not as `ExecutionFailed`,
  `DISPATCH_REJECTED`, or any existing failure category.

### 5.3 FI-07 — Multiple plausible causes

**Current status:** Pressure exists (SC-P3-001 negative fixture shows timeout +
world unchanged) but no alternative-preservation oracle.

**Minimum evidence required:**

1. One identical structured episode (same typed facts: same IDs, types, outcomes,
   observation sequences, action results) that supports at least two causally
   distinct hypotheses.
2. The two hypotheses must be genuinely distinct — "not delivered" vs. "delivered
   but ineffective" for a timeout + world-unchanged case qualifies. "Timeout due
   to network" vs. "timeout due to target busy" does not qualify unless there is
   distinguishable evidence for each.
3. No additional evidence (beyond the typed episode facts) that distinguishes
   between the hypotheses.
4. The episode facts alone must not logically force one hypothesis over the other.

**Fixture requirement:**

- A single replay/capture asset based on SC-P3-001 negative variant (timeout +
  world unchanged).
- The asset must contain only typed structural facts (no embedded diagnostic
  strings masquerading as evidence).
- The assessment must receive ONLY this asset — no side-channel information,
  no scenario metadata beyond what's in the episode, no timing data.

**Oracle requirement:**

- The assessment MUST return at least two distinct hypotheses OR
  `INSUFFICIENT`.
- The assessment MUST NOT return a single root cause.
- The assessment MUST NOT rank hypotheses (no "most likely", no ordering).
- The assessment MUST NOT use diagnostic text, timing, or call order as
  tiebreakers.
- The assessment MUST NOT recommend action (retry, recovery, replanning).
- No numeric confidence, probability, or score.
- If hypotheses are returned, each must cite the specific evidence that supports
  it and acknowledge the evidence that contradicts or fails to distinguish it.

### 5.4 FI-08 — Structural determinism

**Current status:** Deterministic baselines exist (SC-P3-001, SC-P2-003,
Observation Replay, bounded safety, golden replays). No structured-equivalence
perturbation fixture.

**Minimum evidence required:**

1. Two episodes with identical stable typed facts:
   - Same scenario ID, run ID structure, trace structure.
   - Same observation sequences (same Observations, same order).
   - Same action results (same ActionResult values).
   - Same Agent outcomes, Trap/Recovery references, GoalEvidence references.
2. Different:
   - Exact timestamps / durations.
   - Generated span IDs, run IDs, trace IDs.
   - Free-form diagnostic strings (`Reason`, `Info`, exception messages).

**Fixture requirement:**

- Paired Harness-only manifests derived from one deterministic scenario
  (SC-P3-001 or SC-P2-003).
- Manifest A: timing set T1, generated IDs G1, diagnostic strings D1.
- Manifest B: timing set T2 ≠ T1, generated IDs G2 ≠ G1, diagnostic strings
  D2 ≠ D1 (string content differs, not just byte-identical).
- All stable typed facts identical between A and B.
- The fixture must prove that the manifests are genuinely equivalent in
  structure — not just accidentally identical because the perturbation was
  too small.

**Oracle requirement:**

- Both manifests produce identical boundary classifications.
- Both manifests produce identical correlation structure (same references,
  same edges between same typed entities).
- No assertion on timing equality, diagnostic string equality, or generated
  ID equality.
- Diagnostic strings retained only as payload if policy permits, never as
  classification input.
- If classifications differ between A and B, FI-08 is FALSIFIED — the model
  has an undocumented dependency on non-structural evidence.
- If classifications are identical but correlation structure differs, FI-08 is
  PARTIALLY FALSIFIED — the correlation rules have a hidden dependency.

### 5.5 FI-05 through FI-08 summary

| FI | Status | Blocker |
|---|---|---|
| FI-05 | GATED — no injected failure fixture | Requires test-only `ITraceCaptureStore` with configurable failure |
| FI-06 | GATED — no cancellation fixture | Requires deterministic cancellation at named public boundary |
| FI-07 | GATED — no alternative-preservation oracle | Requires oracle that refuses to pick one root cause |
| FI-08 | GATED — no perturbation fixture | Requires paired manifests with timing/diagnostic drift |

---

## 6. Cross-cutting validation

### 6.1 Provenance consistency

**Finding 6.1-A (CONFIRMED): Evidence maturity is correctly stratified.**

The extraction's evidence maturity freeze (§2.1) correctly distinguishes:
- E1: deterministic simulation
- E3/REALITY_SEEDED: replay reconstructed from recorded reality with
  normalization/synthesis
- E4: raw recorded emulator/device evidence (only for directly recorded fields)
- E0: document-only legacy pressure

No evidence is upgraded by proximity to other evidence. "Trace timing,
free-form diagnostics, filenames, and private method order do not upgrade
evidence strength or establish correlation." This is an essential honesty
constraint.

### 6.2 Inference boundary

**Finding 6.2-A (CONFIRMED): Four-layer stratification holds.**

The four layers (direct fact, correlation, classification, diagnostic
hypothesis) are correctly separated:

- Direct facts are immutable recorded receipts from existing owners.
- Correlation groups facts by explicit references.
- Classification is a derived label supported by cited facts.
- Diagnostic hypothesis is a falsifiable possible explanation.

No layer is promoted to the authority of the layer above it. The episode
"remains valid with zero assessments" — classification and hypotheses are
optional envelopes, not required content.

**Finding 6.2-B (CONCERN — TESTABILITY): Hypothesis falsifiability is asserted
but not specified.**

The model states diagnostic hypotheses are "falsifiable" but provides no
mechanism for falsification. Without a falsification criterion, a hypothesis
is indistinguishable from an assertion.

**Recommendation:** Each diagnostic hypothesis must carry at least one
falsification condition: an explicit statement of what evidence WOULD disprove
the hypothesis. "If transport acknowledgment had been received, this hypothesis
would be falsified."

### 6.3 Authority boundary

**Finding 6.3-A (CONFIRMED): One-way data flow is preserved.**

```
Runtime / Environment owners emit their existing authoritative receipts
                       |
                       v
Harness capture, replay, and TraceRun artifacts
                       |
                       v
Harness FailureEpisode correlation
                       |
                       v
optional non-authoritative classification / hypotheses

NO RETURN EDGE TO RUNTIME
```

No arrow returns to Runtime. Harness may collect, correlate, minimize, persist,
structurally compare, and state supported/ambiguous/insufficient hypotheses.
Harness may not dispatch, retry, recover, select, mutate, or alter any Runtime
decision. This is architecturally sound.

**Finding 6.3-B (CONFIRMED): Existing owner authority is unchanged.**

Agent, Container, Traversal, Environment, and Recovery ownership boundaries
remain intact. No existing semantic contract is modified.

### 6.4 Counterfactual stress

The extraction lists six counterfactuals that would falsify or narrow it. I
independently evaluate each:

| Counterfactual | Current evidence | Would falsify if... |
|---|---|---|
| Runtime genuinely requires consuming the correlated episode during the same run | No such requirement exists | A Scenario proves Runtime must read a FailureEpisode to make a decision |
| Explicit existing IDs cannot correlate the minimum episode | IDs exist (RunId, TraceRunId, CaptureSessionId, etc.) but some are optional | A minimum episode cannot be correlated without changing a frozen contract |
| Timeout deterministically establishes action success/failure | SC-P3-001 proves timeout ≠ success and timeout ≠ failure | A Scenario proves timeout alone determines action outcome |
| Harness failure changes independently recorded Runtime result | No such path exists | A test proves Harness outcome overwrites Runtime outcome |
| Categories must be mutually exclusive | Taxonomy stress test confirms composability | An approved Scenario requires mutual exclusion |
| Diagnostic text/timing/call order is the only stable evidence | Multiple deterministic replays exist | A required distinction can only be made via diagnostic string |

**Finding 6.4-A (CONFIRMED): No counterfactual is currently evidenced.**

None of the six counterfactuals has executable evidence supporting it. The
model survives adversarial counterfactual challenge.

### 6.5 What the model does NOT claim (and shouldn't)

The model correctly refrains from claiming:

- That every FailureEpisode fact is a failure (it may contain safe refusal,
  insufficiency, or independent Harness fault).
- That correlation is causation.
- That one label is a root cause.
- That classification enables action.
- That the episode is a Runtime semantic model.
- That the episode authorizes dispatch, retry, Recovery, or completion.
- That the taxonomy is a severity ladder.

**Finding 6.5-A (CONFIRMED): Restraint is appropriate.**

These non-claims are as important as the positive claims. They prevent the
model from being misinterpreted as an implementation authorization.

---

## 7. Aggregate findings

### 7.1 Confirmed strengths

| # | Finding | Severity |
|---|---|---|
| S1 | Episode boundaries anchored to typed structural receipts, not timestamps/diagnostics | — |
| S2 | Partial evidence graph truthfully preserves missing edges | — |
| S3 | Linear chain correctly rejected — would fabricate edges for 5 legitimate episode shapes | — |
| S4 | Taxonomy composability validated — no false mutual exclusion | — |
| S5 | Four-layer stratification (fact/correlation/classification/hypothesis) holds under stress | — |
| S6 | One-way data flow (Runtime → Harness, no return edge) architecturally sound | — |
| S7 | All six counterfactuals remain un-evidenced | — |
| S8 | Existing owner authority boundaries unchanged | — |
| S9 | Evidence maturity stratification (E0–E4) correct and conservative | — |
| S10 | Absence handling honest — missing evidence stays missing, not fabricated | — |

### 7.2 Issues requiring resolution

| # | Finding | Severity | Recommendation |
|---|---|---|---|
| I1 | Simultaneous receipt tiebreaker unspecified (§1.1) | LOW | Add explicit tiebreaker: dispatch receipt for dispatch episodes, observation receipt for world-evidence episodes |
| I2 | No exclusion rule for temporally-in-window but semantically-unrelated receipts (§1.3) | MEDIUM | Add: a receipt is excluded unless it satisfies an explicit inclusion rule; window = typed references, not temporal containment |
| I3 | Optional `RunId` on `TraceRun` creates correlation gap (§2.2) | MEDIUM | Document known gap; specify whether `CaptureSessionId` closes it or correlation remains impossible |
| I4 | `OBSERVED_NO_EFFECT` / `VERIFICATION_NOT_SATISFIED` subsumption direction underspecified (§3.1) | LOW | Clarify: they are evidence fact + classification boundary, not alternatives; episode may carry both |
| I5 | `BOUNDED_NON_CONVERGENCE` boundary fuzziness (§4.2) | MEDIUM | Define as N ≥ 2 consecutive `VERIFICATION_NOT_SATISFIED` on same criterion, terminated by budget/policy exhaustion |
| I6 | `OBSERVATION_UNAVAILABLE` scenario citation missing (§4.4) | LOW | Require specific scenario citation in admission criteria |
| I7 | Hypothesis falsifiability asserted but mechanism unspecified (§6.2) | MEDIUM | Require each hypothesis to carry at least one explicit falsification condition |

### 7.3 FI-05 through FI-08 readiness

| Gate | Minimum evidence | Fixture | Oracle | Ready? |
|---|---|---|---|---|
| FI-05 | Defined (§5.1) | Defined (§5.1) | Defined (§5.1) | No — fixture not built |
| FI-06 | Defined (§5.2) | Defined (§5.2) | Defined (§5.2) | No — fixture not built |
| FI-07 | Defined (§5.3) | Defined (§5.3) | Defined (§5.3) | No — oracle not built |
| FI-08 | Defined (§5.4) | Defined (§5.4) | Defined (§5.4) | No — fixture not built |

---

## 8. Validation verdict

```text
VALIDATION_STATUS
  = VALIDATED_WITH_FINDINGS

EXTRACTION_INTEGRITY
  = CONFIRMED

FALSIFICATION_ATTEMPTED
  = YES

FALSIFICATION_SUCCEEDED
  = NO

ISSUES_FOUND
  = 7 (0 critical, 3 medium, 4 low)

BLOCKING_ISSUES
  = 0
```

The Reality Model Extraction survives independent adversarial validation. No
finding falsifies the core claims:

1. Episode boundaries are determinable from typed structural receipts without
   timestamps, filenames, or diagnostic text.
2. The partial evidence graph is truthful — mandatory edges that don't exist
   aren't fabricated, optional edges that exist are named.
3. The taxonomy is composable, not a forced root-cause enum. Categories describe
   different semantic dimensions and legitimate co-occurrence is not ambiguity.
4. Five missing candidates are confirmed missing with appropriate evidence gates.
5. FI-05 through FI-08 admission criteria are defined and gated on fixture/oracle
   construction, not on model correction.

The seven issues identified are refinements, not refutations. None requires
reopening the extraction or changing the `HARNESS_ARTIFACT_ONLY` ownership
decision.

## 9. Explicit non-actions (reaffirmed)

- No production or test code changes.
- No classifier, engine, Provider, registry, Brain, Planner, Graph, or FSM.
- No Runtime `FailureEpisode` type, enum, field, interface, or mutable state.
- No retry, Recovery, dispatch, planning, or completion authority.
- No ownership, authority, dependency, or architecture-invariant change.
- No Reality Model corpus admission and no new canonical pressure.
- No implementation of FI-05 through FI-08 fixtures — criteria are defined;
  construction is a separate authorized task.

## 10. Recommended next task

```text
PROJECT_LEADER_FAILURE_EPISODE_RESOLVE_VALIDATION_FINDINGS
```

Resolve the seven identified issues (I1–I7) in the Reality Model Extraction
document. No implementation. Then, if the model is stabilized:

```text
PROJECT_LEADER_FAILURE_EPISODE_FI_05_08_FIXTURE_DESIGN
```

Design (not implement) the FI-05 through FI-08 fixtures using the admission
criteria defined in §5 of this validation.

`RUNTIME_FAILURE_EPISODE_INDEPENDENT_VALIDATION_RESULT`

STOP.
