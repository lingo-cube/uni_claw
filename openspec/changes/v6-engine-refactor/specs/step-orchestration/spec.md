## ADDED Requirements

### Requirement: Single-step execution pipeline

The system SHALL execute a complete state machine step through a fixed pipeline: capture pre-step page snapshot, invoke the state machine, record metrics, intercept FRAME_COMPLETE, push children on BRANCH, detect path changes, detect page-non-change after EXECUTE, and record step boundaries.

#### Scenario: State machine step is invoked
- **WHEN** `execute_step()` is called with a valid stack, context, state machine, vision, and action
- **THEN** the state machine's `step()` method is invoked and the resulting transition is returned

#### Scenario: Engine completes traversal
- **WHEN** a Settings app traversal plan with recursive DYNAMIC_MATCH rules is executed
- **THEN** the engine SHALL complete in 89 steps with COMPLETED status, visiting 19 nodes across all 6 first-level and their second-level menu items

### Requirement: FRAME_COMPLETE interception

When the state machine transitions to FRAME_COMPLETE and the current container has a DYNAMIC_MATCH children strategy, the system SHALL check for remaining unvisited dynamic children. If any exist, the system MUST push the next unvisited child onto the stack and override the next state to NODE_SELECT.

#### Scenario: Frame complete with remaining dynamic children
- **WHEN** state machine transitions to FRAME_COMPLETE and the container has unvisited dynamic children
- **THEN** the next unvisited child is pushed and the transition is overridden to NODE_SELECT

#### Scenario: Frame complete with no remaining children
- **WHEN** state machine transitions to FRAME_COMPLETE and all dynamic children are visited
- **THEN** no override occurs and FRAME_COMPLETE proceeds normally

### Requirement: BRANCH child push

When the state machine transitions to BRANCH from EXECUTE, RESULT_VERIFY, or PRECONDITION_CHECK and the current node has unvisited dynamic children, the system SHALL push the next unvisited child onto the stack.

#### Scenario: Branch pushes next child
- **WHEN** a container with 3 generated children transitions BRANCH ← EXECUTE and 1 child has been visited
- **THEN** the next unvisited child is pushed onto the stack

### Requirement: Page-change detection after EXECUTE

After an EXECUTE step that transitions to RESULT_VERIFY, the system SHALL compare the pre-step page fingerprint with the post-execution page fingerprint. If they are identical, the system MUST mark the executed element as invalid.

#### Scenario: Page unchanged after click
- **WHEN** a menu item is clicked and vision service returns the same page (unchanged fingerprint)
- **THEN** the element is marked invalid and SHALL NOT generate further children on this page

#### Scenario: Page changed after click
- **WHEN** a menu item is clicked and vision service returns a different page
- **THEN** no invalidation occurs and the new page's elements are used for child generation

### Requirement: Path-change cache invalidation

When the traversal path changes, the system SHALL invalidate the dynamic children cache for the current container.

#### Scenario: Path changes trigger invalidation
- **WHEN** `context.current_path` differs from the last known path
- **THEN** `invalidate_children_cache` is called for the current container node
