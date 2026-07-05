## MODIFIED Requirements

### Requirement: StateRestorer preserve-and-restore with validation

PopupHandler SHALL provide a `StateRestorer` that preserves traversal state before popup handling and restores it after, with validation of restored state integrity.

#### Scenario: preserve_state saves complete node_stack contents
- **WHEN** `StateRestorer.preserve_state()` is called before popup handling begins
- **THEN** it SHALL save the following fields into a state snapshot:
  - `current_node_id`: the node currently being traversed
  - `node_stack`: the **complete** navigation stack (all StackFrames, not just the depth integer)
  - `current_state`: the current GlobalState
  - `execution_result`: the message from the most recent LastError
  - `timestamp`: the wall-clock time at which the state was preserved

#### Scenario: restore_state restores all preserved fields
- **WHEN** `StateRestorer.restore_state()` is called after popup handling completes
- **THEN** it SHALL restore ALL preserved fields to the traversal context:
  - `current_node_id` (via `CurrentFrame`) SHALL be set to the preserved value
  - `node_stack` SHALL be set to the preserved stack (all frames restored)
  - `current_state` (via `GlobalState`) SHALL be set to the preserved state
  - `execution_result` (via `LastError`) SHALL be set to the preserved result
  - All 5 fields SHALL be restored, not just GlobalState and LastError

#### Scenario: validate_restored_state verifies field matching against preserved values
- **WHEN** `StateRestorer.validate_restored_state()` is called after restoration
- **THEN** it SHALL verify that restored fields MATCH their preserved counterparts (not just structural checks)
- **AND** SHALL verify that `current_node_id` is not null or empty
- **AND** SHALL verify that `node_stack` contains at least one entry
- **AND** SHALL verify that `current_state` is a valid enum value
- **AND** SHALL compare restored `CurrentFrame.NodeId` against preserved `current_node_id`

#### Scenario: Validation failure marks handling result as failed with field details
- **WHEN** `validate_restored_state()` detects that any restored field does not match its preserved value
- **THEN** the StateRestorer SHALL mark the `PopupHandlingResult` as failed
- **AND** SHALL include a description of which field(s) failed validation in the result

### Requirement: PopupHandler.handle_popup() top-level exception fallback

PopupHandler SHALL provide a top-level try-catch wrapper in `handle_popup()` that catches any unhandled exception from the 6-step pipeline and falls back to back navigation.

#### Scenario: Top-level exception fallback to back navigation
- **WHEN** any step in the 6-step pipeline (detect/classify/preserve/handle/restore/validate) throws an unhandled exception
- **THEN** the exception SHALL be caught at the top level
- **AND** the handler SHALL return a `PopupHandlingResult` with `Success = false`, `Action = "back_fallback"`, and `Description` containing "Unhandled exception during popup handling"
- **AND** the exception SHALL NOT propagate to the caller
