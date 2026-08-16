# Perception Phase 3 / Phase 4 Final Consumer-Authority Correction Result

> Date: 2026-08-14
> Role: Sol adversarial semantic-enforcement correction (implementation authorized)
> Authority: `SOL_PERCEPTION_PHASE3_PHASE4_FINAL_CONSUMER_AUTHORITY_CORRECTION`
> Mode: `EXACT_SURVIVING_S1_CORRECTION_ONLY` — **ARCHITECTURE_REOPEN NO**
> Input: `perception-phase3-phase4-final-record-authority-reaudit-result.md`
>   (REPAIR_INCOMPLETE, S1=3: GAP-004 / GAP-006 / GAP-008)
> Result: `PERCEPTION_PHASE3_PHASE4_FINAL_CONSUMER_AUTHORITY_CORRECTION_RESULT`
> Status: **VALIDATED_READY_FOR_FINAL_CLOSURE_REAUDIT**

---

## 0. Verdict

```text
Status: VALIDATED_READY_FOR_FINAL_CLOSURE_REAUDIT
S0Count: 0
S1Count: 0
RemainingS1: NONE
AuthoritativeBypassableCount: 0
CriticalUnprovenCount: 0
AmbiguousAuthoritativePaths: 0
PublicCallerMintPaths: 0
SemanticClosureDeclarable: NO (declaration reserved for the final closure
  re-audit; this round may only declare VALIDATED_READY_FOR_FINAL_CLOSURE_REAUDIT)
ArchitectureReopenRequired: NO
```

The three surviving consumer-boundary seams are closed by moving scope
derivation INTO the record-minting boundaries:

- **GAP-004** — `BaselineReport.create` now derives the authoritative
  population (and every derived field) from the persisted canonical chain
  (EvaluationRunRequest → EvaluationSuite → terminal EvaluationRunResult →
  per-member Prediction/failure/insufficiency → exact GroundTruth identity →
  re-scoring → counts/coverage/sufficiency/task denominators/safety
  scorecard). The caller can no longer choose the population; GT version is
  resolved deterministically (no glob order); a safety scorecard that was
  caller-created has zero authority and is represented as
  UNAVAILABLE / INSUFFICIENT_EVIDENCE instead.
- **GAP-006** — executed training bytes now bind to the admitted
  DatasetVersion manifest at `execute_training`, before `model.train`.
  `data_path` is a LOCATION only, never semantic identity; every resolved
  image's actual bytes must equal the admitted membership exactly, and every
  label must bind to the canonical Annotation record (YOLO label filenames
  are never trusted). Binding evidence is persisted into the session
  evidence. Failure modes all fail closed (FINAL-LEAK-01..10).
- **GAP-008** — `commit_execution_run` is now a pure DERIVATION/COMMIT
  boundary: it loads the persisted execution session by its content
  address + the persisted TrainingConfig by the session's config identity,
  and derives state / terminal_outcome / base model / environment /
  checkpoints / metrics from actual execution evidence
  (FINAL-TRAIN-01..14).

DO_NOT_TOUCH respected: GAP-007, GAP-002, GAP-009, PF01, PF02, Agent,
Brain, ReleasePolicy, EvaluationProfile, model-quality optimization — none
modified. No roadmap expansion (no generic provenance/authority framework,
no DB, no PKI, no ReleasePolicy, no EvaluationProfile, no PF02).

---

## 1. Authority

```text
SOL_PERCEPTION_PHASE3_PHASE4_FINAL_CONSUMER_AUTHORITY_CORRECTION
MODE: EXACT_SURVIVING_S1_CORRECTION_ONLY
IMPLEMENTATION AUTHORIZED
ARCHITECTURE_REOPEN: NO
```

Correction targets, exactly:

1. **GAP-004** — BaselineReport canonical scope authority.
2. **GAP-006** — executed training bytes ↔ admitted DatasetVersion manifest
   content binding.
3. **GAP-008** — commit_execution_run as derivation/commit boundary (not a
   second data-entry API).

---

## 2. GAP-004 — BaselineReport canonical scope authority (CLOSED)

Production changes (`platforms/perception/evaluation/`):

- `baseline.py`: `BaselineReport.create` now derives the authoritative
  population from the persisted canonical chain — the terminal
  EvaluationRunResult's members are the ONLY population; per-member status
  is Prediction (scored) / failure / insufficiency, never caller-chosen.
  `declared_tasks`, counts, coverage, sufficiency, task denominators, and
  the safety scorecard are derived from re-scored content. The safety
  scorecard is NEVER caller-created: a caller-supplied scorecard has zero
  authority and is represented as UNAVAILABLE / INSUFFICIENT_EVIDENCE.
  GT identity is the exact (asset_id, gt_version) recorded in the terminal
  outcome; scoring re-loads that exact GroundTruth and re-scores through
  the canonical context. `persist_baseline` identity excludes history
  metadata (`baselineId`, `createdAt`) so identity derives from content.
- `groundtruth.py`: `load_groundtruth_exact(asset_id, gt_version, root)`
  — deterministic filename `gt-{asset}-v{version}.json`, verified against
  the record's own fields. **No glob, no first-match.**
- `provenance_scorecard.py`: uses `load_groundtruth_exact` — the
  `glob(...)[0]` GT-version selection is GONE.
- `first_baseline.py`: `_load_gt` fails closed with
  `AMBIGUOUS_GROUND_TRUTH` when >1 GT version exists for an asset.
- `run.py` / `suite.py` / `metrics.py` / `scorecard.py` / `asset.py` /
  `prediction.py` / `deployment.py`: GAP-004 corrections already in the
  audited tree (out-of-scope outcome rejection, `load_terminal_result`
  skipping non-result records, taskSlices containing only SCORED stances).

Tests: FINAL-MET-01..12 (`test_rm_met.py`, `test_suite_baseline.py`).

Key adversarial results:
- MET-01..05: caller cannot choose population/classified/declared_tasks/
  counts/coverage — cherry-pick and out-of-scope inclusion blocked.
- MET-06..08: sufficiency and task denominators derive from the canonical
  population; ghost/missing members fail closed.
- MET-09: ELEMENT_DETECTION task slice discriminates declared-task presence
  (SAFETY coordinate-validity is ALWAYS scorable, B11).
- MET-10..11: safety scorecard caller-created = zero authority →
  UNAVAILABLE / INSUFFICIENT_EVIDENCE; never fabricated.
- MET-12: GT version owner missing → STOP with
  `SEMANTIC_PRESSURE_GT_VERSION_OWNER_MISSING`; baseline record frozen.

---

## 3. GAP-006 — executed bytes ↔ admitted manifest binding (CLOSED)

Production changes (`platforms/perception/training/dataset.py`,
`training_config.py`):

- `resolve_training_input_binding(data_yaml_path, dataset, *,
  annotation_dir)` — resolves `data.yaml` → train/val image populations +
  label populations → actual file bytes (sha256) → exact-set comparison
  against the admitted membership (content identity). Labels are bound to
  canonical Annotation records (asset_id + label content), never to YOLO
  label filenames. Produces a content-addressed `TrainingInputBinding`
  (`training-input-binding:{hash}`) with split counts, per-member
  bindings, and evidence metadata.
- Failure modes, all fail closed: DATA_YAML_UNRESOLVABLE,
  SPLIT_DIR_UNRESOLVABLE, UNRELATED_DATA_PATH (zero overlap),
  AMBIGUOUS_MATERIALIZATION (cross-split duplicate content), MISSING_REQUIRED_IMAGE,
  EXTRA_IMAGE, CHANGED_BYTES (same count, different content),
  LABEL_FILE_MISSING, LABEL_CONTENT_MISMATCH, LABEL_ANNOTATION_MISMATCH
  (annotation record unresolvable OR annotation asset ≠ member asset).
- `execute_training` order (canonical): admission re-derivation + persisted
  receipt verification → **content binding** → environment capture →
  `model.train` → evidence persist. The binding is persisted into the
  session evidence (`trainingInputBinding`), so the execution evidence
  itself proves bytes == manifest.

Tests: FINAL-LEAK-01..10 + FINAL-LEAK-10b (direct resolver positive).
All ten failure classes probed + path-independence proof
(FINAL-LEAK-10: identical content relocated to a new root + data.yaml
still binds with the SAME content-derived binding id; `execute_training`
exposes `data_path` (location) only — no caller-supplied semantic identity
parameter).

---

## 4. GAP-008 — commit boundary derivation (CLOSED)

Production changes (`platforms/perception/training/training_config.py`,
`training_run.py`, `mini.py`):

- `TrainingExecutionSession` extended with persisted internal execution
  evidence: `training_input_binding`, `captured_environment`,
  `produced_checkpoints` (name + sha256 of the ACTUAL produced file),
  `training_metrics` (from the framework's actual `results_dict`).
  Session identity remains content-derived (`execution:{hash(payload)}`);
  the new fields change canonical payload → new evidence ids; historical
  persisted evidence remains readable via tolerant `from_evidence_json`
  (no migration; HistoricalArtifacts WAIVED_BY_HUMAN_NOT_EXECUTED).
- `execute_training` captures the runtime environment at execution time and
  records produced checkpoints/metrics from the real results object.
- `training_run_from_execution` derives EVERYTHING authoritative:
  state/terminal_outcome from `session.terminal_error` (null → COMPLETED,
  else FAILED); `base_model_artifact_id` from the persisted TrainingConfig
  (via `session.training_config_id`); environment from the captured
  capture; checkpoints re-verified against the actual produced files
  (missing/altered → TRAINING_INVOCATION_MISMATCH); metrics from actual
  execution output.
- `commit_execution_run(session_evidence_id, config_dir, ...)` — loads the
  persisted session by content-addressed id and the persisted config by the
  session's config identity; has NO state/terminal_outcome/base_model/
  environment/checkpoints/metrics parameters. `save_training_run` remains a
  refused surface; `commit_execution_run` is the ONLY terminal TrainingRun
  writer and its only production consumer is `training.mini`.
- `mini.py` updated: no manual environment block; metrics come from the
  session; both commit branches use `session_evidence_id` + `config_dir`.

Tests: FINAL-TRAIN-01..14 (signature inspection; COMPLETED/FAILED
derivation; terminal_outcome carries the actual error; base model from
persisted config; environment from captured capture; checkpoint id ==
sha256 of the actual produced file; checkpoint revocation fails commit;
metrics from actual output; metrics omitted when not captured; forged/
absent evidence id → error; absent persisted config → error; persisted run
identity content-derived + loadable; no alternate terminal writer).

---

## 5. Targeted falsifier battery (current counts)

```text
FINAL-MET-01..12    : 12/12 PASS   (GAP-004, evaluation/tests)
FINAL-LEAK-01..10   : 10/10 PASS   (GAP-006, training/tests)
FINAL-TRAIN-01..14  : 14/14 PASS   (GAP-008, training/tests)
Total targeted      : 36/36 PASS   (+1 bonus FINAL-LEAK-10b direct
                                    resolver positive)
```

---

## 6. Fresh canonical mini training — process proof

Executed against a CLEAN copy of the production package (verbatim copy of
`platforms/perception`, artifacts + tests excluded, run from the copied
tree) so the historical-format artifacts in the repo are untouched
(WAIVED_BY_HUMAN_NOT_EXECUTED; write-once correctly refused to rewrite a
legacy acceptance-event file whose format predates `acceptedAnnotationId`).

Full canonical chain executed with real ultralytics 8.4.115 / torch 2.2.2
(CPU, 1 epoch, 4 train / 2 val images, `training.mini` run_mini_training):
accepted Annotation → DatasetVersion → admission → receipt → canonical
materialization + content binding → execute_training → persisted session
evidence → derived terminal TrainingRun → Checkpoint → ModelArtifact →
Candidate. Status COMPLETED; run
`trun:e7b421eae4f3ea2d9dd157cd2cd4f0a756e9e32570b8559adc601c3469b2a8c0`.

Independent verification (script `verify_mini_gap006.py`, reads persisted
records only):
- latest TrainingRun COMPLETED with derived state/terminal_outcome,
  captured environment, produced checkpoints (best + last, both
  sha256-verified against the actual .pt files), training metrics from
  actual execution output, admission receipt binding, invocation hash;
- run.datasetVersionId == persisted DatasetVersion id == receipt
  datasetVersionId (ADMITTED);
- **actual image bytes under mini-data/images/{train,val} == admitted
  membership asset ids (exact set equality — 6 images, recomputed
  sha256)**;
- produced checkpoint hashes == sha256 of the actual produced .pt files.

Verifier output: `bytes==identity OK: 6 images match admitted membership`
→ `PROOF PASSED: executed training bytes == admitted dataset identity;
terminal run fully derived from persisted canonical evidence.`

---

## 7. Regression (current counts, fresh runs)

- Full perception suite (`platforms/perception`): **318 passed, 0 failed**
  (fresh full run)
- Evaluation module: 117 passed (incl. FINAL-MET-01..12)
- Training module: 99 passed (incl. FINAL-LEAK-01..10/10b, FINAL-TRAIN-01..14,
  RM-LEAK/ANN/TRAIN regression, TR-01..25 falsifiers)
- GAP-001/003/005/007/009/010/011/012 protected by existing suites — green.
- GAP-002 = E4, GAP-007 = E4, GAP-009 / P4-34 = E4 — untouched, green.
- `training/tests/test_rm_c3.py` migrated to the new APIs; old
  RM-LEAK/RM-ANN/RM-TRAIN semantics preserved as regression.
- TR-16 (training-metric release authority) clarified: the capture boundary
  (`training.training_config`) RECORDS actual execution output into session
  evidence; no module reads training metrics into promote/release/activate
  decisions.

---

## 8. Historical artifacts

```text
HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED
```

No historical rewrite, no legacy migration. The repo's prior mini-training
artifacts (older event/annotation serializations) are preserved as-is;
the fresh proof ran in an isolated tree.

---

## 9. Deltas

```text
RuntimeDelta: EXECUTION_BOUNDARY_ONLY — binding + environment capture +
  checkpoint/metrics recording happen inside execute_training, before
  model.train; commit loads persisted evidence by id.
SemanticDelta: population/state/base_model/environment/checkpoints/metrics
  are no longer caller-declarable at any public boundary — they are
  derived from persisted canonical evidence.
AuthorityDelta: 3 S1 consumer-boundary seams closed; AmbiguousAuthoritativePaths
  1 → 0 (GT glob selection removed); PublicCallerMintPaths stays 0.
ArchitectureReopenRequired: NO
```

---

## 10. Final output block

```text
PERCEPTION_PHASE3_PHASE4_FINAL_CONSUMER_AUTHORITY_CORRECTION_RESULT

Status: VALIDATED_READY_FOR_FINAL_CLOSURE_REAUDIT

S0Count: 0
S1Count: 0
RemainingS1: NONE
AuthoritativeBypassableCount: 0
CriticalUnprovenCount: 0
AmbiguousAuthoritativePaths: 0
PublicCallerMintPaths: 0

GAP004ScopeAuthority: CLOSED
  (population/classified/declared_tasks/counts/coverage/sufficiency/task
   denominators/safety_scorecard/GT version all DERIVED from persisted
   canonical records; caller cannot choose the authoritative population;
   no glob-order GT selection; no canonical GT version owner → STOP with
   SEMANTIC_PRESSURE_GT_VERSION_OWNER_MISSING; caller-created safety
   scorecard = zero authority → UNAVAILABLE/INSUFFICIENT_EVIDENCE;
   FINAL-MET-01..12 PASS)

GAP004GTVersionAuthority: DETERMINISTIC_EXACT
  (load_groundtruth_exact: deterministic filename, record self-verified;
   first_baseline AMBIGUOUS_GROUND_TRUTH fail-closed; provenance_scorecard
   no longer globs)

GAP006ExecutionContentBinding: CLOSED
  (resolve_training_input_binding before model.train; data_path = LOCATION
   only; labels bound to canonical Annotation records, never YOLO label
   filenames; binding evidence persisted into session evidence;
   FINAL-LEAK-01..10 PASS)

GAP006ActualBytesVsManifest: EXACT_SET_EQUALITY_VERIFIED
  (resolved image bytes sha256 == admitted membership asset ids; missing/
   extra/changed bytes, ambiguous materialization, unrelated data_path,
   unresolvable data.yaml, missing/wrong labels — all fail closed;
   FINAL-LEAK-01..10 PASS)

GAP008TerminalStateAuthority: DERIVED
  (session.terminal_error null → COMPLETED else FAILED; terminal_outcome
   carries the actual error; FINAL-TRAIN-02/03/04 PASS)

GAP008BaseModelAuthority: DERIVED_FROM_PERSISTED_CONFIG
  (base_model_artifact_id from persisted TrainingConfig via
   session.training_config_id; no commit param; FINAL-TRAIN-05 PASS)

GAP008CheckpointAuthority: ACTUAL_FILE_VERIFIED
  (checkpoint id = sha256 of the produced file, session-bound;
   revocation fails commit; FINAL-TRAIN-07/08 PASS)

GAP008EnvironmentAuthority: CAPTURED_DURING_EXECUTION
  (TrainingEnvironment derived from the capture, never caller-declared;
   FINAL-TRAIN-06 PASS)

GAP008MetricsAuthority: ACTUAL_EXECUTION_OUTPUT_ONLY
  (metrics from framework results; omitted when not captured;
   FINAL-TRAIN-09/10 PASS)

FinalTargetedFalsifiers: 36/36 PASS
  (FINAL-MET-01..12 + FINAL-LEAK-01..10 + FINAL-TRAIN-01..14; +1 bonus
   FINAL-LEAK-10b)

AmbiguousAuthoritativePaths: 0
PublicCallerMintPaths: 0

Regression: PASS (current counts — see §7)
FreshCanonicalMiniTraining: PASS
  (fresh real ultralytics run; executed bytes == admitted dataset identity;
   derived terminal run/checkpoint/artifact/candidate; repo artifacts
   untouched)

HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED

RuntimeDelta: EXECUTION_BOUNDARY_ONLY
SemanticDelta: DERIVED_FROM_PERSISTED_CANONICAL_EVIDENCE
AuthorityDelta: 3_S1_SEAMS_CLOSED
ArchitectureReopenRequired: NO

ReadyForFinalClosureReaudit: YES
NextTask: SOL_PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT
```
