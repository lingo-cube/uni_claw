# Perception Phase 3 / Phase 4 Semantic Correction — Targeted Re-audit Result

> Date: 2026-08-13
> Role: fresh independent Sol adversarial semantic-enforcement re-audit
> Input: `perception-phase3-phase4-semantic-repair-correction-implementation-result.md`
> Result: `PERCEPTION_PHASE3_PHASE4_SEMANTIC_CORRECTION_TARGETED_REAUDIT_RESULT`
> Status: **REPAIR_INCOMPLETE**
> Repair performed: NONE (audit only — REPAIR was FORBIDDEN)

---

## 0. Verdict

```text
Status: REPAIR_INCOMPLETE
S0Count: 0
S1Count: 4
RemainingS1: GAP-004, GAP-006, GAP-007, GAP-008
AuthoritativeBypassableCount: 4
CriticalUnprovenCount: 0
SemanticClosureDeclarable: NO
Phase3Phase4SemanticEnforcement: OPEN
ArchitectureReopenRequired: NO
RuntimeDelta: NONE | SemanticDelta: NONE | AuthorityDelta: NONE
```

The correction closed the **execution seams** but left the **record-minting
surfaces** public. The surviving pattern is identical across all four
remaining gaps:

> A public dataclass + a public save function = caller-mintable canonical
> authority. Type checks and field congruence do not prove that a record
> was produced by the canonical path.

Executable counterexamples were reproduced for each (probes below; no
production code was modified).

---

## 1. Gap-by-gap adversarial results

### GAP-002 — geometry — CLOSED, E4 / NOT_BYPASSABLE

Attacked the actual `_run_pipeline` response-production path (not the
validator): mocked fusion output carrying invalid `boundsPx`
(out-of-frame negative, NaN, beyond-frame) in `candidates`, `yolo`, and
`ocr`, plus invalid stage-view geometry.

```text
candidates: only the valid sibling survived (invalid [-200,…] dropped)
yolo: NaN boundsPx rejected → empty
ocr:  beyond-frame rejected → empty
status: INVALID_GEOMETRY (never OK_EMPTY)
stage views: raw/normalized pixel-contract enforced; fused = valid only
Second serialization path: none — /v1/analyze and /v1/analyze_raw both
converge on _run_pipeline.
```

Note on probe methodology: a first probe feeding *valid* `boundsPx` with an
*inconsistent* normalized `bounds` dict was "healed" by remap (normalized
values are derived data, recomputed from pixels) — that is remap's normal
semantics, not a bypass. The faithful attack (invalid pixels) is fully
blocked.

### GAP-004 — canonical quality provenance — **STILL BYPASSABLE, S1**

Counterexample reproduced:

```text
ProvenanceBoundScorecard is a PUBLIC dataclass. Minted directly with
invented taskSlices ({"aggregate": {"mean": 1.0, "n": 1000}}), ZERO
scoring_results, request_id="run:FORGED" →
BaselineReport.create accepted it and produced canonical baseline
baseline:ca6c725f8513095d82ea56050df1512 with the invented quality
payload inside qualityScorecard.
```

Caller-created provenance masquerades as canonical scoring evidence. The
dict boundary was closed; the object boundary was not — the type is
constructible and unverified against persisted EvaluationRunResult /
Prediction records.

### GAP-006 — training admission receipt — **STILL BYPASSABLE, S1**

Counterexample reproduced:

```text
TrainingAdmissionReceipt is a PUBLIC dataclass. Forged with matching
datasetVersionId + protectedSetId (never passed canonical admission) →
execute_training accepted it and produced a congruent execution session.
```

Execution compares caller fields; it never verifies the receipt against a
persisted canonical admission record. Arbitrary caller construction mints
training authority.

### GAP-007 — annotation acceptance authority — **STILL BYPASSABLE, S1**

Counterexample reproduced (full file-boundary attack):

```text
Built a MODEL_ASSISTED draft, forged an AnnotationAcceptanceEvent
(payload-hash + predecessor + reviewer chosen by the attacker), forged an
ACCEPTED Annotation binding that event — then persisted all three through
the PUBLIC save_annotation / save_acceptance_event functions and submitted
the dataset. admit_dataset_for_training ADMITTED the forged chain
(receipt admission:3af0fddd1743c504e6ecd5bd2d6a41…).
```

Valid JSON + non-empty fields + self-consistent hashes = review authority.
Nothing distinguishes a canonically-created event from a forged one; the
chain validator validates structure, not origin.

### GAP-008 — TrainingConfig → real invocation — **STILL BYPASSABLE, S1**

Counterexample reproduced:

```text
TrainingRun is a PUBLIC dataclass; save_training_run is public. Minted a
COMPLETED run with training_config_id="tcfg:UNRELATED", invocation_args
{epochs: 999, imgsz: 9999, seed: 999} → persisted to canonical history
(e16084913c4b5f1dd3c323af6f2c636f…).
```

The execution seam derives invocation from the config correctly (that part
is closed — caller-supplied ResolvedTrainingInvocation cannot drive
execution, and `model.train` is invoked only inside `execute_training`).
But canonical RUN RECORDS can be minted directly, bypassing the
execution-session derivation entirely.

### GAP-009 / P4-34 — canonical Host composition — CLOSED, E4

```text
A. STRUCTURAL: zero production `new VisionServiceHost` / `new
   VisionHostConfig` outside the Host assembly (sweep of all src/);
   InternalsVisibleTo grants only UniClaw.Runtime.Tests; reflection
   confirms no public verification-optional constructors;
   CanonicalVisionHostFactory is the sole public creation route.
B. BEHAVIORAL: real composition executed against the real Python server —
   factory → ACTIVE receipt → HEALTHY; restart re-verifies a fresh child;
   wrong model / wrong config / wrong pipeline → fail closed through the
   factory path; unsupported schema → fail closed at the earliest
   boundary; receipt mutation after construction does NOT switch the live
   Host (captured expectation retained).
P4_34: E4.
```

---

## 2. Regression replay (previously closed gaps)

```text
GAP-001: PASS — semantic [] preserved; operational classes distinguishable
         (Vision behavioral suite, fresh .NET run).
GAP-003: PASS — verified-bytes L2 + TOCTOU/path-replacement falsifiers green.
GAP-005: PASS — write-once: byte-identical idempotence, different-content
         collision refusal, legacy pretty-printed records accepted only on
         parsed semantic equality (never normalized).
GAP-010: PASS — no pre-created COMPLETED run; request/result immutable;
         infrastructure precedence truthful.
GAP-011: PASS — executable pipeline documentation unchanged by corrections.
GAP-012: PASS — no legacy launch path references (grep clean).
```

No regression found.

## 3. Public authority surface audit (six repaired domains)

| Domain | CANONICAL | NONCANONICAL_INSPECTION_ONLY | AMBIGUOUS_AUTHORITATIVE_PATH |
|---|---|---|---|
| Geometry | server serialization boundary | — | none |
| Quality | EvaluationScoringContext.score | compute_task_metrics | **ProvenanceBoundScorecard public construction → BaselineReport** |
| Admission | execute_training (seam) | — | **TrainingAdmissionReceipt public construction → accepted by seam** |
| Annotation | (none — origin unverified) | Annotation/from_json | **forged event + accepted record via public saves → admission** |
| Training | training_run_from_execution | TrainingRun construction | **TrainingRun public construction → save_training_run** |
| Host | CanonicalVisionHostFactory | — | none |

## 4. Root cause (single pattern)

The correction enforced the EXECUTION seams (no-receipt→no-execution,
config-derived invocation, chain validation) but the RECORD TYPES and their
SAVE FUNCTIONS remained public. Type-checking a public dataclass is not
enforcement — the same pattern the original audit found with raw dicts,
re-appearing one abstraction level up.

Closing requires (next correction round, not this audit):

1. **GAP-004**: canonical-only construction of ProvenanceBoundScorecard
   (internal constructor + builder), OR BaselineReport.create verifies
   scoring results against persisted EvaluationRunResult/Prediction
   records (loads and cross-checks run/asset/deployment bindings).
2. **GAP-006**: execute_training verifies the receipt against the
   PERSISTED canonical admission record (receipt id + content lookup),
   not caller fields.
3. **GAP-007**: admission verifies event/annotation records against the
   canonical creation path — either canonical-only save surfaces or a
   persisted-authority registry of event ids produced by
   accept_annotation.
4. **GAP-008**: canonical-only TrainingRun creation for terminal records
   (internal constructor + InternalsVisibleTo) so only
   training_run_from_execution can mint persisted runs.

All four fit the existing architecture — no new subsystem, no Runtime
delta, no authority expansion. No architecture reopen.

## 5. Fresh execution evidence

```text
Perception: 9/9 PASS
Evaluation: 101/101 PASS (incl. 16 semantic-enforcement falsifiers)
Training:   62/62 PASS
Governance (unit): 37/37 PASS
Model-Intelligence: 19/19 PASS
Vision Host behavioral + factory composition: real-server suite PASS
.NET full regression: 883/883 PASS (fresh, this session)
Real canonical mini training: PASS (process proof through the execution seam)
Real Host composition: PASS (E4)
DiffCheck: PASS

HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED
                     (not converted to PASS; no historical rewrite found)
```

## 6. Next task

```text
SOL_PERCEPTION_PHASE3_PHASE4_SEMANTIC_RECORD_MINTING_CORRECTION

Scope: exactly the four surviving S1 gaps — canonical-only record
construction/persistence for ProvenanceBoundScorecard (GAP-004),
TrainingAdmissionReceipt (GAP-006), AnnotationAcceptanceEvent +
accepted Annotation (GAP-007), and terminal TrainingRun (GAP-008).

Pattern: internal constructors + canonical builders +
InternalsVisibleTo(tests); where persisted-record verification is cheaper
than constructor restriction, load-and-verify at the consumer boundary.

No new roadmap work. No release policy. No provider work.
```

STOP.
