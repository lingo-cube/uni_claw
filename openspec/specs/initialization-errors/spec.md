## ADDED Requirements

### Requirement: Initialization error base class
The InitializationError base class SHALL provide recoverable attribute to distinguish error types.

#### Scenario: Recoverable error
- **WHEN** InitializationError is created with recoverable=True
- **THEN** error.recoverable is True

#### Scenario: Non-recoverable error
- **WHEN** InitializationError is created with recoverable=False
- **THEN** error.recoverable is False

### Requirement: ConfigurationError for invalid plans
The system SHALL raise ConfigurationError for non-recoverable plan validation failures.

#### Scenario: Missing root node
- **WHEN** TraversalPlan.root_node is None
- **THEN** system raises ConfigurationError with recoverable=False

#### Scenario: Invalid root node type
- **WHEN** TraversalPlan.root_node.node_type is not CONTAINER
- **THEN** system raises ConfigurationError with recoverable=False

#### Scenario: Invalid root node operation
- **WHEN** TraversalPlan.root_node.operation.action is not "no_action"
- **THEN** system raises ConfigurationError with recoverable=False

### Requirement: EntryPolicyError for strategy failures
The system SHALL raise EntryPolicyError for recoverable entry strategy failures.

#### Scenario: All entry strategies fail
- **WHEN** all entry strategies in the fallback chain fail
- **THEN** system raises EntryPolicyError with recoverable=True
- **AND** error includes list of failed strategies
- **AND** error includes last_error from final failure

### Requirement: WaitConditionError for verification timeouts
The system SHALL raise WaitConditionError for recoverable verification failures.

#### Scenario: Verification fails in polling mode
- **WHEN** polling mode times out without satisfying wait_condition
- **THEN** system raises WaitConditionError with recoverable=True

#### Scenario: Verification fails in fast mode
- **WHEN** fast mode single check does not satisfy wait_condition
- **THEN** system raises WaitConditionError with recoverable=True

### Requirement: EntryError for strategy execution failures
The system SHALL raise EntryError for individual strategy execution failures.

#### Scenario: App icon not found in cold launch
- **WHEN** cold_launch strategy cannot find target app icon on home screen
- **THEN** system raises EntryError with descriptive message

### Requirement: Exception handling in initialize()
The initialize() method SHALL propagate exceptions instead of returning False.

#### Scenario: ConfigurationError during validation
- **WHEN** _validate_plan() raises ConfigurationError
- **THEN** initialize() propagates the exception without catching

#### Scenario: EntryPolicyError during entry
- **WHEN** _execute_entry_policy() raises EntryPolicyError
- **THEN** initialize() propagates the exception without catching

#### Scenario: Unexpected exception
- **WHEN** an unexpected exception occurs during initialization
- **THEN** initialize() sets global_state to ERROR and records error in trace
