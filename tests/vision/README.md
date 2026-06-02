# Vision Service Tests

This directory contains tests for vision services, including performance comparison between legacy and new flattened-screen pipelines.

## Directory Structure

```
tests/vision/
├── performance_comparison.py       # Performance comparison framework
├── test_performance_comparison.py   # Unit tests
└── README.md                        # This file
```

```
tests/assets/
├── screenshots/                     # Test screenshots
│   ├── settings_main.png            # Settings main page (split pane layout)
│   ├── settings_display.png         # Display settings (with switches)
│   ├── settings_network.png         # Network settings (with list)
│   ├── dialog_confirm.png            # Confirmation dialog
│   ├── dialog_input.png              # Input dialog
│   ├── tabbed_view.png               # Tabbed view
│   ├── single_page.png               # Single page view
│   └── overlay_popup.png             # Overlay popup
└── ground_truth/                    # Ground truth annotations
    ├── settings_main.json            # Standard PageAnalysis for settings_main
    ├── settings_display.json
    ├── settings_network.json
    └── ...
```

## Performance Comparison Framework

The performance comparison framework (`performance_comparison.py`) provides tools to:

1. **Run Both Pipelines**: Test legacy and flattened pipelines on the same screenshots
2. **Measure Metrics**: Collect token consumption, latency, and accuracy data
3. **Generate Reports**: Produce detailed comparison reports

### Key Classes

| Class | Purpose |
|-------|---------|
| `PerformanceMetrics` | Data class for storing metrics from a single analysis call |
| `ComparisonResult` | Result of comparing legacy vs flattened on one screenshot |
| `VisionServiceTester` | Test framework for running comparisons |

### Usage Example

```python
from tests.vision.performance_comparison import run_comparison
from src.vision.vision_service import ClaudeVisionService
from src.vision.flattened_vision_service import FlattenedVisionService

# Initialize services
legacy_service = ClaudeVisionService(api_key="...")
flattened_service = FlattenedVisionService(api_key="...")

# Run comparison
report = run_comparison(
    legacy_service=legacy_service,
    flattened_service=flattened_service,
    screenshot_dir="tests/assets/screenshots",
    ground_truth_dir="tests/assets/ground_truth",
)

# Print results
print(f"Token reduction: {report['improvements']['avg_token_reduction_pct']:.1f}%")
print(f"Speed improvement: {report['improvements']['avg_speed_improvement_pct']:.1f}%")
print(f"Hierarchy accuracy: {report['accuracy']['flattened_avg']['hierarchy']*100:.1f}%")
```

### Expected Performance Targets

Based on PRD V5.2, the following targets should be achieved:

| Metric | Target |
|--------|--------|
| Token reduction | ≥60% |
| Speed improvement | ≥30% |
| Hierarchy accuracy | ≥90% |
| Behavior accuracy | ≥85% |
| Popup detection accuracy | ≥95% |

## Running Tests

```bash
# Run all vision tests
pytest tests/vision/ -v

# Run with coverage
pytest tests/vision/ --cov=src/vision --cov-report=term-missing

# Run specific test file
pytest tests/vision/test_performance_comparison.py -v

# Run comparison manually (requires real API keys)
python -m tests.vision.performance_comparison
```

## Adding New Test Screenshots

1. Place screenshot PNG file in `tests/assets/screenshots/`
2. Create corresponding ground truth JSON in `tests/assets/ground_truth/`
3. Update this README if adding a new category

### Ground Truth JSON Format

```json
{
  "level1_dir": "left",
  "level1_menus": [
    {"name": "显示", "x": 0.05, "y": 0.15, "active": true}
  ],
  "level2_dir": "top",
  "level2_menus": [
    {"name": "通用", "x": 0.28, "y": 0.06, "active": true}
  ],
  "current_path": ["设置", "显示"],
  "items": [
    {
      "id": 1,
      "name": "亮度",
      "type": "menu_item",
      "coordinate": {"x": 0.45, "y": 0.25},
      "expected_action": "navigate",
      "expects_page_change": true,
      "expects_state_change": false,
      "parent": null,
      "confidence": 0.98
    },
    {
      "id": 2,
      "name": "亮度调节",
      "type": "slider",
      "coordinate": {"x": 0.85, "y": 0.25},
      "expected_action": "toggle",
      "expects_page_change": false,
      "expects_state_change": true,
      "parent": "亮度",
      "confidence": 0.95
    }
  ],
  "is_popup": false,
  "popup_info": null,
  "has_scroll": false,
  "is_end_of_list": false
}
```

## Continuous Monitoring

When running in production, the vision services should log performance data in the following format:

```json
{
  "timestamp": "2026-06-02T10:30:00Z",
  "screenshot_hash": "a1b2c3d4",
  "mode": "flattened",
  "step1_multimodal": {
    "latency_ms": 1234,
    "input_tokens": 500,
    "output_tokens": 380
  },
  "step2_text": {
    "latency_ms": 456,
    "input_tokens": 800,
    "output_tokens": 420
  },
  "total": {
    "latency_ms": 1690,
    "tokens": 1600
  },
  "accuracy": {
    "hierarchy": 0.95,
    "behavior": 0.88,
    "popup": 1.0
  },
  "cache": {
    "flattened_hit": true,
    "page_analysis_hit": false
  }
}
```

This data can be collected and analyzed to track performance over time.
