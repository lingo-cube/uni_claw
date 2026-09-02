# Stage B2 Accepted Evidence Model — Partial Result

> Change: `container-runtime-v2-evidence-model`
> Stage: `B2`
> Result: `PARTIAL — TASKS 2.1..2.5 COMPLETE; SPEC_BLOCKER AT 2.6/2.8`
> Authority: `NONE`（implementation evidence only）

## Completed scope

- `ContainerSlice` now carries the accepted Observation reference, viewport bounds,
  SpatialRegion refs, Occurrence refs, FastAssessment refs, and StabilityEvidence ref.
- `Occurrence` is accepted visual evidence only. Structured evidence may corroborate
  state hints through deterministic IoU + text correspondence; unmatched structured
  evidence remains auxiliary and cannot mint an Occurrence.
- `SliceAcceptancePolicy`, `SourceCorrespondence`, and `OccurrenceMaterializer` are
  stateless pure functions behind `RuntimeAcceptance`.
- `ContainerRuntimeV2Reducer.PrepareAcceptedEvidence` validates all references before
  one immutable state replacement. Stale/dangling candidates return the exact prior
  state reference.
- Acceptance rejection/degradation is emitted through `RuntimeObservability`; no
  Runtime diagnostic domain entity was introduced.
- `FastAssessment` is retained as lowest-tier hint evidence only.
- `ObservedElement.StabilizerHint` is the new input name. `StableKey` remains a
  compatibility shadow alias over the same backing value and does not create a
  second field or identity owner.

## Focused evidence

- New B2 scenarios: 10/10 passed.
- B1 + B2 + Model immutability + R8 reducer/live-state/architecture guards: 111/111 passed.
- Build: passed with zero errors (pre-existing warning set remains).
- Full solution test is not green: Semantic 153 passed / 5 failed; Runtime 2658 passed /
  6 failed. All failures are outside the B2 touched slice; ten match the existing failure
  categories recorded by B1, while the additional VisionHost failure is an environment /
  process-start symptom and is not evidence of B2 readiness or regression attribution.

## Authority and ownership proof

- `FAST_RESULT != ACCEPTED_RUNTIME_EVIDENCE` is represented by the explicit acceptance
  input/result boundary.
- `Occurrence` has no action, obligation, progress, graph, coverage, completion, or
  cross-run identity field.
- Structured-only evidence is stored only as `UnmatchedStructuredEvidence` and has no
  grounding or occurrence reference.
- Accepted Slice/Occurrence/FastAssessment evidence uses the existing
  `ContainerRuntimeV2State` immutable replacement seam and existing
  `SemanticEvidenceRevision`; no second reducer or semantic clock was added.
- `ObservedElement.StabilizerHint` and the legacy `StableKey` alias share one backing
  value. This is compatibility shadowing, not dual-write truth.

## SPEC_BLOCKER / Human Gate

Task 2.6 requires a divergence rate between the legacy StableKey signature path and
`LocalModel correlation`. The latter does not exist yet: tasks 3.1 and 3.3 create
LocalModel and its canonical reconciler. Inventing a B2 correlation decision would
prematurely implement C1 and create identity authority outside the approved stage.

Task 2.8 requires the seven `Container.cs` page-local state fields to MOVE/DERIVE into
LocalModel, LogicalItem state, region coverage, and three-condition closure. Those
destination owners are scheduled in tasks 3.1–3.3 and 4.4–4.5. Removing the fields now
would delete purchased behavior; creating the destination owners now would reorder the
approved owner migration.

Task 2.7 has a pure Primitive taxonomy mapper from the accepted-evidence work, but its
shadow divergence threshold, kill criteria, and convergence evidence are not frozen or
measured. It remains incomplete.

Human disposition is required before further Apply:

1. move 2.6 and 2.8 after their C1/C2 producer tasks while keeping B2 as the evidence
   foundation; or
2. explicitly authorize LocalModel/correlation/coverage owner creation inside B2 and
   update the stage/task dependency model before implementation.

No production authority cutover has occurred. The legacy paths remain formal until a
future shadow measurement satisfies an explicitly approved threshold and kill criteria.
