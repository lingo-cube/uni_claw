# PopupHandler Spec

> Detect → Classify → Preserve → Handle → Restore, 5-step pipeline for popup handling

## ADDED Requirements

### Requirement: PopupDetector regex pattern matching

PopupHandler SHALL provide a `PopupDetector` that identifies popup occurrences through case-insensitive regex pattern matching across 4 popup types.

#### Scenario: PopupType enum values

WHEN `PopupType` is defined
THEN it SHALL contain exactly 5 values: PERMISSION, ERROR, AD, DIALOG, UNKNOWN
AND each value SHALL represent a distinct popup category

#### Scenario: 4 popup types each with 5-6 regex patterns

WHEN `PopupDetector` pattern registry is defined
THEN it SHALL contain pattern entries for exactly 4 popup types: PERMISSION, ERROR, AD, DIALOG
AND each popup type SHALL have between 5 and 6 regex patterns
AND UNKNOWN SHALL NOT have predefined patterns (it is the fallback when no pattern matches)

#### Scenario: Case-insensitive regex matching

WHEN `PopupDetector.detect()` matches patterns against popup text or UI element identifiers
THEN all pattern matching SHALL be case-insensitive
AND SHALL use regex semantics (not substring matching)
AND a match on any pattern within a popup type SHALL classify the popup as that type

#### Scenario: Pattern priority among popup types

WHEN `PopupDetector.detect()` encounters text that matches patterns from multiple popup types
THEN the detector SHALL assign the popup type with the highest semantic priority
AND PERMISSION SHALL have the highest detection priority
AND ERROR SHALL have the second detection priority
AND AD SHALL have the third detection priority
AND DIALOG SHALL have the fourth detection priority

#### Scenario: No pattern match — returns UNKNOWN

WHEN `PopupDetector.detect()` encounters a popup that matches no patterns from any of the 4 defined types
THEN the detector SHALL return `PopupType.UNKNOWN`
AND SHALL NOT attempt heuristic classification beyond pattern matching

---

### Requirement: PopupClassifier 5 sub-method classification

PopupHandler SHALL provide a `PopupClassifier` that enriches a detected popup with dismiss strategy, urgency, and blocking type through 5 sequential sub-methods.

#### Scenario: determine_popup_type delegates to PopupDetector

WHEN `PopupClassifier.classify()` is called
THEN the first sub-method `determine_popup_type()` SHALL delegate to `PopupDetector.detect()` for type classification
AND SHALL accept the raw popup context as input
AND SHALL return the `PopupType` determined by the detector

#### Scenario: find_dismiss_target resolves dismiss button

WHEN `determine_popup_type()` has returned a PopupType
THEN the second sub-method `find_dismiss_target()` SHALL identify the target UI element for dismissing the popup
AND SHALL use the dismiss button priority list specific to the PopupType
AND SHALL return the first available dismiss target from the priority list

#### Scenario: PERMISSION dismiss button priorities

WHEN `find_dismiss_target()` resolves a dismiss target for `PopupType.PERMISSION`
THEN it SHALL evaluate dismiss buttons in the following priority order: ["allow", "accept", "continue", "grant", "ok"]
AND SHALL select the first button in the list that is present and clickable on the popup

#### Scenario: ERROR dismiss button priorities

WHEN `find_dismiss_target()` resolves a dismiss target for `PopupType.ERROR`
THEN it SHALL evaluate dismiss buttons in the following priority order: ["ok", "close", "dismiss", "acknowledge"]
AND SHALL select the first button in the list that is present and clickable on the popup

#### Scenario: AD dismiss button priorities

WHEN `find_dismiss_target()` resolves a dismiss target for `PopupType.AD`
THEN it SHALL evaluate dismiss buttons in the following priority order: ["close", "skip", "x", "dismiss"]
AND SHALL select the first button in the list that is present and clickable on the popup

#### Scenario: DIALOG dismiss button priorities

WHEN `find_dismiss_target()` resolves a dismiss target for `PopupType.DIALOG`
THEN it SHALL evaluate dismiss buttons in the following priority order: ["ok", "cancel", "close", "yes", "no"]
AND SHALL select the first button in the list that is present and clickable on the popup

#### Scenario: determine_dismiss_strategy selects strategy per type

WHEN `find_dismiss_target()` has returned a dismiss target
THEN the third sub-method `determine_dismiss_strategy()` SHALL select a `DismissStrategy` based on PopupType
AND SHALL map PopupType to DismissStrategy as follows:
- PERMISSION → `auto_close`
- ERROR → `back`
- AD → `wait_timeout`
- DIALOG → `auto_close_or_back`
- UNKNOWN → `back` (default fallback)

#### Scenario: DismissStrategy enum values

WHEN `DismissStrategy` is defined
THEN it SHALL contain exactly 4 values: auto_close, back, wait_timeout, auto_close_or_back
AND each value SHALL represent a distinct approach to popup dismissal

#### Scenario: determine_urgency assigns UrgencyLevel

WHEN `determine_dismiss_strategy()` has returned a DismissStrategy
THEN the fourth sub-method `determine_urgency()` SHALL assign an `UrgencyLevel` to the popup
AND SHALL base urgency on PopupType and popup content characteristics

#### Scenario: UrgencyLevel enum values

WHEN `UrgencyLevel` is defined
THEN it SHALL contain exactly 4 values: LOW, MEDIUM, HIGH, CRITICAL
AND the levels SHALL be ordered from least urgent (LOW) to most urgent (CRITICAL)

#### Scenario: determine_blocking_type assigns BlockingType

WHEN `determine_urgency()` has returned an UrgencyLevel
THEN the fifth sub-method `determine_blocking_type()` SHALL assign a `BlockingType` to the popup
AND SHALL base blocking type on whether the popup prevents interaction with underlying content

#### Scenario: BlockingType enum values

WHEN `BlockingType` is defined
THEN it SHALL contain exactly 3 values: MODAL, NON_MODAL, TOAST
AND MODAL SHALL indicate the popup blocks all underlying interaction
AND NON_MODAL SHALL indicate the popup allows partial underlying interaction
AND TOAST SHALL indicate the popup is transient and auto-dismisses

---

### Requirement: StateRestorer preserve-and-restore with validation

PopupHandler SHALL provide a `StateRestorer` that preserves traversal state before popup handling and restores it after, with validation of restored state integrity.

#### Scenario: preserve_state saves traversal context

WHEN `StateRestorer.preserve_state()` is called before popup handling begins
THEN it SHALL save the following fields into a state snapshot:
- `current_node_id`: the node currently being traversed
- `node_stack`: the navigation stack at the point of interruption
- `current_state`: the current traversal state machine state
- `execution_result`: the result of the most recent execution step
- `timestamp`: the wall-clock time at which the state was preserved

#### Scenario: restore_state restores traversal context

WHEN `StateRestorer.restore_state()` is called after popup handling completes
THEN it SHALL restore all preserved fields to the traversal context:
- `current_node_id` SHALL be set to the preserved value
- `node_stack` SHALL be set to the preserved stack
- `current_state` SHALL be set to the preserved state
- `execution_result` SHALL be set to the preserved result

#### Scenario: validate_restored_state verifies integrity

WHEN `StateRestorer.validate_restored_state()` is called after restoration
THEN it SHALL verify that all restored fields match their preserved counterparts
AND SHALL verify that `current_node_id` is not null or empty
AND SHALL verify that `node_stack` contains at least one entry
AND SHALL verify that `current_state` is a valid enum value

#### Scenario: Validation failure marks handling result as failed

WHEN `validate_restored_state()` detects that any restored field does not match its preserved value
OR any required field is null/empty/invalid
THEN the StateRestorer SHALL mark the `PopupHandlingResult` as failed
AND SHALL NOT silently ignore validation mismatches
AND SHALL include a description of which field(s) failed validation in the result

---

### Requirement: PopupHandler.handle_popup() 6-step flow

PopupHandler SHALL provide a `handle_popup()` method that orchestrates popup handling through a 6-step sequential pipeline.

#### Scenario: 6-step sequential pipeline

WHEN `PopupHandler.handle_popup()` is invoked with a popup context
THEN it SHALL execute the following 6 steps in strict sequential order:
1. **detect** — delegate to `PopupDetector.detect()` to identify PopupType
2. **classify** — delegate to `PopupClassifier.classify()` to enrich with dismiss strategy, urgency, and blocking type
3. **preserve** — delegate to `StateRestorer.preserve_state()` to snapshot traversal state
4. **handle** — delegate to the hook dispatch table to execute the PopupType-specific handling action
5. **restore** — delegate to `StateRestorer.restore_state()` to reinstate traversal state
6. **validate** — delegate to `StateRestorer.validate_restored_state()` to confirm restored state integrity

#### Scenario: Each step MUST complete before the next begins

WHEN `PopupHandler.handle_popup()` transitions between steps
THEN each step SHALL complete fully before the next step begins
AND SHALL NOT execute steps in parallel or out of order
AND failure in an early step SHALL NOT skip later steps (but SHALL propagate failure status)

---

### Requirement: PopupHandler hook dispatch table

PopupHandler SHALL dispatch popup handling actions through a hook-based dispatch table keyed by `PopupType`.

#### Scenario: Hook dispatch table structure

WHEN `PopupHandler` is initialized
THEN it SHALL contain a dispatch table of type `Dictionary<PopupType, Func<PopupContext, PopupHandlingResult>>`
AND the dispatch table SHALL contain hooks for each of the 5 `PopupType` values: PERMISSION, ERROR, AD, DIALOG, UNKNOWN
AND each hook SHALL be a `Func<PopupContext, PopupHandlingResult>` delegate

#### Scenario: PERMISSION hook execution

WHEN the handle step dispatches for `PopupType.PERMISSION`
THEN the executor SHALL invoke the PERMISSION hook delegate from the dispatch table
AND SHALL pass the `PopupContext` as the sole argument
AND SHALL return the `PopupHandlingResult` produced by the hook

#### Scenario: ERROR hook execution

WHEN the handle step dispatches for `PopupType.ERROR`
THEN the executor SHALL invoke the ERROR hook delegate from the dispatch table
AND SHALL pass the `PopupContext` as the sole argument
AND SHALL return the `PopupHandlingResult` produced by the hook

#### Scenario: AD hook execution

WHEN the handle step dispatches for `PopupType.AD`
THEN the executor SHALL invoke the AD hook delegate from the dispatch table
AND SHALL pass the `PopupContext` as the sole argument
AND SHALL return the `PopupHandlingResult` produced by the hook

#### Scenario: DIALOG hook execution

WHEN the handle step dispatches for `PopupType.DIALOG`
THEN the executor SHALL invoke the DIALOG hook delegate from the dispatch table
AND SHALL pass the `PopupContext` as the sole argument
AND SHALL return the `PopupHandlingResult` produced by the hook

#### Scenario: UNKNOWN hook execution

WHEN the handle step dispatches for `PopupType.UNKNOWN`
THEN the executor SHALL invoke the UNKNOWN hook delegate from the dispatch table
AND SHALL pass the `PopupContext` as the sole argument
AND SHALL return the `PopupHandlingResult` produced by the hook

#### Scenario: Exception fallback to back navigation

WHEN any hook delegate throws an exception during execution
THEN the handler SHALL NOT propagate the exception
AND SHALL fall back to executing a back navigation action (equivalent to pressing the device back button)
AND SHALL return a `PopupHandlingResult` reflecting the fallback action taken

---

### Requirement: PopupHandler statistics

PopupHandler SHALL track and report popup handling statistics across all handled popups.

#### Scenario: Statistics fields

WHEN `PopupHandler.statistics` is accessed
THEN it SHALL expose the following fields:
- `detected_count`: total number of popups detected by the detector
- `handled_count`: number of popups that were handled to a successful outcome (restoration validated)
- `handling_statistics`: `Dictionary<PopupType, int>` counting how many times each PopupType was detected
- `handling_rate`: ratio of handled_count to detected_count (computed as detected_count > 0 ? handled_count / detected_count : 0.0)

#### Scenario: Statistics are immutable snapshots

WHEN `PopupHandler.statistics` is read
THEN the returned statistics object SHALL be an immutable snapshot at the point of query
AND subsequent handler activity SHALL NOT mutate the previously returned snapshot
AND each read SHALL produce a new snapshot reflecting the current state
