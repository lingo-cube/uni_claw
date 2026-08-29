# Evidence Index — runtime-iterative-full-traversal-acceptance

| File | Content |
|---|---|
| `A-human-gate-and-baseline.md` | Human approval, artifact digests, owners, scope, pre-existing tree state, asset layout decision |
| `runtime-baseline-manifest.sha256` | SHA-256 manifest of all Runtime production files at implementation start (216 files) |
| `semantic-ir-records.md` | Development Semantic IR per task group (DesiredReality…SemanticResolution) |
| `G0-real-emulator-prerequisites.md` | Real emulator + real Settings anchor verification (pre-Stage-A) |
| `B-campaign-runner.md` | Campaign runner acceptance evidence (added on completion) |
| `C-knowledge-fixture.md` | Knowledge fixture acceptance evidence |
| `D-fixture-persistence.md` | Persistence acceptance evidence |
| `E-plandelta-recorder.md` | PlanDelta recorder acceptance evidence |
| `F-settings-binding.md` | Settings binding acceptance evidence |
| `G-stage-a-conservative-campaign/` | Stage A campaign rounds, run evidence, fixture v1 increment |
| `H-stage-b-online-adaptation/` | ≥3 adaptation rounds with PlanDelta provenance |
| `I-stage-c-persisted-reuse/` | Frozen fixture reuse campaign evidence |
| `J-phase26a-acceptance.md` | PHASE26A_ACCEPTANCE_RESULT |
| `K-stage-d-full-traversal/` | 2.6B full traversal evidence |
| `L-advisory-package.md` | Simulator-derived Advisory Knowledge Package |
| `M-full-regression.md` | Final regression numbers |
| `SOURCE-NORMALIZATION-ANCHOR-CONFIRMATION-DIAGNOSTIC-RESULT.md` | Post-`FRAME_LOCAL_COMPOSITION_VALIDITY_VETO_REPAIR_GATE` normalization blocker diagnosis (DIAGNOSIS ONLY / Phase 2.6 STOPPED) |
| `SOURCE-NORMALIZATION-ANCHOR-CONFIRMATION-REPAIR-RESULT.md` | `SOURCE_NORMALIZATION_ANCHOR_ADJACENT_CONFIRMATION_REPAIR_GATE` repair result: anchor-adjacent exact confirmation resolves without growing union; falsifiers fail-closed; Phase 2.6 STOPPED |
| `N-graduation-readiness.md` | Independent spec→test→evidence map + memory learning inputs |

WorkItem record: each worker's returned WorkResult is appended verbatim to the
corresponding letter file together with the leader's independent verification
(build + test re-run + `git status` purity check).
