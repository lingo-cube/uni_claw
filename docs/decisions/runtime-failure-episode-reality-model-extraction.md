# Runtime Failure Episode Reality Model Extraction

> Date: 2026-08-12
> Role: Project Leader / Reality Model Author
> Lane: `SEMANTIC_DISCOVERY`
> Input: `docs/decisions/runtime-failure-episode-evidence-research.md`
> Result: `RUNTIME_FAILURE_EPISODE_REALITY_MODEL_RESULT`
> FailureEpisode ownership: `HARNESS_ARTIFACT_ONLY`
> Admission: **NOT_ADMITTED**
> Implementation authority: **NOT GRANTED**

## 1. Canonical result

```text
FAILURE_EPISODE
  = HARNESS_ARTIFACT_ONLY

EVIDENCE_SHAPE
  = PARTIAL_CORRELATION_GRAPH

TAXONOMY_SHAPE
  = COMPOSABLE_BOUNDARIES
  != FLAT_ROOT_CAUSE_ENUM

RUNTIME_DELTA
  = NONE

OWNERSHIP_DELTA
  = NONE

AUTHORITY_DELTA
  = NONE
```

This extraction confirms that one diagnostic episode can be reconstructed from
existing immutable receipts without becoming a Runtime semantic model. The
artifact records a bounded evidence window and explicit links. It does not
assert that every included fact is a failure, that correlation is causation, or
that one label is a root cause.

The name `FailureEpisode` is therefore an analysis/catalog name. It may contain
an expected safe refusal, insufficient input, or independently failed Harness
operation. Inclusion in an episode is not itself a negative Runtime verdict.

This is an extraction candidate, not admission into the established RM corpus.
The current Reality Model Admission Contract requires one registered Primary
Canonical Pressure, and no new CP or admission authority is purchased here.
World observations and system receipts remain separated below so Runtime
verdicts are not silently promoted to external-world facts.

## 2. Evidence source map

| Source | Direct evidence available | Provenance / strength | Supported boundaries | Limitations |
|---|---|---|---|---|
| `SimulationEnvironment` | ordered Observations, action history, injected `Rejected`, `TimedOut`, dispatched/no-effect, index drift, binding disappearance, permanent non-convergence | `SYNTHETIC`, E1 deterministic simulation | UNKNOWN, binding loss, dispatch rejection/uncertainty, observed no effect, verification failure, bounded non-convergence | Programmed world; never promote to recorded reality or causal truth |
| Observation Replay | versioned Frames and Observations, expected actions, recorded `ActionResult`, action/observation histories, fail-closed divergence/exhaustion | REALITY_SEEDED E3 where derived from recorded evidence; otherwise declared asset provenance | rejected response propagation, UNKNOWN, fresh verification, deterministic replay, Harness conformance mismatch | Perception skipped; current failure replay does not persist one complete FailureEpisode |
| Reality-seeded capture corpus | explicit CaptureSession, Frame sequence, Frame relations, Observation artifacts, trace events and GoalEvidence references | REALITY_SEEDED, E3 executable reproduction | OFF/ON/UNKNOWN observation boundaries and action/effect separation | OFF-to-ON transition is explicitly synthetic; not direct captured failure evidence |
| Golden runs | recorded emulator screenshots/perception inputs plus already-ON, OFF-to-ON, and UNKNOWN replay paths; ad-hoc typed trace JSON | raw assets RECORDED_REALITY E4; executable reconstruction E3 | observation facts, zero mutation, action/fresh-effect closure, UNKNOWN | no recorded rejection, timeout, Recovery failure, or Harness failure golden |
| Runtime observability | immutable `TraceRun`, hierarchical `TraceSpan`, structured attributes/events, explicit operational outcomes | production-shaped trace foundation; E1 when driven by synthetic simulation | operation presence, layer/component propagation, failed/cancelled/unknown operational boundaries | span outcome is not semantic outcome; no direct Frame edge in `TraceRun` |
| Trace capture/persistence | ordered Observation/dispatch/result records, immutable capture bundle, optional TraceRun, separate Runtime outcome, append-only store result | Harness production-shaped assets; current executable store path is success-only | capture/run separation, successful persistence, partial trace preservation shape | no injected listener/store failure end-to-end fixture; `CaptureFault` schema exists but lacks executable producer proof |
| Formal Scenario corpus | SC-P3-001 timeout branches, SC-P2-003 Recovery verification failure, bounded candidate safety/refusal, Intent insufficiency, startup verification failure | mostly E1 deterministic scenarios; exact provenance remains per asset | semantic boundary oracles and deterministic replay | fixtures are distributed; no canonical cross-stream FailureEpisode artifact |
| Legacy failure pressures | rejected dispatch, timeout, stuck world, drift, failure-not-success distinctions recorded in graduation/closeout decisions | E0 when document-only; E1/E3 only where migrated into executable assets | historical pressure and falsifier provenance | legacy interpretation, old mechanism names, and expected behavior are not World Facts |

### 2.1 Evidence maturity freeze

- A passing simulation remains E1.
- A replay reconstructed from recorded reality remains E3 / REALITY_SEEDED when
  fields or transitions were normalized or synthesized.
- Raw recorded emulator/device evidence remains E4 only for fields it directly
  records.
- Document-only legacy pressure remains E0 unless an executable asset is cited
  separately.
- Trace timing, free-form diagnostics, filenames, and private method order do
  not upgrade evidence strength or establish correlation.

## 3. Extracted observations and inferences

### 3.1 Direct world and external-boundary observations

| ID | Record | Support |
|---|---|---|
| `OB-FE-01` | A switch observation can expose ON, OFF, or missing/UNKNOWN state evidence. | Golden/reality replay plus deterministic simulation |
| `OB-FE-02` | An external dispatch receipt can be `Dispatched`, `TimedOut`, or `Rejected`; the receipt contains no world snapshot. | `ActionResult` contract and executable simulation/replay |
| `OB-FE-03` | A fresh post-dispatch Observation may show the expected world transition or no transition. | SC-P3-001, stateful simulation, reality replay |
| `OB-FE-04` | A dispatched action can coexist with a fresh world-unchanged Observation. | Simulation H4/C1 and Intent G9 |
| `OB-FE-05` | A binding surface can be present at one observation and absent/drifted at a later observation. | Simulation H6/H7 and binding-loss scenarios |

### 3.2 System and Harness action/outcome records

These are `OB-SYS` records, not external-world facts:

| ID | Record | Support |
|---|---|---|
| `OB-SYS-FE-01` | Insufficient Intent can terminate projection before a Runtime run and expose no executable Goal. | Intent compilation P5/P13/P14/P15 |
| `OB-SYS-FE-02` | Safety or evidence insufficiency can result in zero dispatch. | bounded-candidate and UNKNOWN scenarios |
| `OB-SYS-FE-03` | Runtime preserves distinct Agent outcomes for state evidence required, binding unresolved, semantic contradiction, budget exhaustion, execution failure, and satisfaction. | `SemanticRunResult` and executable scenarios |
| `OB-SYS-FE-04` | Recovery verification failure retains a Recovery-linked trace and ends under Agent authority without resume. | SC-P2-003 |
| `OB-SYS-FE-05` | Replay action divergence and asset exhaustion fail closed at the Harness boundary. | `ObservationReplayTests` |
| `OB-SYS-FE-06` | A successful Runtime result can be captured and persisted with an independently represented persistence result. | `ObservabilityConformanceTests.GoldenRun_RecordsObservabilityTrace` |

### 3.3 Reality inferences

| ID | Inference | Confidence | Alternatives | Materiality | Method |
|---|---|---|---|---|---|
| `RI-FE-01` | `TimedOut` alone cannot identify action effect or root cause. | HIGH | delivered and effective; delivered but ineffective; not delivered; evidence stale/unavailable | HIGH | deduction across positive and negative post-timeout scenarios |
| `RI-FE-02` | One useful episode is a partial evidence graph, not a mandatory linear chain. | HIGH | a linear chain works for some fully captured action paths only | HIGH | deduction from pre-run insufficiency, zero-dispatch refusal, rejected action, and post-run store failure shapes |
| `RI-FE-03` | Failure labels describe different dimensions and can legitimately co-occur. | HIGH | forcing mutual exclusion loses dispatch/world/verification distinctions | HIGH | cross-scenario boundary comparison |
| `RI-FE-04` | The earliest failed span is not necessarily the semantic origin or root cause. | HIGH | span failure may be propagation, handling, cancellation, or listener/capture loss | HIGH | observability/semantic ownership comparison |
| `RI-FE-05` | Harness operational or conformance failure can be correlated with, but must remain independent from, Runtime outcome. | HIGH for contract separation; MEDIUM for failing-store execution | a future fixture could reveal missing correlation data without changing authority | HIGH | contract deduction plus success-only persistence evidence |
| `RI-FE-06` | Diagnostic text and exact timing are insufficient stable episode identity or classification input. | HIGH for contract; MEDIUM for perturbation behavior | a future structured field could provide an explicit stable link | HIGH | schema inspection and deterministic replay comparison |

## 4. Episode boundary

### 4.1 Start

An episode starts at the earliest directly recorded receipt that introduces the
bounded condition being analyzed:

- **pre-run:** authoritative Intent input plus `Insufficient` projection;
- **pre-dispatch Runtime:** the fresh observation, authorization receipt,
  binding receipt, contradiction, or startup verification receipt that prevents
  safe progress;
- **dispatch:** the exact action dispatch/result pair for rejection or timeout;
- **post-dispatch:** the first fresh observation and explicit expected-effect
  comparison for verification/no-effect analysis;
- **Recovery:** the antecedent Trap/failure receipt, not merely the later
  Recovery failure;
- **Harness:** the capture, projection, replay-conformance, listener, or store
  operation at which the independent Harness fault is recorded.

An earlier Scenario declaration supplies context; it is not automatically the
episode start. An unrelated earlier span or Frame is excluded merely because it
occurred first.

### 4.2 End

An episode ends at the earliest authoritative stabilization boundary relevant
to that condition:

- an insufficient/refusal result with confirmed zero dispatch;
- the required fresh observation plus verification/Agent adjudication after an
  uncertain dispatch;
- a Recovery verification result plus Agent resume/terminal adjudication;
- an Agent terminal semantic receipt when the Runtime chain terminates;
- a final Harness capture/projection/persistence/conformance result for an
  independent Harness episode.

If required evidence never arrives, the bounded terminal receipt ends the
window while the assessment remains `INSUFFICIENT`; absence is not replaced by
`world unchanged`, rejection, success, or failure. A successfully handled
antecedent failure may end at verified handling/resume even when the overall run
later succeeds.

### 4.3 Evidence window membership

One episode window contains only:

1. the start and end receipts;
2. stable typed references necessary to establish the selected boundary;
3. explicit predecessor/next, action/observation, Trap/Recovery, or
   propagation relations;
4. relevant Runtime, dispatch, Recovery, and Harness outcomes kept separate;
5. explicitly declared contradictory or missing evidence.

Shared evidence may be referenced by linked episodes without copying or
rewriting its authoritative content. Recovery failure is normally a linked
secondary boundary whose window includes the antecedent Trap. Harness
persistence failure is a separate window that may reference the completed run.

The window excludes unrelated prior activity, subsequent retries/recoveries,
diagnostic-only strings, inferred private call order, and exact-duration
coincidence.

## 5. Evidence graph

The requested relationship is valid only as a set of optional explicit edges:

```text
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

There is no truthful mandatory edge from `TraceRun` to `Frame` in the current
production model. `TraceRun` currently carries `TraceRunId`, optional `TraceId`
and optional `RunId`; Frames belong to Capture/Replay assets. Correlation may
use explicit Run, Trace, CaptureSession, Frame, Observation sequence, action,
and GoalEvidence IDs where present. Missing IDs stay absent.

Therefore the linear form:

```text
Scenario -> TraceRun -> Frame -> Observation -> Action -> Outcome -> FailureEpisode
```

is rejected as a required schema. It would fabricate edges for pre-run
insufficiency, zero-dispatch refusal, rejected action, partial capture, and
post-run persistence failure.

## 6. Taxonomy validation

### 6.1 Current categories

| Category | Validation | Exact boundary / issue |
|---|---|---|
| `NON_EXECUTABLE_INPUT` | VALID | Pre-run projection is insufficient; no executable Goal/run is implied. |
| `AUTHORIZED_REFUSAL` | VALID AS DISPOSITION | A safety/authority boundary intentionally prevents dispatch. It can co-occur with an evidence reason and is not an execution fault. |
| `EVIDENCE_INSUFFICIENT` | VALID AS EVIDENCE CONDITION | Evidence cannot support the required state/decision. It is not OFF, rejection, or observation transport failure. |
| `BINDING_UNRESOLVED` | VALID | Required interaction grounding is unresolved; does not identify the perception/root cause. |
| `VERIFICATION_NOT_SATISFIED` | VALID | An explicit expected-effect criterion is evaluated against fresh evidence and is not satisfied. |
| `DISPATCH_REJECTED` | VALID | Exact external dispatch receipt is Rejected; it does not prove world relation or root cause. |
| `DISPATCH_UNCERTAIN` | VALID | Exact dispatch receipt is TimedOut; uncertainty survives until fresh evidence narrows only the world-effect question. |
| `OBSERVED_NO_EFFECT` | VALID AS WORLD RELATION | Explicitly scoped pre/post observations show no expected change. It does not prove non-delivery or failed dispatch. |
| `RECOVERY_VERIFICATION_FAILED` | VALID | Recovery action/result plus fresh evidence fail the Recovery criterion; antecedent evidence remains linked. |
| `HARNESS_OPERATIONAL_FAILURE` | VALID AS FAMILY, TOO BROAD ALONE | Capture/listener/projection/store lifecycle failure is independent of Runtime. Sub-boundary must be retained. |

### 6.2 Overlap is compositional, not duplication

- `AUTHORIZED_REFUSAL` is a handling disposition; `EVIDENCE_INSUFFICIENT` or
  an authorization receipt states why dispatch was withheld.
- `DISPATCH_UNCERTAIN` and `OBSERVED_NO_EFFECT` may both be supported in one
  episode; neither selects a transport or target root cause.
- `OBSERVED_NO_EFFECT` becomes `VERIFICATION_NOT_SATISFIED` only when an
  explicit expected-effect criterion and fresh evidence exist.
- `BINDING_UNRESOLVED` and `EVIDENCE_INSUFFICIENT` remain distinct because
  known state with unresolved target binding and unknown state with known
  binding are separately reproducible.
- `RECOVERY_VERIFICATION_FAILED` is linked to, and never replaces, its
  antecedent Trap/failure boundary.
- `HARNESS_OPERATIONAL_FAILURE` may coexist with any Runtime result and must
  never overwrite it.

This proves the categories cannot honestly be one mutually exclusive
root-cause enum. A future assessment must compose boundary + evidence stance +
handling/terminal receipt.

### 6.3 Missing or under-specified boundaries

| Candidate boundary | Evidence status | Decision in this extraction |
|---|---|---|
| `SEMANTIC_CONTRADICTION` | Executable page/evidence contradiction exists | MISSING from current taxonomy; retain as a distinct semantic adjudication boundary, not an external fault cause |
| `BOUNDED_NON_CONVERGENCE` | Executable stuck-world/budget exhaustion exists | MISSING; distinguish repeated verified non-satisfaction from one `OBSERVED_NO_EFFECT` event |
| `CANCELLED` | Trace outcome vocabulary and cancellation-aware ports exist; no accepted end-to-end episode | MISSING but EVIDENCE-GATED; do not normalize into failure |
| `OBSERVATION_UNAVAILABLE` | Architecture/scenario pressure exists; no minimized accepted episode fixture | MISSING but EVIDENCE-GATED; must remain distinct from an Observation containing UNKNOWN evidence |
| `HARNESS_CONFORMANCE_FAILURE` | Replay divergence, asset exhaustion, invalid span graph/outcome are executable | MISSING sub-boundary; distinguish behavior/evidence mismatch from store/listener operational failure |
| startup precondition/foreground verification | Executable SC-P1-002 exists | No new top-level category required yet; represent as `VERIFICATION_NOT_SATISFIED` with startup scope unless independent validation falsifies this fit |

No enum/type purchase follows from this list. Independent validation must first
test minimality and deduplication.

## 7. FI-01 through FI-08 reality mapping

| FI | Available evidence | Missing asset | Required bounded replay/fixture |
|---|---|---|---|
| FI-01 rejection propagation | Simulation L1 and Observation Replay rejection execute `Rejected -> Traversal cannot advance -> Agent ExecutionFailed`; action/observation histories exist | no persisted single episode correlating Scenario, run, TraceRun, action result, and Agent outcome | Harness-only rejection replay with stable IDs and explicit origin/propagation/terminal references; vary diagnostic text |
| FI-02 insufficiency/refusal separation | Intent P5/P13/P14/P15; bounded candidate refusal; UNKNOWN semantic/reality/golden replays; all prove no fabricated dispatch | distributed fixtures and no one three-variant episode catalog | three minimal variants: pre-run insufficiency with no RunId; authorized denial with zero dispatch; UNKNOWN observation with `StateEvidenceRequired` and zero dispatch |
| FI-03 timeout uncertainty | SC-P3-001 positive/negative deterministic scenarios; Simulation H5/L3; fresh observation and action count proofs | no versioned persisted timeout replay containing both world-changed and world-unchanged variants | two replay manifests sharing the same timeout dispatch fact but different fresh post-action observations; assert no success/failure from timeout alone |
| FI-04 Recovery verification failure | SC-P2-003 proves Trap, Recovery ID, action, fresh observation, failed verification, no resume, deterministic trace | no Capture/TraceRun episode graph with the antecedent link | minimize SC-P2-003 into a Harness replay/capture asset retaining Trap -> Recovery -> fresh evidence -> Agent terminal references |
| FI-05 Harness failure isolation | successful Runtime + TraceRun + capture bundle + append-only persistence path; separate result types exist | no injected failing `ITraceCaptureStore`, listener, projector, or capture-fault end-to-end scenario | test-only failing store plus listener/projection variant; pair with Runtime success and Runtime failure; assert outcomes remain independent and partial trace is honest |
| FI-06 cancellation | cancellation-aware APIs and `CANCELLED` observability outcome vocabulary | no deterministic caller-cancelled run/capture episode or terminal oracle | controlled cancellation at a named public boundary with preserved prior facts, child/parent span closure, zero invented world relation, and independent Runtime outcome if emitted |
| FI-07 multiple plausible causes | timeout + fresh unchanged world supplies facts compatible with non-delivery and ineffective delivery | no assessment oracle preserving alternatives/`INSUFFICIENT` | replay one identical structured episode against at least two plausible hypotheses; require no ranking, numeric confidence, retry, or root-cause selection |
| FI-08 structural determinism | SC-P3-001, SC-P2-003, Observation Replay, bounded safety and golden replays prove equal-input deterministic structure | no fixture perturbing timing, generated IDs, and diagnostic strings while holding typed facts equivalent | paired Harness-only manifests with different timing/message/generated IDs and stable semantic correlation keys; require equivalent boundary classifications |

### 7.1 Executability summary

- FI-01 through FI-04 have executable semantic/source evidence but still lack a
  canonical persisted FailureEpisode correlation asset.
- FI-05 is success-path only; the failure-isolation oracle is absent.
- FI-06 is an evidence gap.
- FI-07 has pressure but no alternative-preservation oracle.
- FI-08 has deterministic baselines but no structured-equivalence perturbation
  fixture.

Passing present tests does not close these asset gaps.

## 8. Candidate expected requirements

These requirements are extraction targets for independent validation, not an
implementation or classifier contract:

1. An episode SHALL correlate immutable existing receipts by explicit stable
   references and SHALL preserve missing links as missing.
2. Direct facts, system receipts, correlation, classification, and hypotheses
   SHALL remain distinguishable.
3. Runtime semantic, dispatch, Recovery, observability, and Harness outcomes
   SHALL remain separate.
4. Taxonomy labels SHALL be composable boundaries, not one forced root-cause
   verdict.
5. Timeout SHALL remain dispatch uncertainty; fresh observation SHALL decide
   only supported world-effect claims.
6. Safe refusal and evidence insufficiency SHALL NOT be classified as attempted
   action failure.
7. Harness operational/conformance failure SHALL NOT rewrite Runtime outcome.
8. Missing or contradictory evidence SHALL yield `AMBIGUOUS/INSUFFICIENT`, not
   invented causation, authority, or desired action.
9. Exact duration, generated IDs, filenames, diagnostic strings, and private
   method order SHALL NOT define behavioral equivalence.
10. A FailureEpisode or assessment SHALL NOT authorize dispatch, retry,
    Recovery, replanning, GoalEvidence, or completion.

## 9. Counterfactuals

This extraction would be falsified or narrowed by executable evidence that:

- a Runtime decision genuinely requires consuming the correlated episode
  during the same run;
- explicit existing IDs cannot correlate the minimum episode without changing
  a frozen Runtime semantic contract;
- a timeout deterministically establishes action success/failure without fresh
  world evidence;
- a Harness failure changes the independently recorded Runtime result;
- current categories must be mutually exclusive to reproduce an approved
  Scenario;
- diagnostic text, exact timing, or call order is the only stable evidence of a
  required distinction.

The first two findings would trigger architecture/semantic gates, not authorize
an inline Runtime model. A new fixture that only supplies a missing explicit
correlation edge would narrow the evidence gap without changing ownership.

## 10. Ownership and authority proof

```text
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

Harness may collect, correlate, minimize, persist, structurally compare, and
state supported/ambiguous/insufficient hypotheses. Harness may not dispatch,
retry, recover, select a route/target/capability, mutate evidence, change
GoalEvidence, change RunState, or alter any Runtime decision.

Agent authority, Container ownership, Traversal execution/verification
authority, Environment reporting, Recovery boundaries, and all graduated
semantic contracts remain unchanged.

## 11. Extraction status and next task

```text
EXTRACTION_STATUS
  = READY_FOR_INDEPENDENT_VALIDATION

FAILURE_EPISODE_OWNER
  = HARNESS

RUNTIME_MODEL
  = NOT_REQUIRED

RUNTIME_CHANGES
  = NONE
```

Recommended next task:

```text
PROJECT_LEADER_FAILURE_EPISODE_REALITY_MODEL_INDEPENDENT_VALIDATION
```

Independent validation must challenge provenance, the non-linear episode
boundary, taxonomy composability/minimality, the five candidate missing
boundaries, and FI-05 through FI-08 evidence gaps. It must not implement a
classifier or admit a new Runtime model.

## 12. Explicit non-actions

- No production or test code changes.
- No classifier, engine, Provider, registry, Brain, Planner, Graph, or FSM.
- No Runtime `FailureEpisode` type, enum, field, interface, or mutable state.
- No retry, Recovery, dispatch, planning, or completion authority.
- No ownership, authority, dependency, or architecture-invariant change.
- No Reality Model corpus admission and no new canonical pressure.

`FAILURE_EPISODE = HARNESS_ARTIFACT_ONLY`

STOP.
