# Core Model Responsibility Matrix

> Status: `CANDIDATE / REVIEWED`
> Authority: `NONE`
> Scope: CORE-004 responsibility alignment only

This matrix does not authorize source migration or freeze final inheritance.

## Purpose

This matrix separates Core semantic responsibility from the current Kernel, Agent,
Runtime, Perception, Assurance, Trace, Harness, and UI realization models. It is a
decision aid for later changes, not a one-to-one class mapping.

## Rules used

- A shared semantic duty may be promoted to Core; realization detail stays below Core.
- After promotion, duplicate lower-layer fields, writers, and validators may be removed.
- A mixed-responsibility model is split or composed; it is not forced to inherit from the
  Core type with the most similar fields.
- Inheritance requires true semantic specialization and preservation of history, evidence,
  uncertainty, and permission boundaries.
- Projection, cache, index, snapshot, trace, and Runtime state may exist, but they cannot
  become a second mutable fact authority.
- A missing guarantee blocks only the use that depends on it.

## Candidate matrix

| Current responsibility cluster | Core semantic candidate | Keep below Core | Relation choice | Evidence / gate |
|---|---|---|---|---|
| Evidence records, provenance, admission input | Evidence | Admission checks, ingestion policy, provenance implementation | Split responsibility; Core references semantic values | Core and Evidence-to-Belief tests; source/time/processing/context must remain recoverable |
| World claims, claim evolution, revision history | Claim; Event view where applicable | Reconciliation algorithm, revision store, conflict indexing | Compose/reference; no second claim authority | Claim evolution tests; latest must not rewrite historical basis |
| Containers, logical items, occurrence facts, scoped observations | Segment / Slice | UI occurrence identity, perception payload, coverage calculation | Split mixed UI model into World references plus realization representation | UI continuity/grounding tests; Segment does not prove real identity |
| Slice evidence basis and local coverage | Slice under Segment | Reference frame, units, viewport, calibration, fusion method | Compose with Evidence and domain contract | Overlap/history tests; missing reference-frame guarantee blocks spatial use |
| Candidate/canonical target selection | TargetBinding | Grounding search, candidate ranking, locator resolution | Split; Core keeps binding meaning, realization keeps selection mechanism | Binding seam tests; stale/ambiguous/unauthorized fail closed |
| Logical control operation | Effect | Control policy, tactical hypothesis, driver selection | Compose/reference | Control-to-effect tests; observation does not create permission |
| Dispatch request, receipt, retry/compensation attempt | Attempt under Effect | Transport, driver, persistence, crash recovery, dispatch log | Split; one Core attempt meaning, Runtime owns progress | Effect/Outcome tests; request snapshot, executor, authorization, and three execution dimensions remain a gate |
| Execution contract and explicit requirements | Clause candidate plus runtime contract view | Admission, lifecycle, activation, scheduling | Split/compose; do not equate whole contract with Clause | Contract tests; authority, scope, validity, and revision must be explicit |
| Goal and evaluation criteria | Clause/Expectation input where semantic | Goal authoring, evaluation algorithm, product policy | Reference; no Goal-as-Core fact shortcut | Agent evaluation tests; pending criteria do not become results |
| Assurance judgments and outcome proof | Claim/verification references | Assurance policy, freshness evaluation, proof aggregation | Reference/derive read-only | Assurance and terminal tests; judgment is not reality |
| Run model, obligations, terminal state, runtime outcome | No new Core object by default | Run lifecycle, commitments, closure, runtime coordination | Keep below Core; reference Core records | Runtime/Simulation tests; missing duties block closure only |
| Perception strategies, artifacts, OCR/vision/device acquisition | Evidence input | Recognition algorithm, cache, device transport, calibration | Keep below Core; feed Evidence through contract | Perception tests; omission is not absence |
| Trace spans, diagnostics, replay artifacts | Evidence/Effect references only | Trace lifecycle, diagnostics, replay transport | Read-only reference/projection | Trace tests; trace cannot become World truth |
| Browser, ADB, robot SDK, Harness session types | No direct Core type | Device/session/host semantics | Keep in realization; use opaque domain values at seam | Architecture boundary tests; Core must not reference concrete types |

## Decisions held

### Keep

- The Core object set and `Effect → Attempt → TargetBinding` hierarchy.
- Segment continuity separate from Slice observation scope.
- Evidence as provenance-bearing input and Claim as judgment.
- Event as Evidence + Claim composition until an expression/evolution counterexample exists.
- Runtime, Perception, Assurance, Trace, Harness, and device responsibilities below Core.

### Pending gates

1. **Basis reference gate** — CORE-006 defines the Core candidate `BasisReference` shape for
   Evidence, Claim, Slice, or fixed resource-version support without hiding it in an opaque
   locator string. The production projection for non-Slice bases is still a local gate;
   history-sensitive uses depending on that projection remain blocked.
2. **Attempt execution gate** — CORE-006 defines the Core candidate positions for request
   snapshot, executor, authorization, dispatch progress, external execution, and coordination.
   Runtime persistence, crash recovery, retry, late-result handling, and exactly-once claims
   remain lower-layer gates; they are not implied by the Core record.
3. **Slice context gate** — define domain contracts for reference frame, units, coverage,
   and context where a use needs them. Do not add generic freshness/trust/confidence fields.
4. **Projection gate** — update the realization seam only after validating the candidate
   BasisReference against a non-UI and a UI case; it must support non-Slice fixed bases without
   creating a second projection authority. CORE-006 deliberately leaves this gate open.

## Prohibited shortcuts

- Do not preserve every old class or create empty subclasses, wrappers, or Adapters only to
  satisfy a shape comparison.
- Do not infer inheritance from shared fields.
- Do not let a projection write back to World, Evidence, Binding, or Outcome authority.
- Do not treat a passing full suite as proof that every mapping is semantically correct.
- Do not delete an old writer until the replacement owner, readers, history, and rollback
  evidence are explicit.

## Next use

Each row becomes an implementation or review unit only after its pending gate is resolved
and a scenario-backed semantic test exists. This matrix itself does not migrate code.
