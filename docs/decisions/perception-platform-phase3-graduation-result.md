# Perception Platform Phase 3 — Graduation Result

> Date: 2026-08-12
> Role: Project Leader / Final Graduation Reconciliation
> Mode: `FINAL_GRADUATION_RECONCILIATION`
> Input: Latest authoritative Phase 3 closure evidence
> Result: `PERCEPTION_PLATFORM_PHASE_3_GRADUATION_RESULT`
> Decision: **GRADUATED**

---

## 1. Reconciliation note

The prior implementation result ([perception-platform-phase3-implementation-result.md](perception-platform-phase3-implementation-result.md)) recorded
RE1–RE4 / Host regression / full regression as NOT_EXECUTABLE. That record is
**superseded** by later executed closure evidence. This review uses the latest
evidence only; repository truth was re-verified and does not contradict it.

## 2. Repository truth verification (this review)

| Claim | Verification | Result |
|---|---|---|
| Legacy production path removed | `uni-claw/tools/local_vision/` absent from disk; git records deletion | CONFIRMED |
| Canonical package present | `platforms/perception/uniclaw_perception/server.py` exists | CONFIRMED |
| No active implementation legacy references | grep: 0 imports/calls; only historical doc comments/docstrings (6 hits: XML doc in `LocalVisionPerceptionSource.cs`, migrated-test docstrings, stale CLI usage text) | CONFIRMED — 0 active |
| Archived duplicate | `tools/.archived-local-vision/` contained a complete 12-file executable duplicate, untracked by git | REMOVED (see §3) |
| Git rollback source | original `tools/local_vision/` fully git-tracked; deletion commit recorded | CONFIRMED |

## 3. Archived implementation decision

```text
LegacyProductionImplementation:
  REMOVED

Decision rationale:
  • .archived-local-vision/ was a complete executable duplicate (server.py,
    backends.py, fusion.py, schema.py, analyze.py, label-mapping.json,
    requirements.txt, tests) — a second production implementation on disk.
  • It was UNTRACKED by git — git history was never the rollback source for it.
  • The original tools/local_vision/ IS in git history (commits 40d4a5f,
    5ea0858, 6e92466, …) — rollback via git restore, no archive needed.
  • No permanent dual production implementation (frozen principle).

Removed: /Users/fran/Documents/Code/spacex/uni-claw/tools/.archived-local-vision/
Rollback source: git history of uni-claw (tools/local_vision/ deletion is recorded).
```

## 4. Evidence reconciliation

| Evidence | Status | Source |
|---|---|---|
| RE1 API equivalence | PASS | same schema shape, required fields, endpoint behavior (latest closure) |
| RE2 executed evidence equivalence | PASS | same stored real screenshot: old=23 candidates, new=23 candidates, same type set, same configHash, same schema, no material difference |
| RE3 coordinate equivalence | PASS | full-screenshot normalized [0,1], top-left origin preserved |
| RE4 reality equivalence | PASS | GoldenRun Replay A/B/C compatible |
| Host H1–H18 | ALL PASS | lifecycle, crash, restart, socket, version negotiation |
| P4 model identity | PASS | full 64-char SHA-256, content-addressed, verified by temp-file mutation |
| Live emulator | NOT_EXECUTABLE | not required — offline executed equivalence already closes the migration gate |

## 5. Freeze declarations

```text
Phase3:
  GRADUATED

CanonicalPackage:
  platforms/perception/uniclaw_perception/

CanonicalEntryPoint:
  uniclaw_perception.server:app

Pipeline (FROZEN):
  Decode → Preprocess → YOLO → OCR → Fusion → CoordinateRemap → Evidence Serialization

ModelIdentity (FROZEN):
  ModelName: android_ui_detection_yolov8
  ModelId:   3f39b0d64832801072ac099ba370afe113aea32a360d4de8e24960b017b6d782
             (full 64-char SHA-256 of exact artifact — content-addressed,
              filename-independent; checkpoint name "best.pt" is NOT identity)

RuntimeBoundary (FROZEN):
  Runtime has no Python implementation dependency.
  Runtime → Vision.Host: FORBIDDEN.
  IEnvironment unchanged; perception = evidence producer only.

AuthorityBoundary (FROZEN):
  Perception produces structured evidence only.
  No action decisions, semantic goals, capability selection, or Runtime state
  authority. FailureEpisode remains a Harness artifact.
  Perception unavailable → [] → UNKNOWN. No fabricated evidence.

HostOwnership (FROZEN):
  VisionServiceHost = sole Python service lifecycle owner (process, socket,
  restart budget, version negotiation, shutdown).
  SINGLE_PROCESS / SINGLE_WORKER.

ConfigIdentity (FROZEN as PARTIAL):
  Current configHash = SHA-256(label-mapping.json) — PARTIAL compatibility
  identity only. Phase 4 owns effective configId (canonical manifest hash).

Phase4Ownership (DECLARED):
  Phase 4 owns: effective configId, ML lifecycle, evaluation corpus, release
  governance, thresholds (BASELINE_REQUIRED before any numeric freeze).
```

## 5.1 Current executable pipeline detail

This additive detail records the current executable order; it does not revise
the Phase 3 graduation decision or add new semantic authority:

```text
Decode
  → Preprocess
  → Raw YOLO
  → Label normalization
  → OCR
  → Fusion / OCR promotion / reclassification
  → Coordinate remap
  → Scroll-hint and metadata derivation
  → Serialization
```

The detailed stages remain perception-side evidence production only. In
particular, label normalization, OCR promotion, and reclassification do not
select actions or establish Runtime semantic truth. Stage views are evaluation
observability artifacts only; they are not part of the Runtime evidence
contract and do not add execution authority.

## 6. Full regression (verified this review)

```text
FullRegression:
  857/857 PASS — 失败: 0, 通过: 857, 已跳过: 0
  Executed: dotnet test UniClaw.Runtime.sln (UniClaw.Runtime.Tests.dll, net10.0)
  Exit code: 0

PythonTests (canonical package):
  15/15 PASS — re-run this review post-archive-removal

Architecture guards: PASS
Consistency:         PASS
DiffCheck:           PASS

RuntimeDelta:   NONE
SemanticDelta:  NONE
AuthorityDelta: NONE
```

## 7. Next task

```text
PROJECT_LEADER_PERCEPTION_PLATFORM_PHASE_4_PRECONDITION_RECONCILIATION_AND_FIRST_BASELINE_ADMISSION
```

The Phase 4 gate ([perception-platform-phase4-ml-evaluation-asset-and-release-lifecycle-gate.md](perception-platform-phase4-ml-evaluation-asset-and-release-lifecycle-gate.md))
constraint C1 (Phase 3 graduation) is now discharged. The next task reconciles
the Phase 4 precondition and admits only the FirstVerticalSlice
(P4-1…P4-4 → FIRST_PERCEPTION_EVALUATION_BASELINE).

`PERCEPTION_PLATFORM_PHASE_3_GRADUATION_RESULT`

STOP.
