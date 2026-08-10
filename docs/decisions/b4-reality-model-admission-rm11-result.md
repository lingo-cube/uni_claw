# B4_REALITY_MODEL_ADMISSION_RM11_RESULT

> Generated: 2026-08-10
> Development lane: `SEMANTIC_DISCOVERY`
> Role: Reality Governance Architect + Dedup Arbiter
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Candidate: RM-11 — Intent / Execution Representation Separation
> Primary CP: CP-14 — Task Intent Must Not Be Conflated With Execution Method
> Contract: `docs/system/reality-model-admission-contract.md` (frozen v1.0)
> Inputs: CP-14 extraction result, independent-validation result, FS-CP14-001..004, accepted RM-01..RM-10 corpus, TE/U1/legacy evidence

## Condition Resolution

| Condition | Resolution | Status |
|---|---|---|
| `C-RM11-01` Authority reconciliation | The extraction artifact now cites the adopted Human receipt in `docs/decisions/reality-model-admission-contract-gate.md`; its stale awaiting-adoption statement was removed. | **RESOLVED** |
| `C-RM11-02` Provenance normalization | DIRECT WFs cite their OB records and explicit grades; WF-06 cites RI-02; WF-07 cites RI-06; every RI now records an inference method. E0/E1/E2/E3 grades are unchanged. | **RESOLVED** |
| `C-RM11-03` WF-05 repair | WF-05 now contains only observed WiFi ON/OFF task-start facts. Dispatches are recorded in OB-SYS-CP14-01. Zero/non-zero concrete-work meaning lives in RI-CP14-04. | **RESOLVED** |
| `C-RM11-04` Minimality repair | WF-03 was removed from the core WF set and its evidence retained as OB-05. ER-03 now states only scope/inventory separation; constraint enforcement remains RM-06/CP-07. ER-08 remains DERIVED/CROSS_CUTTING. | **RESOLVED** |
| `C-RM11-05` State-pressure correction | Normative classification is `NO_STATE_PRESSURE`. The rejected candidate is non-normative research history and authorizes no state/FSM work. | **RESOLVED** |
| `C-RM11-06` Ambiguity evidence maturity | RI-06 and WF-07 remain MEDIUM confidence. E-15 remains E1 and E-18 remains E0. Independent executable ambiguity evidence remains an evidence-maturity upgrade only. | **DEFERRED_EVIDENCE_ONLY — NOT AN ADMISSION BLOCKER** |

All admission-blocking conditions are resolved. The deferred evidence item does
not weaken the core closed/open representation distinction and may not be used
to promote RI-06 or WF-07 above MEDIUM without new evidence.

## Admitted Reality Model

| Field | Final value |
|---|---|
| RM-ID | `RM-11` |
| Title | Intent / Execution Representation Separation |
| Type | MODEL |
| Primary CP | CP-14 |
| Secondary CPs | CP-03, CP-04, CP-06, CP-07, CP-09 — supporting/cross-cutting only |
| World Facts | 6: WF-CP14-01, WF-CP14-02, WF-CP14-04..07 |
| Observation Records | 9: OB-CP14-01..08 + OB-SYS-CP14-01 |
| Reality Inferences | 7: RI-CP14-01..07 |
| Expected Requirements | 8: ER-CP14-01..08 |
| Evidence strength | E3 strongest; E1 core minimum; E0 supporting ambiguity provenance only |
| Confidence | HIGH for the core representation distinction; MEDIUM for ambiguity extraction risk |
| Validation | PASS after condition resolution |
| Admission outcome | **ACCEPT_NEW_MODEL** |
| Admission date | 2026-08-10 |

### Final World Fact Set

| ID | Support | Canonical admitted statement | Support chain |
|---|---|---|---|
| WF-CP14-01 | DIRECT | A task can declare categories, boundaries, safety constraints, and completion without enumerating every concrete page, element, coordinate, or route. | OB-01/03/04; TE-01 E1, TE-03 E1, TE-08 E3, E-05 E1 |
| WF-CP14-02 | DIRECT | Fresh observations can reveal concrete work absent from the pre-execution representation. | OB-01/04/07; TE-01 E1, TE-04 E1, TE-08 E3 |
| WF-CP14-04 | DIRECT | A closed-world task can explicitly contain concrete actions, targets, and a prescribed route. | OB-02; TE-02 E2, E-16 E1 |
| WF-CP14-05 | DIRECT | U1 task-start observations include one WiFi-ON world and one WiFi-OFF world. | OB-06; U1 E1 |
| WF-CP14-06 | INFERRED | Declared scope bounds legitimate discovered work but does not enumerate the complete observation-populated inventory. | RI-02 HIGH; OB-01/04/07; TE-04 E1, TE-05 E1, TE-08 E3 |
| WF-CP14-07 | INFERRED | Vocabulary-valid or structurally complete intent data can still be semantically wrong or incomplete. | RI-06 MEDIUM; OB-08; E-15 E1, E-18 E0 |

WF-CP14-03 is not admitted. OB-CP14-05 remains supporting evidence only.

## G1–G7 Validation

| Gate | Result | Admission finding |
|---|---|---|
| G1 Provenance | **PASS** | Every admitted WF terminates in OB and graded evidence. INFERRED facts cite RIs. E-18 remains E0 and TE-08 remains E3. |
| G2 Architecture Neutrality | **PASS** | Normative content requires no Compiler, Planner, Task IR, FSM, Graph, LLM/VLM, provider, or Runtime component. Legacy names remain provenance context only. |
| G3 Fact / Inference Separation | **PASS** | User/task declaration, current-world observation, execution representation, system dispatch record, inferred concrete-work meaning, and completion requirement remain separate layers. No GoalSatisfied verdict remains in a WF. |
| G4 Minimality | **PASS** | WF-03 removed. The remaining six WFs are each required by the open-world, closed-world, world-variation, scope/inventory, or ambiguity falsifier. ER-03 is narrowed; ER-08 remains derived. |
| G5 Deduplication | **PASS** | RM-11 is the only model whose primary oracle is Intent/Execution-Representation separation. RM-02/03/06/10 remain distinct owners of their existing pressures. |
| G6 Counterfactual | **PASS** | FS-CP14-001..004 state observable refuters for world variation, open-world discovery, explicit concrete method, and missing authority/desired state. |
| G7 Expected Requirement Adequacy | **PASS** | ER-01..07 collectively prevent CP-14's fail oracles without prescribing an implementation; ER-08 cross-references existing completion authority. |

## Deduplication and Novelty Decision

| Existing model / pressure | Preserved owner | RM-11 boundary |
|---|---|---|
| RM-03 / CP-06 | Goal satisfaction and zero unnecessary mutation | Whether intent entails a concrete execution representation |
| RM-02 / CP-04 | Progress and honest completion over discovered work | Whether concrete work must be pre-enumerated |
| RM-06 / CP-07 | Enforcement of declared constraints | Semantic distinction between declared scope and observed inventory |
| RM-10 / CP-12 | Grounding a concrete target among candidates | Transformation boundary before local target grounding begins |
| TRD-01 / TRD-02 evidence | Type-level route and scope/inventory distinctions | Standalone CP-14 model combining open and closed task classes |

Novelty test:

```text
Same world-fact cluster as accepted RM-01..RM-10: NO
Same primary CP/oracle as accepted RM-01..RM-10: NO
CP-14 coverage without RM-11: NO
Novelty: PASS
AdmissionOutcome: ACCEPT_NEW_MODEL
```

## Mode and Ambiguity Boundaries

### ClosedWorldMode

`PRESERVED`.

An explicit concrete method may be part of the task boundary. It remains
subject to world correspondence and effect evidence; route execution is not
Goal completion evidence by itself.

### OpenWorldMode

`PRESERVED`.

Scope, categories, safety, depth, and completion can be declared while concrete
pages, elements, coordinates, route, and work inventory remain observation-
populated. Open-world input must not be rejected merely because it lacks a
pre-enumerated route.

### Ambiguous Intent

Missing target, desired state, scope, permission, or authority must not be
silently created. RM-11 does not select clarification, rejection, deferral, UI,
interaction, model, or provider behavior. RI-06/WF-07 remain MEDIUM and their
evidence upgrade is deferred.

## State Pressure Decision

```text
StateMachinePressure: NO_STATE_PRESSURE
```

Changing observations, discovered work, dispatch records, and completion
evidence do not prove a missing lifecycle state or transition. RM-11 creates no
FSM, State Machine, state-semantic task, or architecture pressure.

## Corpus Reconciliation

| Metric | Before RM-11 | After RM-11 |
|---|---:|---:|
| Accepted models | 10 | **11** |
| World Facts | 33 | **39** |
| Observation Records | 26 | **35** |
| Reality Inferences | 23 | **30** |
| Expected Requirements | 28 | **36** |
| Canonical CP coverage | 13 / 14 | **14 / 14** |

### CP Coverage

| CP | Result after admission |
|---|---|
| CP-01..CP-13 | Existing RM-01..RM-10 and cross-cutting coverage unchanged |
| CP-14 | **COVERED — RM-11 ACCEPTED** |

`14 / 14` means every canonical pressure now has admitted model coverage or its
already-recorded cross-cutting coverage. It does not authorize implementation,
declare usability completion, or create a capability candidate.

## Deferred Evidence Register Addition

| ID | RM | Element | Current strength | Upgrade path | Admission impact |
|---|---|---|---|---|---|
| DEF-RM11-01 | RM-11 | RI-06 / WF-07 ambiguous-intent semantic error | E1 (E-15) + E0 (E-18) / MEDIUM | Independent executable ambiguous-intent falsifier | None; evidence-maturity upgrade only |

## Final Result

```text
B4_REALITY_MODEL_ADMISSION_RM11_RESULT

RM-ID: RM-11
Title: Intent / Execution Representation Separation
PrimaryCP: CP-14
Validation: PASS
AdmissionOutcome: ACCEPT_NEW_MODEL
CorpusCount: 11
CPCoverage: 14/14
ClosedWorldMode: PRESERVED
OpenWorldMode: PRESERVED
StateMachinePressure: NO_STATE_PRESSURE
ArchitectureImpact: NONE
RuntimeChanges: NONE
```

## Explicit Non-Actions

- No Compiler design
- No Planner design
- No FSM design
- No Task IR class
- No Runtime change
- No capability candidate generation
- No architecture change
- No new CP

## Recommended Next Task

`CP14_CAPABILITY_SEMANTIC_GATE_PREPARATION`

STOP.
