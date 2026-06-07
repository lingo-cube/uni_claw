# Simulation Test Fixtures

## Directory

```
tests/assets/fixtures/
├── README.md                    # This file
├── virtual_pages_simple.json    # Minimal single-page fixture
├── pages_all.json               # 7 settings pages (full menu)
├── pages_find.json              # 5 pages for target search
├── plan_all.json                # Full menu traversal plan
├── plan_find_version.json       # Target search plan
├── plan_static.json             # Static path plan
├── ai_data.json                 # AI inference data
├── graph_nodes.json             # Graph node definitions
├── page_analysis.json           # Page analysis samples
├── state_machines.json          # State machine fixtures
└── trace_data.json              # Trace data samples
```

## Virtual Pages Format

All virtual_pages fixtures for `MockVisionService` use the standard format:

```json
{
  "home": {
    "elements": [
      {
        "text": "Settings",
        "element_type": "menu_item",
        "bounds": {"x": 0.5, "y": 0.3},
        "action_hint": "view",
        "metadata": {}
      }
    ],
    "level1_dir": "right",
    "level2_dir": "bottom",
    "is_popup": false,
    "has_scroll": false,
    "is_end_of_list": false
  }
}
```

**Required fields per page:**
- `elements`: list of UI element dicts (minimal: `{"text": "...", "element_type": "..."}`)
- `level1_dir`: "left" | "right" | "top" | "bottom"
- `level2_dir`: "left" | "right" | "top" | "bottom"
- `is_popup`: boolean

**Rule**: Always use `"elements"`, never `"items"`. The `PageAnalyzer` outputs `elements`, and `MockVisionService._build_page_analysis` reads `elements`.

## Usage

```python
from tests.assets import load_virtual_pages, load_fixture

# Standard simple page
vp = load_virtual_pages()  # loads virtual_pages_simple.json

# Specific fixture
vp = load_virtual_pages("pages_all.json")
plan_data = load_fixture("plan_all.json")
```

## Rules

1. **Never hardcode virtual_pages inline** in test code — use fixtures
2. **One fixture = one purpose**: `virtual_pages_simple.json` for basic tests, `pages_all.json` for complex ones
3. **Use `elements` key**, never `items` (old format — causes empty PageAnalysis)
4. **All pages must have `level1_dir` and `level2_dir`** (`PageAnalysis` requires them)
