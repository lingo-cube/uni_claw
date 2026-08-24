# Scroll Execution Profile

## Human Symptom

On a real device, scrolling through a dense Settings list produces garbled OCR
results — text rows are unreadable, normalisation fails, and the Agent cannot
identify the target element. The same scroll works correctly in the emulator
but consistently fails on the physical device.

## Expected Reality

The scroll action should produce a clean, readable viewport regardless of
device type. The physical swipe velocity should be controlled so that the
screen content is not blurred during the scroll animation, and the observation
captured after the scroll should be usable for grounding and normalisation.

## Observed Reality

The adb `input swipe` command used a fixed 300ms duration for all scrolls
regardless of distance. On a dense Settings list with many small text rows,
this produced a swipe velocity of ~1024–2048 px/s (depending on the device
resolution and scroll distance). At this velocity, the list content was
visually blurred during the scroll animation, and the post-scroll frame
contained garbled OCR text that could not be normalised.

## Reality Gap

The scroll duration was a hidden adb default (300ms), not an explicit
parameter. The Agent and Traversal had no control over the physical execution
velocity — the same fixed duration was applied to a 10% step and a 100% step,
producing very different velocities. The physical execution semantics (how
fast the swipe moves) were not matched to the semantic intent (how far the
viewport should scroll).

## Evidence Reference

- Decision: `docs/decisions/scroll-execution-profile-implementation-result.md`
  (full implementation — velocity-capped duration derivation, Adapter-layer
  only, real-device comparison: OCR normalisation pass rate 0% → 67%)
- Decision: `docs/decisions/device-action-execution-semantics-analysis-result.md`
  (execution semantics analysis: duration hidden in adb default, no per-action
  timing model, ownership analysis)
- Trace: scroll execution timeline showing fixed 300ms duration for all
  distances; post-scroll frames with garbled OCR
- Test: `ScrollExecutionProfileTests` (10/10 PASS — velocity never exceeds cap,
  different StepFractions produce different durations, command reaches adb)

## First Divergence Point

The `DeviceActionTranslator` — the translation from semantic `ScrollForward`
(StepFraction) to physical `AdbOperation.Swipe` (distance, duration). The
swipe duration was not an explicit parameter; it was left as the adb default
(300ms). The translator is the correct seam for execution-parameter derivation,
but the duration was not derived — it was a hidden default.

## Owner

**Adapter layer (`DeviceActionTranslator`)** — the translation from semantic
action to physical device operation. The Agent (semantic) and Traversal
(execution) are not at fault; the gap is in the physical execution parameter
derivation, which is an Adapter-layer concern.

## Minimal Change

Add a **Scroll Execution Profile** inside the `DeviceActionTranslator`:
- `ComputeScrollDurationMs(distance) = max(200ms, ⌈distance / 0.9px/ms⌉)`
  (velocity cap ≈ 900 px/s; 200ms floor keeps tiny steps reliable)
- The derived duration is passed to `AdbOperation.Swipe` as an explicit
  parameter
- Non-scroll actions (Tap/Back/Launch) are untouched; the default null
  duration keeps byte-compatibility with existing call sites

DeviceAction semantic model unchanged (still only `StepFraction`); no physical
fields leaked to the Agent or Traversal.

## Rejected Alternatives

- **Make the Agent control duration:** rejected — would leak physical execution
  details into the semantic layer; the Agent should not know about swipe
  velocity or duration. The DeviceAction is correctly abstract.
- **Fix the OCR model instead:** rejected — the OCR model works correctly on
  stable frames; the problem is that the frames are blurred by excessive swipe
  velocity. Improving OCR to handle blur would mask the physical root cause and
  increase model complexity.
- **Add a fixed delay after every scroll:** rejected — would not fix the
  velocity problem; the blur is caused by the swipe speed, not by insufficient
  settling time. The scroll execution profile addresses the root cause.

## Engineering Lesson

**A hidden default (adb's 300ms swipe duration) is an architecture gap, not a
configuration detail.** When the semantic meaning of an action (scroll distance)
and the physical execution parameter (swipe velocity) are coupled, the
translation layer must explicitly derive the physical parameter from the
semantic intent. A hidden default that works for one device (emulator) may
fail on another (real device) because the relationship between distance and
duration is device-dependent. The Adapter layer is the correct seam for this
derivation — not the Agent, the Traversal, or the semantic model.