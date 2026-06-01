# Vision Service Capability Specification

## ADDED Requirements

### Requirement: Multi-Provider Support
The system SHALL support multiple AI vision providers through a common interface.

#### Scenario: Claude Provider
- **WHEN** user configures `VISION_PROVIDER=anthropic`
- **THEN** system uses ClaudeVisionService with Anthropic API

#### Scenario: MiMo Provider (OpenAI Protocol)
- **WHEN** user configures `VISION_PROVIDER=mimo`
- **THEN** system uses MiMoVisionService with OpenAI-compatible endpoint

#### Scenario: MiMo Provider (Claude Protocol)
- **WHEN** user configures `VISION_PROVIDER=mimo-cc`
- **THEN** system uses MiMoCCVisionService with Anthropic-compatible endpoint

#### Scenario: Mock Provider
- **WHEN** user uses `--mock` flag
- **THEN** system uses MockVisionService without API calls

### Requirement: Screenshot Analysis
The system SHALL analyze mobile screenshots and return structured page information.

#### Scenario: Complete Page Analysis
- **WHEN** system calls `analyze_screenshot()` with screenshot bytes
- **THEN** system returns PageAnalysis containing:
  - level1_menus: list of primary navigation menus with coordinates
  - level2_menus: list of secondary tabs with coordinates
  - current_path: currently active menu path
  - items: clickable elements in content area

#### Scenario: Popup Detection
- **WHEN** screenshot contains a popup dialog
- **THEN** PageAnalysis.is_popup SHALL be true
- **AND** popup_info SHALL contain popup title and content

#### Scenario: Back Button Detection
- **WHEN** screenshot contains a back navigation button
- **THEN** PageAnalysis.back_button SHALL contain its coordinates

### Requirement: App Entry Discovery
The system SHALL locate target app icons on home screen.

#### Scenario: Entry Found
- **WHEN** system calls `find_app_entry()` with target app name
- **AND** target icon exists on screen
- **THEN** system returns dict with x, y coordinates and app name

#### Scenario: Entry Not Found
- **WHEN** system calls `find_app_entry()` with non-existent app name
- **THEN** system returns None

### Requirement: AI Prompt Templates
The system SHALL use defined prompt templates for AI analysis.

#### Scenario: Structure Analysis Prompt
- **WHEN** analyzing page structure
- **THEN** system uses PROMPT_STRUCTURE template
- **AND** template requests JSON response with specific schema

#### Scenario: Entry Finding Prompt
- **WHEN** finding app entry
- **THEN** system uses PROMPT_FIND_ENTRY template with target name
- **AND** template requests JSON with found status and coordinates

### Requirement: Model Configuration
The system SHALL support configurable AI models.

#### Scenario: Default Model
- **WHEN** no model specified
- **THEN** Claude uses claude-3-5-sonnet-20241022
- **AND** MiMo uses mimo-v2.5

#### Scenario: Custom Model
- **WHEN** user sets `VISION_MODEL` environment variable
- **THEN** system uses specified model

### Requirement: API Key Management
The system SHALL support API key through environment variables.

#### Scenario: Claude API Key
- **WHEN** ANTHROPIC_API_KEY is set
- **THEN** ClaudeVisionService uses that key

#### Scenario: MiMo API Key
- **WHEN** MIMO_API_KEY is set
- **THEN** MiMo services use that key

#### Scenario: Missing API Key
- **WHEN** required API key is not set
- **THEN** service raises ValueError with clear message

### Requirement: Response Parsing
The system SHALL parse AI JSON responses with error handling.

#### Scenario: JSON in Markdown
- **WHEN** AI response contains JSON in markdown code blocks
- **THEN** system extracts JSON from code blocks

#### Scenario: Invalid JSON
- **WHEN** AI response contains invalid JSON
- **THEN** system raises VisionError with parsing details

#### Scenario: Missing Expected Fields
- **WHEN** parsed JSON lacks required fields
- **THEN** Pydantic validation raises appropriate error
