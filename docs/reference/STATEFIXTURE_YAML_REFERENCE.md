# StateFixture YAML Format Reference

Complete reference for the StateFixture YAML format used in V6.9.2 simulation testing.

## Table of Contents

- [Overview](#overview)
- [Structure](#structure)
- [Page Definition](#page-definition)
- [Element Definition](#element-definition)
- [Transition Definition](#transition-definition)
- [Configuration](#configuration)
- [Examples](#examples)
- [Validation Rules](#validation-rules)

## Overview

A StateFixture YAML file defines a set of application pages with their UI elements and transition rules between pages. It enables stateful simulation testing where actions can cause page transitions.

## Structure

```yaml
pages:
  <page_id>:
    page_name: str
    elements: [...]
    is_complete: bool
transitions:
  <transition_id>:
    trigger: str
    from_page: str
    to_page: str
    action: str
initial_page: str
history_depth: int
```

## Page Definition

### Basic Structure

```yaml
pages:
  home:
    page_name: "HomeScreen"
    elements: [...]
    is_complete: false
```

### Page Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `page_name` | string | Yes | Human-readable page name |
| `elements` | array | Yes | List of page elements |
| `is_complete` | boolean | Yes | Whether traversal should complete on this page |

### Page ID

The key used in the `pages` map (e.g., `home` above) is the page identifier used in transitions.

## Element Definition

### Basic Structure

```yaml
elements:
  - id: "submit_btn"
    type: "button"
    text: "Submit"
    coordinate: {x: 0.5, y: 0.8}
    action_target: "success_page"
```

### Element Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique element identifier within the page |
| `type` | string | Yes | Element type (see types below) |
| `text` | string | Yes | Display text (maps to `MenuItem.name`) |
| `coordinate` | object | No | Position as `{x: float, y: float}` (0-1 range) |
| `action_target` | string | No | Target page ID for transitions |

### Element Types

Supported element types (mapped to `MenuItemType` enum):

| Type | Description | Typical Action |
|------|-------------|----------------|
| `button` | Clickable button | click |
| `back_button` | Navigation back button | click/back |
| `text_input` | Text input field | click/type |
| `switch` | Toggle switch | click |
| `slider` | Slider control | swipe |
| `text` | Static text label | none |
| `image` | Image element | click |
| `list_item` | List item | click |
| `checkbox` | Checkbox | click |
| `radio` | Radio button | click |

### Coordinate System

Coordinates are in the 0-1 range:
- `x: 0.0` = left edge, `x: 1.0` = right edge
- `y: 0.0` = top edge, `y: 1.0` = bottom edge

Examples:
```yaml
coordinate: {x: 0.5, y: 0.5}  # Center
coordinate: {x: 0.1, y: 0.1}  # Top-left (back button position)
coordinate: {x: 0.5, y: 0.9}  # Bottom-center (submit button position)
```

## Transition Definition

### Basic Structure

```yaml
transitions:
  to_settings:
    trigger: "settings_btn"
    from_page: "home"
    to_page: "settings"
    action: "click"
```

### Transition Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `trigger` | string | Yes | Element ID that triggers the transition |
| `from_page` | string | Yes | Source page ID |
| `to_page` | string | Yes | Target page ID |
| `action` | string | Yes | Action type (click, back, swipe) |

### Transition ID

The key used in the `transitions` map is for identification purposes only.

### Action Types

| Action | Description | Usage |
|--------|-------------|-------|
| `click` | Click/tap on element | Most common |
| `back` | Navigate back | back_button elements |
| `swipe` | Swipe gesture | Slider, scrollable content |

## Configuration

### Initial Page

```yaml
initial_page: "home"
```

The page where simulation starts. Must be a valid page ID defined in `pages`.

### History Depth

```yaml
history_depth: 10
```

Maximum number of pages to keep in navigation history for back navigation.

Default: `10`

## Examples

### Example 1: Simple Two-Page Flow

```yaml
pages:
  login:
    page_name: "LoginScreen"
    elements:
      - id: "username"
        type: "text_input"
        text: "Username"
        coordinate: {x: 0.5, y: 0.3}
      - id: "password"
        type: "text_input"
        text: "Password"
        coordinate: {x: 0.5, y: 0.4}
      - id: "submit"
        type: "button"
        text: "Login"
        coordinate: {x: 0.5, y: 0.6}
        action_target: "home"
    is_complete: false

  home:
    page_name: "HomeScreen"
    elements:
      - id: "logout"
        type: "button"
        text: "Logout"
        coordinate: {x: 0.9, y: 0.9}
        action_target: "login"
    is_complete: true

transitions:
  login_to_home:
    trigger: "submit"
    from_page: "login"
    to_page: "home"
    action: "click"
  home_to_login:
    trigger: "logout"
    from_page: "home"
    to_page: "login"
    action: "click"

initial_page: "login"
```

### Example 2: Branching Navigation

```yaml
pages:
  home:
    page_name: "HomeScreen"
    elements:
      - id: "settings"
        type: "button"
        text: "Settings"
        coordinate: {x: 0.5, y: 0.3}
        action_target: "settings"
      - id: "profile"
        type: "button"
        text: "Profile"
        coordinate: {x: 0.5, y: 0.5}
        action_target: "profile"
      - id: "help"
        type: "button"
        text: "Help"
        coordinate: {x: 0.5, y: 0.7}
        action_target: "help"
    is_complete: false

  settings:
    page_name: "SettingsScreen"
    elements:
      - id: "back"
        type: "back_button"
        text: "Back"
        coordinate: {x: 0.1, y: 0.1}
    is_complete: false

  profile:
    page_name: "ProfileScreen"
    elements:
      - id: "back"
        type: "back_button"
        text: "Back"
        coordinate: {x: 0.1, y: 0.1}
    is_complete: false

  help:
    page_name: "HelpScreen"
    elements:
      - id: "back"
        type: "back_button"
        text: "Back"
        coordinate: {x: 0.1, y: 0.1}
    is_complete: false

transitions:
  home_to_settings:
    trigger: "settings"
    from_page: "home"
    to_page: "settings"
    action: "click"
  home_to_profile:
    trigger: "profile"
    from_page: "home"
    to_page: "profile"
    action: "click"
  home_to_help:
    trigger: "help"
    from_page: "home"
    to_page: "help"
    action: "click"
  settings_back:
    trigger: "back"
    from_page: "settings"
    to_page: "home"
    action: "click"
  profile_back:
    trigger: "back"
    from_page: "profile"
    to_page: "home"
    action: "click"
  help_back:
    trigger: "back"
    from_page: "help"
    to_page: "home"
    action: "click"

initial_page: "home"
history_depth: 10
```

### Example 3: Single Static Page

```yaml
pages:
  main:
    page_name: "MainScreen"
    elements:
      - id: "button1"
        type: "button"
        text: "Button 1"
        coordinate: {x: 0.3, y: 0.5}
      - id: "button2"
        type: "button"
        text: "Button 2"
        coordinate: {x: 0.7, y: 0.5}
    is_complete: true

transitions: {}

initial_page: "main"
```

## Validation Rules

### Page Validation

1. **Initial page must exist**: `initial_page` must reference a valid page ID
2. **Page ID uniqueness**: Each page ID must be unique within the fixture
3. **Element ID uniqueness**: Element IDs must be unique within a page

### Transition Validation

1. **Source page must exist**: `from_page` must reference a valid page ID
2. **Target page must exist**: `to_page` must reference a valid page ID
3. **Trigger element must exist**: `trigger` must reference a valid element ID in the source page
4. **Action type must be valid**: Must be one of: `click`, `back`, `swipe`

### Element Validation

1. **Type must be valid**: Must be one of the supported element types
2. **Coordinates in range**: `x` and `y` must be between 0 and 1
3. **Required fields**: `id`, `type`, and `text` are required

## Field Mapping to Models

### StateFixture → PageAnalysis

| StateFixture Field | PageAnalysis/MenuItem Field | Notes |
|-------------------|----------------------------|-------|
| `element.text` | `MenuItem.name` | Display text |
| `element.type` | `MenuItem.type` | MenuItemType enum |
| `elements` | `PageAnalysis.items` | NOT `menu_items` |
| `page_name` | `PageAnalysis.name` | Page name |
| `coordinate` | `Coordinate` | x, y in 0-1 range |

### Important Notes

1. **items vs menu_items**: The PageAnalysis model uses `items`, not `menu_items`
2. **name vs text**: MenuItem stores display text in `name`, not `text`
3. **Type conversion**: Element type strings are converted to `MenuItemType` enum values

## Loading in Code

```python
from src.simulation.state_fixture import StateFixture

# Load from YAML file
fixture = StateFixture.from_yaml("path/to/fixture.yaml")

# Access pages
home_page = fixture.get_page("home")

# Get initial page
initial = fixture.get_initial_page()

# Get transition
transition = fixture.get_transition("home_to_detail")

# Validate fixture
fixture.validate()  # Raises ValueError if invalid
```

---

**Last Updated:** 2026-06-07
**Version:** V6.9.2
