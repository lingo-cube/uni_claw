# Perception Phase 3 / Phase 4 Gap Register

> Audit commit: `d843557c87456841369cefc46473d40d42997544`  
> Discovery was read-only except disposable falsifier probes. No production repair is included here.

## Summary

| GapId | Phase | RuleId | Severity | Type | Disposition | Confidence |
|---|---|---|---|---|---|---|
| GAP-001 | P3 | P3-05 | S2 | FAIL_CLOSED_GAP | FIX_BEFORE_L3 | HIGH |
| GAP-002 | P3 | P3-11 | S1 | RULE_BYPASSABLE | FIX_NOW | HIGH |
| GAP-003 | P4 | P4-01 | S1 | IDENTITY_GAP | FIX_NOW | HIGH |
| GAP-004 | P4 | P4-04, P4-05, P4-36 | S1 | PROVENANCE_GAP | FIX_NOW | HIGH |
| GAP-005 | P4 | P4-14, P4-15, P4-21, P4-23, P4-37 | S1 | IMMUTABILITY_GAP | FIX_BEFORE_RELEASE_POLICY | HIGH |
| GAP-006 | P4 | P4-17 | S1 | PROVENANCE_GAP | FIX_BEFORE_RELEASE_POLICY | HIGH |
| GAP-007 | P4 | P4-03, P4-18 | S1 | AUTHORITY_DRIFT | FIX_BEFORE_RELEASE_POLICY | HIGH |
| GAP-008 | P4 | P4-19, P4-20 | S1 | PROVENANCE_GAP | FIX_BEFORE_RELEASE_POLICY | HIGH |
| GAP-009 | P4 | P4-34, P4-35 | S1 | TEST_GAP | FIX_BEFORE_L3 | MEDIUM |
| GAP-010 | P4 | P4-14, P4-36 | S1 | PROVENANCE_GAP | FIX_NOW | HIGH |
| GAP-011 | P3→P4 | P3-10 | S3 | SEMANTIC_DRIFT | RECORDED_DEFERRAL | HIGH |
| GAP-012 | P3 | P3-14 | S3 | DOCUMENTATION_DRIFT | FIX_NOW | HIGH |

No S0 gap was retained. The discovered bypasses corrupt or weaken identity/evidence/governance claims but do not give Perception, Host, Evaluation, or Training Agent authority, action authority, goal-completion authority, or an operational deployment activation path.

## GAP-001 — Infrastructure failure and empty-world evidence collapse

- **Phase / Rule:** P3 / P3-05
- **Severity / Type:** S2 / `FAIL_CLOSED_GAP`
- **Frozen intent:** Perception failure produces no fabricated evidence, while operational diagnostics distinguish `NO_ELEMENTS` from `PERCEPTION_INFRASTRUCTURE_FAILURE`.
- **Actual implementation:** `LocalVisionPerceptionSource.AnalyzeAsync` returns an `ImmutableArray<PerceptionCandidate>` only. HTTP non-success and a response without candidates both return `[]`; transport and JSON failures have no typed evidence result at this boundary.
- **Exact bypass/reproduction:** Make `/v1/analyze` return HTTP 500, then return HTTP 200 with `{"candidates":[]}`. Both successful method results visible to downstream are an empty candidate array (the 500 path explicitly returns `[]`; the no-element path also returns an empty builder). No result field preserves the distinction.
- **Production path:** `PhysicalEnvironment` → `IPerceptionSource.AnalyzeAsync` → `LocalVisionPerceptionSource` → Python UDS endpoint.
- **Why tests missed it:** Existing tests prove safety and empty evidence, not a typed operational distinction at the adapter contract.
- **Impact:** Safety is fail-closed, but diagnostics/replay/failure intelligence cannot tell a genuine empty screen from an analysis outage; triage and L3 operational evidence are weakened.
- **Recommended disposition:** `FIX_BEFORE_L3` within the perception/adapter evidence boundary; do not move failure authority into Agent.
- **Confidence:** HIGH.

## GAP-002 — Coordinate service contract permits out-of-range evidence

- **Phase / Rule:** P3 / P3-11
- **Severity / Type:** S1 / `RULE_BYPASSABLE`
- **Frozen intent:** Serialized bounds are full-original-screenshot normalized coordinates in `[0,1]^2` with top-left origin.
- **Actual implementation:** `remap_coords` divides remapped pixels by original dimensions without clamping or rejecting invalid input. Runtime later nulls an invalid `ElementBounds`, preventing unsafe spatial use, but the HTTP service still returns schema-invalid spatial evidence as a successful response.
- **Exact bypass/reproduction:** Invoke `remap_coords` with `boundsPx=[-10,-5,120,110]`, `scale=2`, `top_px=10`, `orig_w=100`, `orig_h=100`. The serialized bounds become `x1=-0.2`, `x2=2.4`, `y2=2.3`.
- **Production path:** YOLO/OCR/Fusion evidence → `uniclaw_perception.remap` → response serialization → `LocalVisionPerceptionSource`.
- **Why tests missed it:** Coordinate tests use valid detections and verify ordinary remapping/equivalence; they do not inject negative or overflow pixel bounds at the production remap boundary.
- **Impact:** The service violates its normalized evidence contract while reporting request success. C# safely discards the bounds, so this is not fabricated Agent truth or unsafe dispatch, but identity/truth consumers outside that adapter can consume invalid evidence.
- **False-positive self-check:** Downstream `ElementBounds.IsValid` was found and prevents invalid bounds from becoming usable Runtime coordinates. That guard reduces severity from S0 but does not repair the successful service output contract or non-C# consumers.
- **Recommended disposition:** `FIX_NOW`; reject/clip according to an explicitly chosen output policy and add an end-to-end invalid-coordinate falsifier.
- **Confidence:** HIGH.

## GAP-003 — EvaluationAsset identity is trusted, not revalidated on load

- **Phase / Rule:** P4 / P4-01
- **Severity / Type:** S1 / `IDENTITY_GAP`
- **Frozen intent:** `AssetId` is the SHA-256 identity of source bytes and is independent of path/name.
- **Actual implementation:** `from_file/from_bytes` computes the identity correctly. `from_manifest` trusts `content_hash`, ignores the persisted `assetId`, and does not hash `source_path` bytes. Suite and baseline membership then trust the loaded ID.
- **Exact bypass/reproduction:** Edit an asset manifest so `content_hash` names asset A while `source_path` points to different bytes B (or make `assetId` disagree with `content_hash`). `load_asset_manifest` accepts it and `asset_id` returns the injected hash.
- **Production path:** asset manifest → `load_asset_manifest` → suite/baseline/candidate evaluation asset lookup.
- **Why tests missed it:** Tests prove same bytes across rename/move and normal manifest round-trip. They do not tamper the stored hash/path relationship.
- **Impact:** Evaluation evidence can be attributed to the wrong content identity, contaminating suite membership, GT association, and score history.
- **False-positive self-check:** Content-addressed filenames and `from_file` are valid construction guards, but neither restricts direct `from_manifest` or validates source bytes at use time. No composition-root restriction or load guard was found.
- **Recommended disposition:** `FIX_NOW`; independently validate manifest identity and source-byte identity before evaluation use.
- **Confidence:** HIGH.

## GAP-004 — Direct metric invocation is not bound to Prediction provenance

- **Phase / Rule:** P4 / P4-04, P4-05, P4-36
- **Severity / Type:** S1 / `PROVENANCE_GAP`
- **Frozen intent:** A score must bind compatible Prediction and GT asset, stage, and label-space provenance; incompatibility is `NOT_SCORABLE`.
- **Actual implementation:** Canonical baseline and candidate flows explicitly pass fused stage/space. The public `compute_task_metrics` accepts an unbound list of candidate dictionaries plus GT, accepts no prediction asset ID, and silently defaults omitted prediction stage/space to fused values. It also cannot establish that declared stage views were actually present.
- **Exact bypass/reproduction:** Take candidates produced for asset A and call `compute_task_metrics(candidates_A, ground_truth_B)` without stage/space. The call assigns fused defaults and can produce scores for B; no asset mismatch can be detected by the function.
- **Production path:** canonical `first_baseline` and `candidate_eval` are guarded by caller discipline; the reusable evaluation metric API is the alternate production path.
- **Why tests missed it:** Stage tests pass explicit mismatches and prove `NOT_SCORABLE`; metric tests use matching synthetic inputs and depend on the defaults. No test swaps asset identities.
- **Impact:** A valid-looking quality artifact can be computed for the wrong asset or an unproven stage/label provenance, undermining evaluation authority before ReleasePolicy exists.
- **False-positive self-check:** The two current high-level callers join by asset and pass explicit stage/space. This limits immediate exposure but does not make the shared API non-bypassable, and no type/composition constraint prevents another production caller.
- **Recommended disposition:** `FIX_NOW`; make scoring consume a provenance-bound prediction/GT pair and require explicit stage/space.
- **Confidence:** HIGH.

## GAP-005 — Append-only artifact semantics are not persistence-enforced

- **Phase / Rule:** P4 / P4-14, P4-15, P4-21, P4-23, P4-37
- **Severity / Type:** S1 / `IMMUTABILITY_GAP`
- **Frozen intent:** Suites, Runs, Baselines, DatasetVersions, Annotations, TrainingRuns, ModelArtifacts, Candidates, and legacy history are immutable/append-only.
- **Actual implementation:** Frozen dataclasses and content-derived IDs protect in-memory construction. Most `save_*` helpers unconditionally call `Path.write_text` on an ID-derived path. Baseline alone refuses different content by default, but exposes `overwrite=True` which bypasses the freeze.
- **Exact bypass/reproduction:** Save a valid record, then directly rewrite its ID-named JSON (or call a save helper with a forged/directly constructed same-ID record). The old historical bytes are replaced. For Baseline, call `persist_baseline(changed_report, dir, overwrite=True)`.
- **Production path:** evaluation asset/suite/run/prediction/baseline persistence; training annotation/dataset/config/run/checkpoint/artifact/candidate/lineage persistence; governance manifests.
- **Why tests missed it:** Tests prove frozen values, new identities on normal mutation, default baseline refusal, and ordinary history preservation. They do not uniformly attempt same-path overwrite through every writer; some source tests merely assert the absence of an `overwrite` token.
- **Impact:** Historical evidence can be silently rewritten under a stable artifact location, invalidating reproducibility, failure history, and legacy immutability claims.
- **False-positive self-check:** Content-addressed names make accidental collision less likely and Git may reveal changes for checked-in assets, but runtime-generated artifact directories and explicit overwrite paths have no write-once filesystem or store invariant. Baseline's default guard is real but optional.
- **Recommended disposition:** `FIX_BEFORE_RELEASE_POLICY`; centralize collision-safe write-once semantics without adding release authority.
- **Confidence:** HIGH.

## GAP-006 — Dataset leakage validation is optional and non-blocking

- **Phase / Rule:** P4 / P4-17
- **Severity / Type:** S1 / `PROVENANCE_GAP`
- **Frozen intent:** Exact-content, known capture-group, and protected-evaluation leakage are rejected.
- **Actual implementation:** `check_leakage` identifies findings, but `DatasetVersion` construction and `save_dataset` never invoke it. `run_mini_training` invokes it without protected IDs and proceeds regardless of non-empty findings.
- **Exact bypass/reproduction:** Construct a DatasetVersion containing the same capture group in TRAIN and VALIDATION or a protected asset in TRAIN; save it directly, or run training. The manifest is saved and training is not blocked.
- **Production path:** DatasetVersion constructor/save → mini training → TrainingRun.
- **Why tests missed it:** Tests directly call `check_leakage` and assert returned findings. They do not assert that dataset admission/training refuses them.
- **Impact:** Training/evaluation contamination can be recorded as reproducible evidence and later distort candidate comparison.
- **False-positive self-check:** There is no current ReleasePolicy, so no deployment is automatically activated. That does not preserve provenance truth or the frozen rejection rule.
- **Recommended disposition:** `FIX_BEFORE_RELEASE_POLICY`; enforce leakage at dataset admission and execution, with explicit protected membership input.
- **Confidence:** HIGH.

## GAP-007 — Annotation acceptance authority is directly constructible

- **Phase / Rule:** P4 / P4-03, P4-18
- **Severity / Type:** S1 / `AUTHORITY_DRIFT`
- **Frozen intent:** Model-assisted output is not training truth until an explicit reviewed acceptance event creates a new accepted version.
- **Actual implementation:** `accept_annotation` models the intended event, but the public frozen dataclass constructor and `create_annotation(review_status=...)` permit `MODEL_ASSISTED + ACCEPTED` directly. `from_json` trusts status, and DatasetMembership does not validate that a referenced annotation is accepted or has an acceptance predecessor.
- **Exact bypass/reproduction:** Call `create_annotation(source=MODEL_ASSISTED, review_status=ACCEPTED, ...)`, save it, then reference its ID in a DatasetVersion. No acceptance event or reviewer transition is required.
- **Production path:** annotation construction/load → save → DatasetVersion membership → training.
- **Why tests missed it:** The existing test follows the intended default-DRAFT then `accept_annotation` path; it does not invoke the optional accepted parameter or direct constructor.
- **Impact:** A model prediction can acquire training-truth authority without the frozen human/review authority event.
- **False-positive self-check:** The accepted status changes content identity and the `accept_annotation` helper records provenance, but neither fact prevents direct accepted creation; no dataset gate verifies event lineage.
- **Recommended disposition:** `FIX_BEFORE_RELEASE_POLICY`; make accepted construction exclusive to a validated acceptance transition and validate dataset references.
- **Confidence:** HIGH.

## GAP-008 — Recorded TrainingConfig is not checked against actual invocation

- **Phase / Rule:** P4 / P4-19, P4-20
- **Severity / Type:** S1 / `PROVENANCE_GAP`
- **Frozen intent:** TrainingConfig identity contains actual training-affecting inputs, and TrainingRun truthfully binds that execution.
- **Actual implementation:** The config hashes epochs/batch/imgsz/etc. `run_mini_training` then separately hardcodes corresponding `model.train` values; no adapter builds invocation from the config and no post-call congruence check exists.
- **Exact bypass/reproduction:** Change the hardcoded `model.train(imgsz=160, epochs=1, ...)` or call YOLO training directly while retaining the same saved TrainingConfig/TrainingRun IDs. The recorded configuration and execution can diverge undetected.
- **Production path:** `_build_training_config` → saved config → separately constructed `model.train` call → TrainingRun.
- **Why tests missed it:** Tests mutate config fields and observe a new ID; they do not intercept/compare the effective arguments passed to the training framework.
- **Impact:** Training lineage can be internally consistent as files yet false about the execution that produced a checkpoint/model artifact.
- **False-positive self-check:** The current constants happen to match in source and the mini artifact is test-only. No type or runtime guard makes that relationship invariant, and direct training remains possible.
- **Recommended disposition:** `FIX_BEFORE_RELEASE_POLICY`; derive execution inputs from the immutable config or capture and compare effective framework arguments.
- **Confidence:** HIGH.

## GAP-009 — Canonical Host identity verification is not production-composed

- **Phase / Rule:** P4 / P4-34, P4-35
- **Severity / Type:** S1 / `TEST_GAP`
- **Frozen intent:** The real canonical Host path requires expected identity, compares it to loaded `/version` facts, and fails closed.
- **Actual implementation:** `ForCanonicalProduction` requires ExpectedIdentity and `VerifyIdentityOrThrow` is fail-closed. Repository search found no production call to that factory and no production `new VisionServiceHost(...)` composition. Direct `new VisionHostConfig()` leaves ExpectedIdentity null and verification returns immediately. Canonical Python lifespan snapshots actuality; `/version` also has a noncanonical fallback.
- **Exact bypass/reproduction:** Construct the Host with `new VisionHostConfig { ... }`, omit ExpectedIdentity, and start a service with any compatible schema; identity comparison is skipped. More importantly, the repository has no concrete production composition proving the canonical factory is the path actually used.
- **Production path:** Intended `ForCanonicalProduction` → Host startup → `/version`; actual repository ends at a reusable Host library plus tests.
- **Why tests missed it:** Identity tests instantiate the guarded path/seam. They prove the mechanism, not a real application composition root. This audit's runtime-snapshot/restart suite did not return a terminal count after socket ResourceWarnings.
- **Impact:** The claimed canonical deployment verification is not enforceable at an absent composition root; a future caller can select the optional legacy path and still run the Host.
- **False-positive self-check:** The factory and startup guard are genuine and mismatch tests pass. Therefore this is not classified as an active wrong-deployment incident or S0. However, no build guard, internal constructor, or existing production composition makes the factory mandatory.
- **Recommended disposition:** `FIX_BEFORE_L3`; establish/authorize a concrete production composition root and mechanically forbid the unverified path there. Re-run real runtime-snapshot round-trip.
- **Confidence:** MEDIUM (lack of composition is certain; severity depends on the first real host consumer).

## GAP-010 — EvaluationRun declares COMPLETED before execution outcome

- **Phase / Rule:** P4 / P4-14, P4-36
- **Severity / Type:** S1 / `PROVENANCE_GAP`
- **Frozen intent:** EvaluationRun terminal truth reflects execution; deployment mismatches are infrastructure/evidence-integrity failures, never valid quality runs.
- **Actual implementation:** `EvaluationRun.create` sets `terminal_status=COMPLETED` before asset execution. The run is persisted, then per-asset `EvaluationInfrastructureError` is caught and recorded only in result dictionaries. The immutable run remains COMPLETED even when every execution failed.
- **Exact bypass/reproduction:** Create and save a run that claims a mismatched canonical deployment; execute assets. `run_fresh_inference` correctly raises mismatch, `execute_asset` catches it, but the persisted run record still says COMPLETED.
- **Production path:** `EvaluationRun.create/save_run` → baseline execution loop → `execute_asset` → caught infrastructure error.
- **Why tests missed it:** EXI tests prove mismatch rejection at the inference seam. Evaluation tests check per-asset unscored errors, not the already-persisted run terminal status.
- **Impact:** Consumers can read a COMPLETED run whose claimed identity was never successfully executed, confusing infrastructure failure with a complete evaluation episode.
- **False-positive self-check:** No score is fabricated for the failed asset and error detail is retained, so model-quality values remain fail-closed. The run-level terminal claim is nevertheless false and is a primary provenance artifact.
- **Recommended disposition:** `FIX_NOW`; model CREATED/RUNNING/terminal run outcome truthfully and persist terminal records append-only.
- **Confidence:** HIGH.

## GAP-011 — Frozen pipeline description omits material internal transformations

- **Phase / Rule:** P3→P4 / P3-10
- **Severity / Type:** S3 / `SEMANTIC_DRIFT`
- **Frozen intent:** The canonical pipeline description accurately conveys the evidence transformation boundary.
- **Actual implementation:** Stage order is still correct, but YOLO raw-label normalization, Fusion unmatched-OCR promotion/reclassification, post-remap scroll hints/metadata, and the evaluation-only stage-view channel are behaviorally meaningful details absent from the simple frozen description.
- **Exact reproduction:** Trace `server.run_pipeline`, YOLO inference, Fusion engine and serializer; observe outputs whose semantic type/label was normalized or promoted before final candidates.
- **Production path:** canonical Python analysis pipeline; evaluation enables the additive capture channel.
- **Why tests missed it:** Tests verify behavior/equivalence, not completeness of graduation prose.
- **Impact:** Auditors can misunderstand which stage owns normalization/promotion; no authority or Runtime invariant changes.
- **Recommended disposition:** `RECORDED_DEFERRAL`; correct normative description during the repair gate without redesign.
- **Confidence:** HIGH.

## GAP-012 — Stale legacy launch documentation remains in active source/help

- **Phase / Rule:** P3 / P3-14
- **Severity / Type:** S3 / `DOCUMENTATION_DRIFT`
- **Frozen intent:** Canonical package/entry point is unambiguous and legacy paths have zero active production references.
- **Actual implementation:** No legacy import or executable production path remains, but `LocalVisionPerceptionSource` XML documentation and `platforms/perception/cli/benchmark_raw.py` usage text still name `tools/local_vision`.
- **Exact reproduction:** Search production/help sources for `tools/local_vision`.
- **Production path:** Documentation/help only; not imported execution.
- **Why tests missed it:** Tests do not lint stale source comments/CLI usage strings.
- **Impact:** Operator/developer confusion; no runtime semantic impact.
- **Recommended disposition:** `FIX_NOW` as bounded documentation cleanup after the audit gate.
- **Confidence:** HIGH.

## Correlation and repair order

Shared root causes are deliberately consolidated rather than counted once per writer or artifact:

1. `GAP-002`, `GAP-003`, `GAP-004`, `GAP-010` — close successful-but-invalid evidence and run-truth paths first.
2. `GAP-005` — establish collision-safe append-only persistence across existing artifacts.
3. `GAP-006`, `GAP-007`, `GAP-008` — enforce training admission, review authority, and execution provenance before ReleasePolicy.
4. `GAP-001`, `GAP-009` — close operational distinction and real Host composition before L3.
5. `GAP-011`, `GAP-012` — reconcile semantic/documentation drift.

All retained gaps can be repaired inside existing Perception, Adapter, Host, Evaluation, Training, and Governance ownership. No Agent/Container/Traversal authority, dependency direction, or architecture invariant needs to reopen on current evidence.
