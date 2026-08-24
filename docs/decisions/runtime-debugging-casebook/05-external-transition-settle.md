# External Transition Settle

## Human Symptom

Clicking "App location permissions" navigates to the external permission page
(com.android.permissioncontroller). The page is visible on screen for several
seconds, but the Runtime judges the transition as failed — "did not produce an
external foreground" — and aborts the flow.

## Expected Reality

When the user taps an element that triggers a cross-application navigation, the
system should recognise that the external page has appeared once the transition
has settled. The judgment should be based on the settled state (the external
page open and stable), not on the first post-action frame (which is still in
the original app while the transition is in progress).

## Observed Reality

The `TryHandleExternalBoundaryAsync` handler checked only the **first
post-action frame** for external foreground detection. The tap was successful
and the external activity started, but the first frame after the tap was still
in the original app (the transition had not completed yet). The handler
immediately declared failure without waiting for the external page to appear.

## Reality Gap

The external transition was a cross-application cold start that takes 3–4
observation frames to complete (from tap to permissioncontroller being visible
and stable). The entering judgment used the first frame (which is always owned
during the transition) — no settle was applied. The returning judgment
(original app → external page) already had a bounded settle; the entering
judgment (external → owned app) was missing the same protection.

## Evidence Reference

- Decision: `docs/decisions/external-boundary-transition-settle-analysis-result.md`
  (full analysis — root cause classification, evidence timeline, ownership
  decision)
- Decision: `docs/decisions/external-boundary-transition-settle-fix-result.md`
  (implementation — `SettleExternalTransitionAsync` bounded settle added to
  the entering judgment)
- Trace: external transition frame timeline showing `ebd_obs_22` =
  `com.android.permissioncontroller` (the external page appears 3 frames after
  the tap); the first post-action frame (`ebd_obs_20`) is still in settings
- Test: `EBD19_ExternalTransitionDelayed_SettlesAndRecognized` (delayed
  transition recognised) and `EBD20_ExternalTransitionNeverAppears_FailsClosed`
  (never appears → fail closed)

## First Divergence Point

The entering judgment in `TryHandleExternalBoundaryAsync` — the handler read
`firstPostAction` (the first observation after the action) and immediately
decided whether the foreground was external. Since the transition is not
instantaneous, the first frame is always in the owned app. The handler should
have waited for a bounded confirm sequence (candidate + confirmation) before
declaring success or failure.

## Owner

**Agent — external transition handler** (`TryHandleExternalBoundaryAsync`).
The Environment foreground detection is correct (subsequent frames correctly
identify the external package). The action is successful. The settlement
judgment is an Agent-owned policy.

## Minimal Change

Add a **bounded settle** to the entering judgment of
`TryHandleExternalBoundaryAsync`:
1. A CANDIDATE frame where the foreground has left the owned application.
2. A CONFIRMATION frame with the same external foreground (stable identity).
3. Budget: `MaxExternalTransitionObservations = 6` (cross-app cold start is
   slower; real-device evidence shows 3–4 frames).
4. Budget exhausted → fail closed (no assumed success).

The returning judgment (SystemBack → owned app) already had a bounded settle
and was not modified.

## Rejected Alternatives

- **Increase the observation cadence:** rejected — the cadence is correct; the
  external page appears at the expected rate. The problem is the *judgment
  timing*, not the capture rate.
- **Make Environment detect transitions:** rejected — the Environment
  correctly reports foreground per frame; the decision "has the transition
  settled?" is an Agent-owned policy, not an Environment contract.
- **Remove the entering check entirely:** rejected — would accept external
  pages that never actually appear (false positive). The bounded settle is
  needed to distinguish "transition still in progress" from "transition never
  happened".

## Engineering Lesson

**Any boundary transition judgment (entering OR returning) needs a settle
confirmation, not just the first frame.** A cross-application cold start is
not instantaneous — the first post-action frame is always in the original app.
The entering judgment must use the same bounded settle pattern as the returning
judgment: a candidate frame (foreground leaves owned) followed by a
confirmation frame (same external foreground stable). This is an Agent-internal
policy in the external transition handler; the Environment is not the owner.