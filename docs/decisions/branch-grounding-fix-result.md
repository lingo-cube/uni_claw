# PROJECT_LEADER_BRANCH_GROUNDING_FIX_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Goal: prevent OpenWorld DFS from dispatching a stale/unresolved branch after
> viewport changes — scope limited to branch grounding validation.

---

## 1. Problem Confirmed

The existing dispatch path (`Agent.OpenWorld` pending-branch selection) already
had strong grounding validation (identity safety → explicit
`RequiredBranchGrounding` → `SourceGroundingValidator.Validate` →
`ResolveCurrentVisibleElement` → authorization), but the final two checks ran
against **historical evidence**:

- **`ResolveCurrentVisibleElement(current, branchClassSignature)`** matched the
  CURRENT occurrence by structured signature only — a similar-appearance
  impostor (same text, different element) could satisfy the signature match
  without being the branch's true grounded source.
- **Authorization** (`CandidateAuthorizationEvaluator(source, sourceCandidate)`)
  was evaluated against the **historical discovery observation**, not the
  CURRENT frame — if the viewport change altered the element (text/type), the
  dispatch inherited stale authorization.

This is the "Viewport changes → Branch revisited → Dispatch without fresh
grounding" hole.

## 2. Fix (scope-limited, evidence-driven)

Added a **BRANCH GROUNDING BEFORE DISPATCH** gate inside the existing pending-
branch selection loop (no new loop, no new authority, no DFS change):

```text
ResolveCurrentVisibleElement(current, branchClassSignature)
        |
        +-- null (not visible)  -> continue (pending; existing revisit recovers)
        |
        v
FRESH GROUNDING GATE (new):
  (a) current occurrence resolves to the SAME logical source class via
      SourceGroundingValidator.TryResolveLogicalSource (explicit
      normalization-resolution check — rejects similar-appearance impostors);
  (b) authorization RE-EVALUATED on the CURRENT element via
      CandidateAuthorizationEvaluator(current, freshCandidate) — never
      inherited from history.
        |
        +-- (a) or (b) fails -> trace + continue (NO dispatch; branch stays
                                pending; existing bounded revisit mechanism
                                recovers the viewport; bounded retry only)
        |
        v
dispatch (unchanged)
```

Both checks are pure additions that can only REJECT a dispatch (tighten), never
relax one: existing graduated tests keep the same outcome because their current
element equals the historical source element (same signature, same
authorization).

## 3. Authority Verification

- **DFS ownership: UNCHANGED.** The gate runs inside the existing branch-
  selection loop; dispatch/ordering/ancestry/visited logic untouched.
- **Agent authority: UNCHANGED.** Agent remains sole semantic/dispatch authority.
- **Semantic boundary / Vision-first contract / GoalEvidence / Strategy loop:
  UNCHANGED.**
- **Grounding fail-closed: STRENGTHENED, never weakened** — the gate can only
  reject, via the existing `SourceGroundingValidator` and caller authorization
  criterion.
- **STOP conditions held:** no Settings logic, no child-index assumptions, no
  fixed scroll count, no coordinate memory as identity (the gate validates the
  CURRENT occurrence's logical-source resolution + authorization, never a saved
  coordinate/index), no new execution loop.

## 4. Tests

`tests/UniClaw.Runtime.Tests/Evidence/BranchGroundingBeforeDispatchTests.cs` — 5
deterministic proofs (all green):

| # | Test | Proves |
|---|---|---|
| 1 | `BranchDisappearsAfterScroll_NoDispatch` | a branch scrolled out of the current frame is never dispatched from stale grounding (no Tap) |
| 2 | `BranchReappearsAfterViewportRecovery_DispatchAllowed` | overlapping frames keep the branch's fresh grounding; dispatch happens |
| 3 | `SimilarAppearanceImpostor_Rejected` | two rows with the same text (signature match, different element) are not dispatched blindly |
| 4 | `VisionOnlyGrounding_NoAdb` | the whole run uses the in-memory world; no adb |
| 5 | `GenericTreeWorld_NoScenarioKnowledge` | rows are generic "Node NN"; zero Settings/WiFi/Android vocabulary |

## 5. Regression Result

| Suite | Result |
|---|---|
| Build `src/UniClaw.Runtime.sln` (`-p:NuGetAudit=false`) | **0 errors / 0 warnings** |
| `UniClaw.Runtime.Tests` | **1946 / 1948** (+5 new grounding tests; deterministic fully green) |
| Grounding/OpenWorld/AdaptiveScroll/Settings/BoundedCrossPage/VisionFirst/SourceProvenance (186) | 185 / 186 (only EBD real-device, pre-existing) |
| `Semantic.Tests` | **32 / 32** |
| `check-consistency.sh` | ALL PASS |
| `git diff --check` | clean |
| Scenario-string check on new production diff | clean |

Remaining 2 failures are the PRE-EXISTING real-device items (Capstone
real-emulator + ExternalBoundary real-device) — both are device/hardware
integration concerns unrelated to this grounding fix (see
`docs/decisions/adaptive-scroll-grounding-result.md` §6).

## 6. Remaining Blockers

1. Capstone real-emulator completion — pre-existing device integration issue
   (bounded-revisit depth + fixture return-button tap precision); independent
   of grounding.
2. ExternalBoundary real-device — pre-existing device issue (Settings layout /
   OCR ordering during scroll); independent of grounding.
3. No code-level blockers; deterministic matrix fully green.

## 7. Conclusion

The stale/unresolved branch dispatch hole is closed with a minimal, evidence-
driven grounding gate: before any pending branch dispatch, the CURRENT
observation's occurrence must resolve to the branch's logical source class and
the CURRENT element must re-authorize. Failures leave the branch pending and
delegate viewport recovery to the existing bounded revisit mechanism. DFS
ownership, Agent authority, the semantic boundary, Vision-first grounding, and
GoalEvidence are unchanged; grounding fail-closed is strictly strengthened.
