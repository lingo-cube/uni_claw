# Scroll Stability Confirmation

## Human Symptom

After a scroll action, the list is still visibly settling (items drift into
position), but the Runtime accepts the observation as stable and dispatches a
tap on coordinates that were computed from the mid-settle frame. The tap hits
the wrong row ("Safety & emergency" instead of "Location").

## Expected Reality

The system should confirm that the viewport has stopped moving before accepting
a scroll-produced observation as the decision frame for grounding and dispatch.
A mid-settle frame should be recognised as transient and either re-observed or
waited out.

## Observed Reality

The post-scroll observation was accepted immediately because the acceptance
path only checks:
- bounds-valid (yes, the element has bounds even while scrolling)
- same-Container continuity (yes, still the same page)
- exploration new-source (yes, a new area of the list)
- NORM4 ordered overlap at completeness

There is **no stability criterion**: no check that the screen stopped moving,
no re-observe to confirm the viewport settled. The step that wrongly promotes
a mid-settle frame to "stable" is the post-scroll acceptance path
(`SettlePostScrollEvidenceQualityAsync` → continuity → accept).

## Reality Gap

The `Observation` was physically captured mid-scroll (the list was still
settling), but the Runtime treated it as a stable decision frame. The element
bounds in the mid-settle frame differ from the post-settle layout — the tap
that was grounded on those bounds hits the row below the intended target.

## Evidence Reference

- Decision: `docs/decisions/observation-stability-contract-analysis-result.md`
  (full analysis — scroll-induced bounds drift, offset growth pattern,
  temporal signature of scroll motion, not a detection/transform defect)
- Decision: `docs/decisions/physical-scroll-container-semantic-traversal-graduation-decision.md`
  (scroll semantic mechanism graduation, fresh-Observation-after-every-scroll
  enforcement, stale-grounding rejection)
- Trace: post-scroll frame timeline shows bounds shifting across frames until
  the list physically settles
- Observation: decision-frame carries bounds that shift by 0.04–0.11 (screen
  fraction) from the post-settle layout; offset grows with scroll depth

## First Divergence Point

The `Observation` acceptance seam (`AcceptFreshObservation` in the post-scroll
path) — the acceptance policy has no stability criterion, so a mid-settle frame
is promoted to "decision frame" while the physical viewport is still in motion.
The Environment correctly provides raw frames; the stability judgment is an
Agent-owned acceptance policy that was missing.

## Owner

**Agent — observation-acceptance seam.** The Environment provides raw frames at
the correct timing; the judgment "is this frame stable enough to be the
decision frame" is an Agent-owned acceptance policy. Traversal and Semantic
Capability are not at fault.

## Minimal Change

Add a **bounded stability confirmation** to the post-scroll acceptance path:
after the first acceptance, immediately re-observe and compare the
navigation-signature set (or a bounds-stability metric). If the two
observations agree on the visible element set, accept as STABLE; if not,
repeat until the budget is exhausted (existing fail-closed semantics).

## Rejected Alternatives

- **Blame the Environment / Vision timing:** rejected — the Environment
  captures at the correct moment; the screenshot is accurate for that instant;
  the problem is that the instant is mid-settle, not that the capture is wrong.
- **Add a fixed delay after every scroll:** rejected — weakens real-time
  behaviour, impossible to tune for all devices, and masks the real problem
  (stability judgment, not raw timing).
- **Make Traversal own the stability check:** rejected — Traversal is the
  execution boundary; the decision to accept an observation as stable belongs
  to the Agent's exploration/acception policy.

## Engineering Lesson

**Observation acceptance must include a stability criterion, not just a
bounds-validity check.** A frame can be valid (bounds non-null, same-Container)
yet physically transient. For any action that produces screen motion (scroll,
transition), the acceptance path should confirm the screen has stabilised
before promoting the frame to the decision frame. This is an Agent-internal
policy — the Environment is not the owner.