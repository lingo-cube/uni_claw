# SOL_PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT — Result

> Authority: `SOL_PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT`
> Role: Fresh independent GPT-5.6 Sol semantic closure auditor
> Mode: **AUDIT_ONLY** — no production code, no tests, no repairs, no
> architecture reinterpretation, no roadmap expansion.
> Result: `PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT_RESULT`
> Status: **REPAIR_INCOMPLETE**
> Date: 2026-08-14

## 0. Method

The previous correction result
(`perception-phase3-phase4-final-consumer-authority-correction-result.md`)
was **not trusted**. Only executable repository behavior was trusted.

Three **fresh, independently written** attack-replay probes were executed
against the current working tree (probes live outside the repo, in `/tmp`):

| Probe | Scope | Replays | Result |
|-------|-------|---------|--------|
| `audit_gap004_probe.py` | GAP-004 | cherry-pick via create(); denominator shrink; out-of-suite outcome; GT version determinism under flipped filesystem order; caller-minted forged report through persist_baseline | **13/14 PASS — 1 FAIL** |
| `audit_gap006_probe.py` | GAP-006 | receipt=A / data_path=B before model.train; matrix 1–6; path independence; run dataset identity source | **20/20 PASS** |
| `audit_gap008_probe.py` | GAP-008 | fake terminal-fact commit params; state/outcome/base-model/checkpoint/environment/metrics derivation; tamper revocation; forged evidence id; missing config | **16/16 PASS** |

## 1. GAP-004 — Baseline Quality Authority

### PASS (replayed, executable)

- **Cherry-pick impossible via the sanctioned API.** `BaselineReport.create`
  exposes no `scoring_results` / `asset_count` / `scored_count` /
  `classified` / `declared_tasks` / `safety_scorecard` / `coverage` /
  `gt_version` / `population` parameter (signature inspection), and passing
  `scoring_results` raises `TypeError`. Caller scope cannot enter `create()`.
- **Denominator unshrinkable.** Honest 10-member chain (7 scored /
  3 insufficient) yields `assetCount=10`, `scoredCount=7`,
  `unscoredCount=3`, `coverage.assetCount=10`, per-task slice
  `denominator=7` (scored-only, by frozen semantics) with population
  denominator 10 carried in coverage, and `evidenceSufficiency.stance =
  PARTIAL` (7/10 never reports SUFFICIENT).
- **Out-of-suite evidence rejected.** Terminal result containing an outcome
  outside the requested population raises
  `PROVENANCE_MISMATCH:OUT_OF_SCOPE_OUTCOME`.
- **GT version deterministic, no glob order.** `load_groundtruth_exact`
  resolves the exact claimed version; flipping file mtimes/order cannot
  change resolution (claims v10 → v10 with `ELEMENT_DETECTION` scored;
  claims v1 → v1, no task slice). Wrong/missing exact version → member
  `UNSCORABLE`, never substitution. `first_baseline._load_gt` fails closed
  with `AMBIGUOUS_GROUND_TRUTH` on multiple versions.

### FAIL (surviving S1 — the only one found)

- **Public caller mint path at the persistence boundary.**
  `BaselineReport` is a plain public frozen dataclass with no
  `__post_init__` validation, and `persist_baseline` validates only
  self-consistency (identity derives from own content; counts match
  coverage; body matches fields) — **not** that the report was derived from
  canonical evidence. Executable proof: a fully self-consistent forged
  report (fabricated `assetCount=7 / scoredCount=7`, fabricated
  `safetyScorecard={'forged': True}`, fabricated
  `evidenceSufficiency=SUFFICIENT`, `scoringResultCount=0`) constructed via
  the public constructor was **accepted by `persist_baseline`** and written
  as `baseline:<content-hash>.json` (persisted JSON verified by read-back:
  `assetCount: 7`, `scoredCount: 7`, `safetyScorecard: {'forged': True}`,
  `requestId: run:FORGED`). `evaluation/incremental.py` reads persisted
  baseline files by id as the previous baseline with no re-derivation, so
  the forged record carries downstream authority.
- This contradicts the correction's declared `PublicCallerMintPaths: 0` and
  "caller-created safety scorecard = zero authority": the persistence
  boundary accepts caller-derived quality fields verbatim.
- **GAP-004 verdict: FAIL** (1 surviving S1; all other sub-criteria PASS).

### Doc-vs-behavior discrepancy (recorded, not itself a bypass)

- The correction result doc claims `SEMANTIC_PRESSURE_GT_VERSION_OWNER_MISSING`
  as part of FINAL-MET-12. That string exists **nowhere** in executable code
  or tests (repo-wide grep: doc only). Executable GT authority is
  `AMBIGUOUS_GROUND_TRUTH` fail-closed + exact-identity resolution +
  UNSCORABLE representation — which satisfies the audit brief's GT criteria.
  The doc over-claims a code that is not implemented.

## 2. GAP-006 — Training Content Binding

All 20 fresh replay checks PASS:

1. **receipt=A, data_path=B (different images)** → `TRAINING_DATA_BINDING_MISMATCH`
   raised; **`model.train` never invoked** (0 invocations).
2. **Same filenames, different bytes** → FAIL (identical filenames, altered
   content rejected).
3. **Extra image** → FAIL (`EXTRA_IMAGE`).
4. **Missing image** → FAIL (`MISSING_REQUIRED_IMAGE`).
5. **Changed label file** → FAIL (`LABEL_CONTENT_MISMATCH`).
6. **Different directory, same content** → PASS; identical content-addressed
   `binding_id` (`training-input-binding:c3d9030e…` both times) — path is
   location only, never identity.
7. **Correct canonical materialization** → PASS; evidence:
   `datasetVersionId` matches, `resolvedMemberCount=3`,
   `image_content_ids=3`, `label_annotation_bindings=3`,
   `split_counts={train:2, val:1}`, `binding_evidence{contentIdentity:
   sha256, bindingVersion: GAP-006-v1}`.
8. **Dataset identity source**: full execute+commit yields
   `run.dataset_version_id == session.dataset_version_id == ds.id ==
   binding.datasetVersionId`, with receipt id recorded; `execute_training`
   re-derives the canonical admission (recomputed receipt must equal the
   claimed persisted receipt) before binding, so neither `data_path`, the
   receipt alone, nor any caller field can inject dataset identity.
- **GAP-006 verdict: PASS.**

## 3. GAP-008 — TrainingRun Authority

All 16 fresh replay checks PASS:

- `commit_execution_run` has **no** `state` / `terminal_outcome` /
  `base_model_artifact_id` / `environment` / `produced_checkpoints` /
  `training_metrics` / `checkpoints` / `dataset` parameter (signature
  inspection; passing any raises `TypeError`).
- **state/terminal_outcome**: derived from `session.terminal_error`
  (empty → `COMPLETED`/`completed`; `RuntimeError: boom` → `FAILED` /
  `failed: RuntimeError: boom`).
- **base_model_artifact_id**: derived from the persisted `TrainingConfig`
  loaded via `session.training_config_id` (`sha256:base-model` observed).
- **checkpoints**: taken from actual produced files; content hash
  re-verified at commit — tampering the checkpoint file after execution
  raises `TrainingInvocationMismatchError` (no lineage).
- **environment**: from `session.captured_environment` captured during
  execution (os/python/device/seed observed in run).
- **metrics**: from actual execution output only (`mAP50=0.5,
  fitness=0.4` observed).
- **forged evidence id** and **missing persisted config** → both rejected.
- **GAP-008 verdict: PASS.**

## 4. Regression — exact fresh counts (current run, not reused counts)

| Suite | Count |
|-------|-------|
| Full perception pytest (`platforms/perception`, one run) | **318 passed / 0 failed** |
| — `platforms/perception/tests` | 26 passed |
| — `evaluation/tests` (incl. FINAL-MET-01..12, RM-MET-01..10) | 117 passed |
| — `training/tests` (incl. FINAL-LEAK-01..10/10b, FINAL-TRAIN-01..14) | 99 passed |
| — `governance/tests` (governance falsifiers / identity / runtime snapshot) | 48 passed |
| — `uniclaw_perception/tests` | 9 passed |
| — `tools/model_intelligence/tests` (model intelligence) | 19 passed |
| Targeted falsifier battery (test_rm_met + test_suite_baseline + test_rm_c3) | **93 passed** in 3.96s (37 targeted FINAL falsifiers + preserved RM-*) |
| .NET regression (`dotnet test src/UniClaw.Runtime.sln`) | **905 passed / 1 failed / 906 total**, 0 build warnings/errors; architecture guards passed |
| — `Vision.CORR_HOST04_RestartReverifiesRealChild` (failed in full run) | **passes 1/1 in isolation** (real child-process restart integration test, flaky under full-suite load) |

The single .NET failure is unrelated to this closure: the correction diff
contains **zero .NET files** (git status: only `platforms/perception/**` +
`docs/decisions/**`), and the test spawns a real Vision-host child process
and restarts it (environmental/flaky, green in isolation).

## 5. Closure decision

| Criterion | Count |
|-----------|-------|
| S0Count | 0 |
| S1Count | **1** |
| AuthoritativeBypassableCount | **1** |
| PublicCallerMintPaths | **1** |
| CriticalUnproven | 0 |

`SemanticClosureDeclarable = NO` — the closure rules require S1 = 0,
AuthoritativeBypassableCount = 0 and PublicCallerMintPaths = 0; the single
surviving GAP-004 persistence-boundary mint path violates all three.

## 6. Result block

```
PERCEPTION_PHASE3_PHASE4_FINAL_CLOSURE_REAUDIT_RESULT

Status: REPAIR_INCOMPLETE

S0Count: 0
S1Count: 1

RemainingGaps:
  The ONLY executable surviving bypass: persist_baseline accepts a fully
  self-consistent forged BaselineReport (public frozen dataclass, no
  __post_init__ validation; persist validates self-consistency only, never
  derivation from canonical evidence) — caller-minted fabricated counts,
  safety scorecard and SUFFICIENT sufficiency persist as
  baseline:<content-hash>.json and are consumed by incremental.py with
  no re-derivation. Declared "PublicCallerMintPaths: 0" and "safety
  scorecard caller-created = zero authority" are contradicted by this
  executable behavior. (Secondary, non-bypass discrepancy: the correction
  doc claims SEMANTIC_PRESSURE_GT_VERSION_OWNER_MISSING, which exists
  nowhere in code/tests; executable GT authority is AMBIGUOUS_GROUND_TRUTH
  fail-closed + exact-identity resolution + UNSCORABLE representation.)

GAP004: FAIL
GAP006: PASS
GAP008: PASS

SemanticClosureDeclarable: NO

AuthoritativeBypassableCount: 1
PublicCallerMintPaths: 1

FreshAttackReplay: PARTIAL — GAP-006 20/20 PASS, GAP-008 16/16 PASS,
  GAP-004 13/14 PASS (1 surviving mint path at the persist boundary)

Regression: exact counts —
  perception full: 318 passed / 0 failed
    (tests 26, evaluation 117, training 99, governance 48,
     uniclaw_perception 9, model_intelligence 19)
  targeted falsifier battery: 93 passed
  .NET: 905 passed / 1 failed / 906 (Vision CORR_HOST04 real-child restart
    flaky under full-suite load; passes 1/1 isolated; 0 warnings/errors;
    architecture guards passed; failure unrelated to the correction diff —
    no .NET files modified)

RuntimeDelta: NONE (no runtime behavior changed by this audit)
SemanticDelta: NONE (this audit changes no semantics; records that the
  previous correction doc over-claims SEMANTIC_PRESSURE_GT_VERSION_OWNER_MISSING,
  absent from executable behavior)
AuthorityDelta: 1_S1_SEAM_SURVIVES — GAP-004 persistence boundary accepts
  caller-derived quality fields on a self-consistent forged report

ArchitectureReopenRequired: NO (the surviving bypass is a same-seam
  enforcement gap at the existing persist_baseline boundary; no
  architecture reopen needed)

NextTask: ONLY_THE_EXACT_SURVIVING_CORRECTION
```

STOP.
