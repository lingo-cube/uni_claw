# Canonical Cases（取自 V1 evidence-driven-debugging §6，语义未改）

### Case 1: Wrong Tap

- **Expected:** Clicking the target button navigates to the target page.
- **Observed:** The click navigated to a different page.
- **Reality Gap:** The action was dispatched, but the target element was not
  the intended one.
- **Evidence needed:** action trace, observation frame, bounds freshness.
- **First Divergence:** Confirm whether the `Observation` used for the click
  still represented the current page.
- **Owner:** Observation / Grounding.

### Case 2: External Page Not Detected

- **Expected:** Clicking the permission entry navigates to the external
  permission page.
- **Observed:** The permission page appeared, but the Runtime judged it had
  not.
- **Reality Gap:** The real device state and the Runtime state disagreed.
- **Evidence needed:** external transition trace, foreground timeline,
  observation frames.
- **First Divergence:** `Foreground` state detection did not reflect the real
  external state.
- **Owner:** Environment detection.

### Case 3: OCR Failure After Scroll

- **Expected:** After scrolling, the system continues to recognise list
  content.
- **Observed:** After scrolling, the list could not be normalised.
- **Reality Gap:** The `Observation` produced by the action did not meet the
  requirements for subsequent understanding.
- **Evidence needed:** scroll trace, frame timeline, OCR result.
- **First Divergence:** Determine whether the problem originated from scroll
  motion, observation timing, or vision capability.
- **Owner:** Determined by evidence (scroll / Observation timing / Vision).
