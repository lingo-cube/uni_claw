# PROJECT_LEADER_SCROLL_STABILITY_CONFIRMATION_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_SCROLL_STABILITY_CONFIRMATION_FIX — ensure the page has
> STOPPED after a scroll before any recognition / grounding / tap proceeds.
>
> **AuthorityDelta: NONE — ArchitectureDelta: ADDITIVE** (Agent acceptance
> seam only; no ownership/authority/contract change, no scenario knowledge, no
> ADB/XML correction, no fixed delays).

---

## 1. 问题表现

Current flow executed a tap with coordinates from a frame captured while the
page was still settling after a scroll (inertia / bounce-back): the target was
observed at position A in the screenshot but had moved to B by tap execution —
the tap hit the row below ("Safety & emergency" instead of "Location" on the
EBD real-device Settings list).

## 2. 证据确认

- OCR detection, coordinate transform, and candidate association verified
  CORRECT (rest-frame OCR-vs-settled offset ≈ 0; server remap math correct).
- The offset grows with scroll depth (rest ≈ 0 → +0.04 → +0.07 → +0.10-0.11) —
  the temporal signature of scroll motion, not a detection/transform defect.
- The acceptance path had NO stability criterion: `SettlePostScrollEvidenceQualityAsync`
  validates bounds validity only; a mid-settle frame was accepted as the
  decision basis (see `docs/decisions/observation-stability-contract-analysis.md`).

## 3. 修改内容

**Production** (`src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs`):

| change | detail |
|--------|--------|
| `ConfirmScrollStabilityAsync` (new) | BOUNDED re-observe after a scroll (first frame + up to 3 confirmation frames). Two consecutive observations with the SAME navigation-signature set AND every row's center-Y within `ScrollStabilityBoundsEpsilon` (0.02) prove the viewport STOPPED. Returns the LATEST confirmed frame as the decision basis. Same-Container sanity WITHOUT the container's continuity side effects (continuity/accept stay with the caller, exactly once). Budget exhausted → null → fail closed (no unstable frame used). |
| `IsViewportStable` / `NavigationRowCenters` (new) | signature-set equality + per-row center-Y tolerance; row identity is the occurrence signature (no coordinate memory). |
| exploration accept path (`ScrollForward`) | settle(quality) → **stability confirmation** → continuity → accept |
| revisit accept path (`ScrollBackward`) | same insertion; unconfirmed stability stops the revisit fail-closed |

**Test/fixture adaptations** (observation cadence: each scroll now contributes a
stability-confirmed frame instead of the post-scroll frame):

| file | change |
|------|--------|
| `Evidence/ScrollStabilityConfirmationTests.cs` (new) | settle-physics world: target observed A → B → C → C; asserts the dispatch uses ONLY the stable position (world-provided values, no fixed coordinates), never A/B; never-stable variant fails closed; Vision-only; no Settings/ADB; no fixed action counts. |
| `OpenWorldBranchAcceptanceProvenanceRepairTests.cs` | ACCEPT3/8/10 grounding references moved to the stability-confirmed frame sequences (scroll-1 → +1, scroll-2 → +2). |
| `OpenWorldPostExplorationCurrentRepairTests.cs` | CURRENT accepted-seq assertions → `seq=[2,4,6,8]`, `source-seq=8`. |
| `OpenWorldBoundedSourceRevisitTests.cs` | RVT212 now fail-closes at the stability check (which runs before continuity); assertion updated to the new closed message (outcome unchanged). |
| `OpenWorldPostActionSettleTests.cs` | SET910 epoch-seq assertion → `[2,4,6,8]`. |
| `ScrollArtifactEligibilityScenarioTests.cs` | ART12 textless script now enqueues TWO frames per scroll (post + stability-confirmed) so the genuine-UNKNOWN surface is present in an ACCEPTED frame. |

## 4. AuthorityDelta

**NONE** — Agent authority, DFS ownership, GoalEvidence, Traversal ownership,
Semantic boundary, Vision-first contract, ADB auxiliary-only contract unchanged.

## 5. ArchitectureDelta

**ADDITIVE** — a stability acceptance criterion inside the Agent's existing
exploration/revisit acceptance seam. No new state owner (loop-local), no new
cross-layer contract, not BREAKING. Fail-closed preserved (unconfirmed
stability never dispatches).

## 6. 测试结果

- New `ScrollStabilityConfirmationTests`: **2/2 PASS** (stable-frame dispatch /
  never-stable fail-closed / Vision-only / no Settings-ADB).
- Broad deterministic sweep (stability, revisit, OpenWorld, provenance,
  U2OpenWorld, settle, scroll-artifact, Capstone formal): **284/284 PASS**.
- Full regression: **1959 PASS / 1 FAIL / 1960 total** — the only failure is
  `ExternalBoundary_RealDevice` (see §7); Capstone real-device PASSES.

## 7. 真机结果

- **Capstone**: PASS standalone (42s) and inside the full suite — the extra
  stability observations do not disturb parent-return / revisit behavior.
- **ExternalBoundary**: the scroll-stability target is ACHIEVED — real-device
  evidence shows `scroll stability CONFIRMED` frames, the tap now lands on the
  **Location** row (container `Location:uselocation` entered), and the "App
  location permissions" tap **successfully transitions to
  com.android.permissioncontroller** (post-tap frame evidence). The remaining
  failure is a NEWLY-EXPOSED, INDEPENDENT issue (NOT scroll stability):
  `TryHandleExternalBoundaryAsync` judges the external foreground from the
  FIRST post-tap frame, which is still on the owned app during the transition —
  the ordinary branch dispatch has a bounded settle
  (`SettlePostActionObservationAsync`) but the external-boundary path does not.
  Reported for a separate scoped fix (add the same bounded settle to the
  external-boundary path); the EBD test stays red on that issue.

## 8. 剩余风险

- `ScrollStabilityBoundsEpsilon` (0.02) is a policy constant; it was exercised
  successfully on the real device (all EBD/Capstone stability confirmations
  passed without false positives). Devices with slow long-fling settling may
  exhaust the confirmation budget → fail closed (never dispatches on a
  knowingly-mid-settle frame) — the intended conservative behavior.
- The stability confirmation adds observations per scroll; the sequence-keyed
  scripted fixtures were re-keyed to the new cadence (event/settle-based where
  applicable), and any future cadence change must repeat that discipline.
- The external-boundary first-frame foreground check remains the open item
  (out of this task's scope, reported in §7).
