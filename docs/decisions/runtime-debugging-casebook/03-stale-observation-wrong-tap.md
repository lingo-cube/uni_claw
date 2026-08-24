# Stale Observation / Wrong Tap

## Human Symptom

The Agent tapped a target button, but the tap landed on the wrong element
("Safety & emergency" instead of "Location"). The action was dispatched
successfully but the result was semantically wrong — the wrong page opened.

## Expected Reality

The device action (`Tap`) should be grounded on an `Observation` that still
represents the current physical screen. The element bounds used for the tap
coordinates must be fresh — reflecting the layout at the moment the tap is
executed, not a prior frame that has since become stale.

## Observed Reality

The `Observation` used for grounding and dispatch was a mid-settle frame from
just after a scroll — the list was still physically settling. The element
bounds captured in that frame were correct for that instant, but by the time
the tap was executed the list had settled to a different layout and the
computed coordinates hit the row below the target.

## Reality Gap

The element bounds were **valid but stale** — valid in the sense of being
non-null and structurally correct, but stale because the physical screen state
had changed between the observation capture and the action execution. The
acceptance seam had no way to distinguish "bounds are non-null" from "bounds
still represent the current layout".

## Evidence Reference

- Decision: `docs/decisions/observation-stability-contract-analysis-result.md`
  (full analysis — scroll-induced bounds drift, temporal signature of scroll
  motion, offset growth pattern)
- Decision: `docs/decisions/device-action-execution-semantics-analysis-result.md`
  (execution timing gaps: fixed 300ms post-action delay for all action types,
  no per-action-type timing model)
- Trace: post-scroll frame timeline showing bounds shifting 0.04–0.11 screen
  fraction between the decision frame and the post-settle frame
- Observation: decision-frame bounds vs post-settle frame bounds — the offset
  pattern is consistent with scroll-settle motion, not a detection/transform
  defect (OCR and coordinate transforms verified correct)

## First Divergence Point

The `Observation` acceptance seam — the decision to promote a mid-settle frame
to "current decision frame" without a stability confirmation. The `Tap` action
itself is correct (it dispatches to the coordinates it was given); the problem
is that the coordinates were grounded on a stale frame.

## Owner

**Agent — observation-acceptance seam.** The Environment captures frames at the
correct timing; the Traversal dispatches the action correctly; the Semantic
Capability grounds correctly. The missing stability check is an Agent-owned
acceptance policy.

## Minimal Change

Add a **bounded stability confirmation** to the post-scroll acceptance path:
re-observe and verify that the navigation-signature set (or bounds-stability
metric) is stable before promoting the frame to the decision frame. This
ensures that the `Observation` used for grounding and dispatch reflects the
settled screen state, not a transient mid-settle state.

## Rejected Alternatives

- **Blame the Environment / scroll timing:** rejected — the Environment
  captures at the correct moment; the scroll is a physical motion that takes
  time to settle; the Environment cannot be expected to know when the frame
  will be used for decision-making.
- **Add a fixed delay before every tap:** rejected — masks the real problem
  (stability judgment) with a fragile timing heuristic; different devices and
  scroll distances need different delays.
- **Make Traversal re-accept the observation before dispatch:** rejected —
  Traversal executes actions; it does not own the acceptance policy. The
  acceptance seam is the correct owner.

## Engineering Lesson

**A frame can be valid (bounds non-null) yet stale (no longer reflects the
physical screen).** The acceptance policy must distinguish between "bounds are
valid" and "bounds are stable". For any action type that can produce screen
motion, the pipeline should confirm that the frame used for grounding is
stable before dispatching. This is an Agent-internal acceptance policy, not
an Environment or Traversal change.