# External Foreground Detection

## Human Symptom

The EBD real-device test clicks "App location permissions". The external page
(com.android.permissioncontroller) appears and stays visible for 6 consecutive
frames — all settle conditions (candidate + confirmation) are met. Yet the
test still fails: the foreground is never recognised as external, the settle
budget exhausts, and the system declares "did not settle into an external
foreground".

## Expected Reality

The auxiliary foreground detection (used in the test environment to determine
the current foreground package from a uiautomator XML dump) should correctly
parse the XML structure regardless of attribute order. The `package` attribute
should be found wherever it appears in the XML node — first, middle, or last
attribute.

## Observed Reality

The `DeriveForegroundFromXml` helper used the regex
`"<node[^>]*?package=\"([^\"]*)\">"` — which requires the `package="..."` to be
the **last attribute** before the closing `>`. Real uiautomator XML dumps have
the `package` attribute in the middle of the node (followed by
`content-desc`, `bounds`, etc.), so the regex failed to match on every frame.
The function returned `null`, and the caller fell back to
`obs.ForegroundApplication` — which was the stale owned foreground (`settings`).
The settle loop never saw an external foreground, budget exhausted, and the
test failed closed.

## Reality Gap

The XML dump was correct and clearly showed `com.android.permissioncontroller`
in the `package` attribute of the root node — but the detection regex was too
fragile to extract it. The test environment believed the foreground was still
`settings` (the stale fallback value), while the physical device was already
showing the permission page.

## Evidence Reference

- Decision: `docs/decisions/external-foreground-detection-fix-result.md`
  (full implementation — new `UiAutomatorXml.ForegroundPackage` parser with
  robust regex + 8 dedicated unit tests)
- Decision: `docs/decisions/external-boundary-transition-settle-analysis-result.md`
  (the settle analysis that was previously blocked by this detection failure)
- Decision: `docs/decisions/external-boundary-transition-settle-fix-result.md`
  (the settle fix that was unblocked after this detection fix landed)
- Trace: XML frame 21-26 showing `com.android.permissioncontroller` (6 frames
  stable external); AllStructured frames 1-27 all showing `fg=settings`
  (contradiction between XML truth and detection result)
- Test: `ForegroundDetectionTests` (8/8 PASS — covering package-first-attr,
  package-middle-attr, package-last-attr, no-package, empty XML, external
  package, realistic Settings-root dump)

## First Divergence Point

The `DeriveForegroundFromXml` function — the regex was written to match a
specific attribute ordering (package as the last attribute before `>`), not the
general case (package anywhere in the node). The regex was correct for the
specific frame shape that was used during initial development, but real-world
uiautomator dumps from the real device use a different attribute order.

## Owner

**Test harness (uiautomator auxiliary parsing)** — this is a test-only
auxiliary parser used in the test environment for device state detection. The
Runtime (`src/UniClaw.Runtime/`) is not involved. The external boundary
handler, settle budget, transition contract, and Agent authority are all
correct independently of this detection bug.

## Minimal Change

Replace the fragile regex with a robust parser:
- New: `UiAutomatorXml.ForegroundPackage` — a shared internal parser that
  matches `package="..."` at any position within the node opening tag
  (using `"<node\b[^>]*?\spackage=\"([^\"]*)\""` — `\s` before `package`
  ensures it is a real attribute, not a substring of another value)
- The caller fallback is unchanged: `ForegroundPackage(xml) ??
  obs.ForegroundApplication`
- 8 dedicated unit tests covering all edge cases (attribute order, no package,
  empty XML, external package, realistic Settings-root dump)

## Rejected Alternatives

- **Fix the uiautomator output format:** rejected — uiautomator is a system
  tool; its output format is not under our control. The parser must handle
  the format as it is, not as we wish it to be.
- **Use ADB as the primary detection source:** rejected — would violate the
  Vision-first contract (ADB is auxiliary only). The uiautomator XML is an
  auxiliary analysis tool for the test environment, not a primary authority.
- **Remove the fallback to `obs.ForegroundApplication`:** rejected — the
  fallback is correct when the parser returns `null` (e.g., non-XML frames);
  the problem was that the parser was returning `null` for valid XML.

## Engineering Lesson

**A regex that depends on attribute ordering is fragile.** When parsing a
machine-generated format (uiautomator XML dump) that is not under your control,
the parser must be robust to attribute ordering. A regex that matches
`package="..."` at any position within the opening tag (with `\s` before the
attribute name to avoid false matches) is the minimum reliable pattern. More
importantly: **when the detection layer and the raw evidence disagree, the
raw evidence is the truth.** The XML frames clearly showed the external page;
the contradictory detection result was a parser bug, not a real ambiguity.