# Perception Phase 3 / Phase 4 Semantic Repair Targeted Re-audit Result

> Date: 2026-08-13  
> Role: fresh independent Sol targeted falsification audit  
> Contract: `SOL_PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_TARGETED_REAUDIT`  
> Input implementation result: `PERCEPTION_PHASE3_PHASE4_SEMANTIC_AUDIT_REPAIR_IMPLEMENTATION_RESULT`  
> Result: `PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_TARGETED_REAUDIT_RESULT`  
> Status: **REPAIR_INCOMPLETE**

## 1. Canonical decision

The repair implementation does not close the prior semantic audit. The targeted
re-audit reproduced surviving production-boundary attacks for `GAP-002`,
`GAP-004`, `GAP-006`, `GAP-007`, and `GAP-008`; `GAP-009` remains unproven at
the required real canonical Host composition boundary. These are six remaining
S1 roots. No S0, architecture-reopen pressure, Runtime semantic delta, or
release-authority purchase was found.

Implementation tests passing is not semantic closure. The named tests omit or
weaken the surviving attacks described below.

```text
Status: REPAIR_INCOMPLETE
AllS1Closed: NO
RemainingS1: GAP-002, GAP-004, GAP-006, GAP-007, GAP-008, GAP-009
SemanticClosureDeclarable: NO
Phase3Phase4GraduationDeclarable: NO
ArchitectureReopenRequired: NO
```

## 2. Independent attack replay

| Gap | Targeted disposition | Independent result |
|---|---|---|
| GAP-001 | CLOSED, E3 operational distinction | Adapter preserves semantic `[]` while emitting `OK_EMPTY`, infrastructure, schema, malformed-response, invalid-geometry, and timeout classes; caller cancellation is rethrown. Focused .NET falsifiers passed. |
| GAP-002 | **OPEN, S1, BYPASSABLE** | `remap_coords` validates and removes invalid `candidates` only. The original out-of-range payload remains successfully present in `yolo` and `ocr`; the Gate required the post-remap boundary to enforce candidates, YOLO, and OCR. The replay retained `x1=-0.2`, `x2=2.4`, `y2=2.3` in both lists. `COORD-07` does not attack these production response paths. |
| GAP-003 | CLOSED, E4 | Manifest internal identity mismatch is rejected. L2 reads source bytes once, verifies that buffer, and decodes/executes the same buffer. A wrong claimed AssetId failed before pipeline load; the path-replacement falsifier passed. |
| GAP-004 | **OPEN, S1, BYPASSABLE** | The original detached `compute_task_metrics(candidates_A, ground_truth_B)` call still returns `SCORED`. A typed scoring context exists, but it accepts detached request/deployment values rather than a Run request/result object, and canonical `BaselineReport` persistence still accepts caller-supplied `quality_scorecard` dictionaries. Therefore detached metric output can still cross a canonical quality-artifact boundary. Checking that `save_result` has no `TaskMetricResult` parameter does not close alternate Baseline persistence. |
| GAP-005 | CLOSED, E3 with collision/concurrency falsification | The shared primitive uses canonical bytes, an exclusive hard-link publication step, byte-identical idempotence, and collision refusal. Enumerated canonical writers use it; baseline overwrite was removed; content-addressed model bytes refuse collision. Direct replay preserved the original bytes. |
| GAP-006 | **OPEN, S1, BYPASSABLE** | `validate_training_admission` rejects a protected AssetId and cross-split known CaptureGroup when explicitly called. Canonical training, however, creates a receipt with an empty protected set, discards the receipt, and neither passes nor records it at the execution boundary. `execute_ultralytics_training` requires no admitted dataset/receipt. Thus an old receipt cannot masquerade as another snapshot, but canonical execution is not bound to any receipt at all. |
| GAP-007 | **OPEN, S1, BYPASSABLE** | `create_annotation(... ACCEPTED)` is blocked, but the public dataclass/deserializer still accepts a directly constructed `MODEL_ASSISTED + ACCEPTED` record with invented non-empty provenance. `is_accepted_training_truth` checks only presence, not predecessor/event validity. Dataset admission does not load or validate referenced annotations; the forged Annotation ID was admitted successfully. |
| GAP-008 | **OPEN, S1, BYPASSABLE** | The intended mini path derives the Ultralytics arguments from `TrainingConfig`, but the canonical execution seam accepts an arbitrarily constructed `ResolvedTrainingInvocation`. `TrainingRun` independently accepts any `trainingConfigId`, invocation arguments, and invocation hash without loading the config or checking congruence. Replay executed `epochs=999` and recorded it under an unrelated claimed config ID. |
| GAP-009 | **OPEN, S1, P4-34 UNPROVEN** | `CanonicalVisionHostFactory` snapshots all four receipt axes and the Host comparison predicate fails closed. However, `VisionHostConfig` and `VisionServiceHost` constructors remain public optional-identity seams. `HOST-08` scans only files inside `src/UniClaw.Vision.Host` and excludes both files containing the direct construction, so it cannot forbid another production project from bypassing the factory. No test starts a factory-created Host against the real CURRENT ACTIVE `/version` path and then exercises restart with the captured expectation. Python runtime-snapshot tests and Host predicate tests are separate proofs, not the required production composition proof. |
| GAP-010 | CLOSED, E4 for the original attack | New execution uses immutable request plus terminal result, old `EvaluationRun` is loader-only, infrastructure has terminal precedence, and write-once result persistence prevents terminal replacement. The original pre-created `COMPLETED` run attack no longer exists. |
| GAP-011 | CLOSED | The Phase 3 graduation record additively names raw YOLO, label normalization, OCR promotion/reclassification, remap, metadata/scroll hints, serialization, and evaluation-only stage views. |
| GAP-012 | CLOSED | Current Adapter XML and benchmark help no longer reference `tools/local_vision`; canonical package/entry point text is present. |

## 3. Prior BYPASSABLE rows and P4-34

| Prior row | Current result |
|---|---|
| P3-11 | **BYPASSABLE** — invalid YOLO/OCR response geometry survives |
| P4-01 | EFFECTIVE, E4 |
| P4-04 | **BYPASSABLE** — detached metric result remains scoreable and can feed Baseline input |
| P4-05 | **BYPASSABLE** — detached metric defaults remain available on the same path |
| P4-14 | EFFECTIVE for new Run request/result and write-once history |
| P4-15 | EFFECTIVE, E3 write-once DatasetVersion history |
| P4-17 | **BYPASSABLE** — training execution does not require the admission receipt |
| P4-18 | **BYPASSABLE** — forged accepted annotation is not rejected by admission |
| P4-19 | **BYPASSABLE** — execution/record congruence remains caller discipline |
| P4-21 | EFFECTIVE, E3 terminal TrainingRun history |
| P4-23 | EFFECTIVE, E3 metadata and model-byte collision refusal |
| P4-37 | EFFECTIVE, E3 legacy history remains read-only through canonical writers |
| P4-34 | **UNPROVEN** — mechanism proven; mandatory real production composition not proven |

Therefore six of the twelve prior BYPASSABLE rows remain BYPASSABLE and P4-34
cannot be raised to E4.

## 4. Scope and delta audit

The implementation stayed inside the purchased Perception, Evaluation,
Training, Governance, Adapter, Host, test, and documentation areas. No Agent,
Container, Traversal, Environment decision logic, Runtime semantic contract,
promotion, activation, rollback, ReleasePolicy, EvaluationProfile, registry,
or automatic retraining was added.

```text
RuntimeDelta: NONE
SemanticDelta: NONE
AuthorityDelta: NONE
OwnershipDelta: NONE
DependencyDelta: NONE
ReleaseAuthorityIntroduced: NO
ArchitectureReopenRequired: NO
ImplementationScopeExceeded: NO
```

## 5. Fresh execution evidence

```text
FocusedPythonTargeted:
  74/74 PASS
  Includes geometry, write-once, asset, metric/run, training, and real Python
  runtime-snapshot/restart tests.

FocusedDotNetDiagnosticsAndIdentity:
  15/15 PASS

ConsistencyC1_C10: ALL PASS
DiffCheck: PASS

HistoricalArtifacts:
  WAIVED_BY_HUMAN_NOT_EXECUTED
```

The historical JSON comparison was explicitly waived by the Human. It was not
executed, is not represented as PASS, and does not block this decision.

The full .NET suite, full Python package suites, and a new standalone fresh-L2
receipt were not rerun in this targeted audit after executable surviving S1
counterexamples established `REPAIR_INCOMPLETE`. Their earlier implementation
claims are not adopted as fresh audit evidence.

## 6. Required correction boundary

The remaining work fits the existing architecture and original purchase:

1. enforce geometry rejection on serialized `candidates`, `yolo`, and `ocr`;
2. make canonical quality persistence consume provenance-bound scoring evidence,
   not arbitrary scorecard dictionaries or detached request/deployment claims;
3. make the training execution boundary require and record the exact
   snapshot-bound admission receipt;
4. validate referenced accepted Annotation records and their real predecessor /
   review-event chain at admission;
5. make the training execution seam derive from the actual TrainingConfig and
   make TrainingRun congruence mechanically verified rather than declarative;
6. close Host reachability across all production projects and execute a real
   factory-created CURRENT ACTIVE startup, mismatch, and restart proof.

No new subsystem, policy, identity authority, or Runtime dependency is needed.

## 7. Result contract

```text
PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_TARGETED_REAUDIT_RESULT

Status: REPAIR_INCOMPLETE
AllS1Closed: NO
RemainingS1Count: 6
RemainingS1:
  GAP-002
  GAP-004
  GAP-006
  GAP-007
  GAP-008
  GAP-009

PreviousBypassableRules:
  EFFECTIVE: P4-01, P4-14, P4-15, P4-21, P4-23, P4-37
  BYPASSABLE: P3-11, P4-04, P4-05, P4-17, P4-18, P4-19

PreviousUnprovenP4_34: UNPROVEN
HistoricalArtifacts: WAIVED_BY_HUMAN_NOT_EXECUTED
SemanticClosureDeclarable: NO
Phase3Phase4GraduationDeclarable: NO
ArchitectureReopenRequired: NO

NextTask:
  PROJECT_LEADER_PERCEPTION_PHASE3_PHASE4_SEMANTIC_REPAIR_CORRECTION_GATE
```

STOP.
