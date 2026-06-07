# Spec: Behavior Validation

## ADDED Requirements

### Requirement: Expected Behavior YAML Format
The system SHALL support a YAML-based expected behavior definition format.

#### Scenario: Load expected behavior from YAML
- **WHEN** an ExpectedBehavior YAML file is loaded
- **THEN** the system SHALL parse scenario name, description, actions, page_transitions, visited_nodes, final_state, and completion_mode

### Requirement: Action Sequence Definition
The system SHALL support defining expected action sequences with node associations.

#### Scenario: Define click action
- **WHEN** an action is defined as {action: click, node: btn1, target: Button1}
- **THEN** the system SHALL create an ExpectedAction with action="click", node_id="btn1", target="Button1"

### Requirement: Page Transition Definition
The system SHALL support defining expected page transitions.

#### Scenario: Define page transition
- **WHEN** a page_transition is defined as {from: home, to: detail, trigger: btn1}
- **THEN** the system SHALL create an ExpectedPageTransition with from_page="home", to_page="detail", trigger="btn1"

### Requirement: Node Visitation Definition
The system SHALL support defining expected visited node sets.

#### Scenario: Define visited nodes
- **WHEN** visited_nodes is defined as [root, btn1, btn2]
- **THEN** the ExpectedBehavior.visited_nodes SHALL be a set containing "root", "btn1", "btn2"

### Requirement: Completion Mode Definition
The system SHALL support different completion modes.

#### Scenario: Normal completion mode
- **WHEN** completion_mode is defined as "normal"
- **THEN** the ExpectedBehavior.completion_mode SHALL be CompletionMode.NORMAL

#### Scenario: Exception completion mode
- **WHEN** completion_mode is defined as "exception" with expected_exception="TimeoutError"
- **THEN** the ExpectedBehavior.completion_mode SHALL be CompletionMode.EXCEPTION
- **AND** ExpectedBehavior.expected_exception SHALL be "TimeoutError"

### Requirement: Behavior Validation
The system SHALL validate actual simulation results against expected behavior.

#### Scenario: Validate action sequence match
- **WHEN** expected actions are [{action: no_action, node: root}, {action: click, node: btn1}]
- **AND** actual trace contains matching actions in the same order
- **THEN** ValidationResult.status SHALL be OK
- **AND** errors SHALL be empty

#### Scenario: Detect action sequence mismatch
- **WHEN** expected action is click but actual action is back
- **THEN** ValidationResult SHALL contain an error with category="action_sequence"
- **AND** ValidationResult.status SHALL be FAIL

### Requirement: Page Transition Validation
The system SHALL validate actual page transitions against expected transitions.

#### Scenario: Validate page transition sequence
- **WHEN** expected transitions are [{from: home, to: detail, trigger: btn1}]
- **AND** actual trace contains matching transition
- **THEN** no page_transition errors SHALL be present

#### Scenario: Detect missing page transition
- **WHEN** a page_transition is expected but not found in actual trace
- **THEN** ValidationResult SHALL contain an error with category="page_transition"

### Requirement: Node Visitation Validation
The system SHALL validate actual node visitation against expected nodes.

#### Scenario: Validate all expected nodes visited
- **WHEN** expected visited_nodes are {root, btn1, btn2}
- **AND** actual trace contains all these nodes
- **THEN** no node_visitation errors SHALL be present

#### Scenario: Detect unvisited expected node
- **WHEN** btn2 is in expected visited_nodes but not in actual trace
- **THEN** ValidationResult SHALL contain an error with category="node_visitation"
- **AND** the error SHALL specify btn2 as missing

#### Scenario: Detect unexpected visited node
- **WHEN** btn3 is visited but not in expected visited_nodes
- **THEN** ValidationResult SHALL contain a warning with category="node_visitation"
- **AND** the warning SHALL specify btn3 as unexpected

### Requirement: Final State Validation
The system SHALL validate the final execution state.

#### Scenario: Validate final completed state
- **WHEN** expected final_state is "COMPLETED"
- **AND** actual result.final_state.value is "COMPLETED"
- **THEN** no state errors SHALL be present

#### Scenario: Detect final state mismatch
- **WHEN** expected final_state is "COMPLETED" but actual is "ERROR"
- **THEN** ValidationResult SHALL contain an error with category="state"

### Requirement: Node Matching Strategy
The system SHALL support multi-level node matching with confidence scoring.

#### Scenario: Exact node match
- **WHEN** expected node_id is "btn1" and actual node_id is "btn1"
- **THEN** MatchResult SHALL have matched=true, match_type="exact", confidence=1.0

#### Scenario: Fuzzy ID substring match
- **WHEN** expected node_id is "btn1" and actual node_id is "btn1_generated_123"
- **THEN** MatchResult SHALL have matched=true, match_type="fuzzy", confidence=0.9
- **AND** reason SHALL describe the substring match

#### Scenario: Fuzzy target text match
- **WHEN** expected target is "Button1" and actual target contains "Button1"
- **THEN** MatchResult SHALL have matched=true, match_type="fuzzy", confidence=0.7
- **AND** reason SHALL describe the text match

#### Scenario: No match
- **WHEN** expected node_id has no similarity to actual node_id
- **THEN** MatchResult SHALL have matched=false, match_type="none", confidence=0.0

### Requirement: Fuzzy Match Severity
The system SHALL allow configuration of fuzzy match severity.

#### Scenario: Strict fuzzy match mode
- **WHEN** BehaviorValidator is initialized with strict_fuzzy_match=true
- **AND** a fuzzy match occurs
- **THEN** the ValidationIssue severity SHALL be "error"

#### Scenario: Lenient fuzzy match mode
- **WHEN** BehaviorValidator is initialized with strict_fuzzy_match=false
- **AND** a fuzzy match occurs
- **THEN** the ValidationIssue severity SHALL be "warning"

### Requirement: Validation Statistics
The system SHALL track exact and fuzzy match counts in validation results.

#### Scenario: Track match counts
- **WHEN** validation completes with 2 exact matches and 1 fuzzy match
- **THEN** ValidationResult.exact_match_count SHALL be 2
- **AND** ValidationResult.fuzzy_match_count SHALL be 1

### Requirement: Expected Behavior Validation
The system SHALL validate expected behavior definitions for completeness.

#### Scenario: Detect action order mismatch
- **WHEN** an action defines order=0 but appears at index 1
- **THEN** ExpectedBehavior.validate() SHALL return an error about order mismatch

#### Scenario: Detect missing visited nodes
- **WHEN** visited_nodes is empty
- **THEN** ExpectedBehavior.validate() SHALL return an error about no visited nodes

#### Scenario: Detect missing exception definition
- **WHEN** completion_mode is EXCEPTION but expected_exception is not specified
- **THEN** ExpectedBehavior.validate() SHALL return an error about missing expected_exception
