# Capability: Hierarchy Baseline

4 层级导航基线测试，验证深层 DFS 遍历、多页面滚动状态管理、多层返回导航。

## ADDED Requirements

### Requirement: System shall provide 4-level hierarchy baseline test

The system SHALL provide a baseline test that validates DFS traversal behavior across a 4-level navigation hierarchy with 12 pages total.

#### Scenario: Full traversal visits all 4 levels
- **WHEN** the hierarchy baseline test runs in full traversal mode
- **THEN** the system SHALL visit all 12 pages
- **AND** the system SHALL complete with `success: true` and `reason: all_visited`
- **AND** the system SHALL execute at least 80 total steps
- **AND** the system SHALL visit at least 75 unique elements across all pages

### Requirement: System shall handle target search at Level 3

The system SHALL support target search scenarios that find elements at Level 3 of the hierarchy and terminate early.

#### Scenario: Target search terminates at Level 3
- **WHEN** the hierarchy baseline test runs in target search mode with target at Level 3
- **THEN** the system SHALL visit at most 8 pages (excluding pages deeper than target)
- **AND** the system SHALL complete with `success: true` and `reason: target_found`
- **AND** the system SHALL find the target element in the app_list (Level 3)

### Requirement: System shall handle multiple scrollable pages in single traversal

The system SHALL support scenarios where a single traversal visits multiple independent scrollable pages.

#### Scenario: Multi-scroll traversal visits 3 scrollable pages
- **WHEN** the hierarchy baseline test runs in multi-scroll mode
- **THEN** the system SHALL visit all 3 scrollable pages (network_list, app_list, perm_list)
- **AND** the system SHALL execute at least 15 scroll operations across all pages
- **AND** each scrollable page SHALL maintain independent scroll state

### Requirement: System shall handle scroll followed by multi-level back navigation

The system SHALL support scenarios where scrolling is followed by multiple levels of back navigation.

#### Scenario: Scroll then deep back navigation
- **WHEN** the hierarchy baseline test scrolls a page and then navigates back multiple levels
- **THEN** the system SHALL preserve scroll state during navigation
- **AND** the system SHALL successfully execute 3 consecutive back operations to reach Level 0
- **AND** the system SHALL complete with `success: true`

### Requirement: System shall aggregate scroll metrics across multiple pages

The system SHALL aggregate scroll metrics (scrollCount, jumpDetected, jumpRecovered) across all scrollable pages in hierarchy scenarios.

#### Scenario: Scroll metrics reflect aggregate behavior
- **WHEN** the hierarchy test completes with 3 scrollable pages visited
- **THEN** the scrollCount SHALL represent the sum of scrolls across all 3 pages
- **AND** the jumpDetected and jumpRecovered SHALL represent the sum across all pages
- **AND** the finalProgress SHALL be 0.0 with a note indicating multi-page scenario

### Requirement: System shall validate ElementCoverage for mixed fixture and scroll data

The system SHALL support ElementCoverage validation where some elements come from StateFixture (fixed pages) and others from ScrollDataStore (scrollable pages).

#### Scenario: ElementCoverage includes both fixture and scroll elements
- **WHEN** the hierarchy test validates ElementCoverage
- **THEN** the required elements SHALL include fixture elements (using auto_derive)
- **AND** the required elements SHALL include scroll elements (manually listed)
- **AND** the coverage ratio SHALL be at least 0.95

### Requirement: System shall maintain per-page scroll state

The system SHALL maintain independent scroll state for each page in the hierarchy, ensuring scroll progress is preserved during page transitions.

#### Scenario: Scroll state preserved during page navigation
- **WHEN** the user scrolls network_list to 50% progress, then navigates to app_list, then returns to network_list
- **THEN** network_list SHALL retain 50% scroll progress
- **AND** app_list SHALL maintain its own independent scroll state
