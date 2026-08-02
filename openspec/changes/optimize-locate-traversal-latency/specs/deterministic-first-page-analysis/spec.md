## ADDED Requirements

### Requirement: Page-analysis cache survives device actions when the hierarchy fingerprint is unchanged

The page-analysis cache SHALL retain its cached analysis across device actions (tap, swipe, back, input) as long as the UIAutomator hierarchy fingerprint remains unchanged. On a cache hit, the analyzer SHALL NOT call the remote vision model.

#### Scenario: Tap on same page hits cache

- **WHEN** a step executes an action that does not change the hierarchy fingerprint
- **THEN** the next page analysis returns the cached result without a remote model call

#### Scenario: Fingerprint change invalidates cache

- **WHEN** an action changes the hierarchy fingerprint
- **THEN** the next page analysis performs a fresh analysis and updates the cache

### Requirement: Post-scroll analysis is skipped when the swipe reveals no structural change

After a scroll swipe, the traversal SHALL compare the pre-swipe and post-swipe UIAutomator hierarchy fingerprints; when they are equal (no new content revealed), the traversal SHALL NOT call the remote vision model for that scroll.

#### Scenario: Scroll reveals no new items

- **WHEN** a scroll swipe completes and the post-swipe hierarchy fingerprint equals the pre-swipe fingerprint
- **THEN** the scroll is treated as end-of-list without a remote vision call

#### Scenario: Scroll reveals new items

- **WHEN** a scroll swipe completes and the post-swipe hierarchy fingerprint differs from the pre-swipe fingerprint
- **THEN** the traversal analyzes the new page state to incorporate the newly revealed items

### Requirement: ResultVerify retry loop exits early when the page is unchanged

The ResultVerify handler SHALL compare the hierarchy fingerprint after its first verification attempt with the fingerprint observed before the action; when they are equal, the handler SHALL terminate the retry loop without further remote model calls and treat the verification as pending/unchanged.

#### Scenario: Unchanged page after first check

- **WHEN** the first post-action verification shows a fingerprint equal to the pre-action fingerprint
- **THEN** the handler exits the retry loop immediately and issues no further remote model calls

#### Scenario: Changed page verifies normally

- **WHEN** the first post-action verification shows a changed fingerprint
- **THEN** the handler proceeds with its normal verification path and retry behavior

### Requirement: Host page analysis prefers deterministic UIAutomator results over vision

The Host page analyzer SHALL run deterministic UIAutomator analysis before any remote vision call and SHALL return the deterministic result without a vision call when it is complete and reliable. The analyzer SHALL fall back to the remote vision model only when the deterministic result is missing, incomplete, or otherwise unreliable. A deterministic result is reliable when it produces a non-empty clickable item set, a recognized page identity or scroll state, and consistent coordinates.

#### Scenario: Settings page resolves deterministically

- **WHEN** the UIAutomator dump yields a complete page (items, scroll state, identity)
- **THEN** the analyzer returns the deterministic analysis and does not call the remote vision model

#### Scenario: Deterministic result unreliable

- **WHEN** the UIAutomator dump is empty, yields no clickable items, or fails to parse
- **THEN** the analyzer falls back to the remote vision model and returns its analysis

### Requirement: Safety semantics are preserved under deterministic-first analysis

Fingerprint-based skipping and deterministic-first ordering SHALL NOT weaken safety-before-action guarantees: safety policy evaluation SHALL still run before every device action using the freshest available page state, and any evidence of page state ambiguity SHALL force a vision fallback.

#### Scenario: Ambiguous page state forces fallback

- **WHEN** deterministic analysis cannot establish a trustworthy page state before an action
- **THEN** the analyzer performs a vision fallback before the safety evaluation proceeds
