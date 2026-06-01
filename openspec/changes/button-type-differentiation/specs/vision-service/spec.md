# Vision Service Capability Specification - Delta

## MODIFIED Requirements

### Requirement: Screenshot Analysis
The system SHALL analyze mobile screenshots and return structured page information with button type classification.

#### Scenario: Complete Page Analysis
- **WHEN** system calls `analyze_screenshot()` with screenshot bytes
- **THEN** system returns PageAnalysis containing:
  - level1_menus: list of primary navigation menus with coordinates
  - level2_menus: list of secondary tabs with coordinates
  - current_path: currently active menu path
  - items: clickable elements with type classification and expected behavior

#### Scenario: Popup Detection
- **WHEN** screenshot contains a popup dialog
- **THEN** PageAnalysis.is_popup SHALL be true
- **AND** popup_info SHALL contain popup title and content

#### Scenario: Back Button Detection
- **WHEN** screenshot contains a back navigation button
- **THEN** PageAnalysis.back_button SHALL contain its coordinates

#### Scenario: Button Type Classification
- **WHEN** analyzing items in screenshot
- **THEN** each item includes enhanced type classification
- **AND** type field may be: menu_item, tab, switch, toggle, button, back_button, link, readonly, icon, text
- **AND** item includes expected_action field
- **AND** item includes expects_page_change boolean
- **AND** item includes expects_state_change boolean

### Requirement: AI Prompt Templates
The system SHALL use defined prompt templates with button type classification instructions.

#### Scenario: Structure Analysis Prompt
- **WHEN** analyzing page structure
- **THEN** system uses PROMPT_STRUCTURE template
- **AND** template includes button type classification instructions
- **AND** template includes expected behavior prediction instructions
- **AND** template provides examples for each button type
- **AND** template requests JSON response with enhanced schema

#### Scenario: Enhanced Item Schema
- **WHEN** PROMPT_STRUCTURE defines item schema
- **THEN** schema includes:
  - type: detailed button type classification
  - expected_action: predicted behavior (navigate/toggle/action/none)
  - expects_page_change: boolean for page change expectation
  - expects_state_change: boolean for state change expectation

#### Scenario: Entry Finding Prompt
- **WHEN** finding app entry
- **THEN** system uses PROMPT_FIND_ENTRY template with target name
- **AND** template requests JSON with found status and coordinates
- **AND** template does NOT require button type classification (entry icons are uniform)

## ADDED Requirements

### Requirement: Type Classification Guidance
The system SHALL provide clear guidance to AI for button type classification.

#### Scenario: Type Definition in Prompt
- **WHEN** PROMPT_STRUCTURE is constructed
- **THEN** it includes definitions for each button type
- **AND** provides visual examples where applicable

#### Scenario: Expected Action Instructions
- **WHEN** PROMPT_STRUCTURE is constructed
- **THEN** it instructs AI to predict button behavior
- **AND** explains expected_action categories
- **AND** explains when to set expects_page_change vs expects_state_change
