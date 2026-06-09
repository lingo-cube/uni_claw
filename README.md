# Uni-claw: AI-Powered Mobile UI Traversal Framework

A modular, testable framework for automated mobile UI exploration using AI vision analysis and ADB control.

## Overview

Uni-claw enables automated traversal of mobile applications by combining:
- **AI Vision**: Screen understanding using multiple vision services (Claude, MiMo)
- **ADB Control**: Precise device interaction via Android Debug Bridge
- **State Management**: Intelligent caching and resume capability
- **Extensible Architecture**: Interface-based design for easy testing and extension
- **V6 Simulation** 🆕: Offline testing with mock components and visualization

## Features

- **Non-invasive**: Only requires screenshots and tap events
- **Intelligent Caching**: Remembers coordinates and menu structure
- **Resume Capability**: State persistence for interrupted runs
- **Comprehensive Handling**: Popups, page jumps, and error recovery
- **Mock Clients**: Full test coverage with mock implementations
- **Event System**: Real-time traversal events for monitoring
- **AI Strategy Advisor** (V5.0): Edge case decision support with safety filtering
- **Simulation Mode** (V6.0) 🆕: Device-less testing with trace visualization
- **State Machine Extensions** (V6.0) 🆕: FRAME_COMPLETE, ERROR_HANDLING, POPUP_HANDLING states
- **Declarative Plans** (V6.0) 🆕: TraversalPlan with completion policies and exit conditions

## Architecture

```
uni-claw/
├── src/
│   ├── adb/           # ADB client interface
│   ├── vision/        # Vision service interface
│   ├── state/         # Data models and state management
│   ├── traversal/     # Core traversal engine
│   ├── graph/         # V6: Graph models and traversal plans
│   ├── simulation/    # V6: Simulation and mock components
│   ├── ai/            # AI Strategy Advisor (V5.0)
│   ├── safety/        # Safety filter for AI outputs
│   ├── context/       # Traversal context for AI
│   └── config/        # Configuration management
└── tests/
    ├── v6/            # V6 test suite
    └── ...            # Existing tests
```

### Key Components

| Component | Description |
|-----------|-------------|
| `ADBClient` | Interface for device control (Real/Mock implementations) |
| `VisionService` | Interface for screen analysis (Claude/MiMo/Mock implementations) |
| `TraversalEngine` | Core traversal logic following PRD specification |
| `StateManager` | State persistence and recovery |
| `ContentTree` | Hierarchical UI structure representation |
| `AIStrategyAdvisor` | AI-powered decision support for edge cases (V5.0) |
| `SafetyFilter` | Validates AI outputs to prevent dangerous operations |
| `TraversalPlan` (V6.0) | Declarative traversal plan with completion policies |
| `GraphTraversalEngine` (V6.0) | Graph-based traversal execution engine |
| `SimulationRunner` (V6.0) | Offline simulation with trace visualization |

## Quick Start

### Installation

```bash
pip install uni-claw
```

### Basic Usage

```python
from uni_claw import TraversalEngine, VisionService, ADBClient

# Initialize components
vision = VisionService.create(mode="flattened", ai_provider="claude")
adb = ADBClient()

# Create traversal engine
engine = TraversalEngine(vision=vision, adb=adb)

# Run traversal
result = engine.traverse(app_name="com.example.app", max_steps=100)
```

### V6 Simulation Mode

```python
from src.graph.plan import TraversalPlan
from src.simulation.runner import SimulationRunner

# Load plan
plan = TraversalPlan.from_file("plan.json")

# Create virtual pages
virtual_pages = {
    "/home": {"screen_info": {"title": "Home"}, "elements": [...]}
}

# Run simulation
runner = SimulationRunner(virtual_pages, plan)
result = runner.run()

# Visualize trace
print(runner.render_tree())
print(runner.render_mermaid())
```

### AI Strategy Advisor (V5.0)

The AI Strategy Advisor provides intelligent decision support when rule-based approaches encounter edge cases:

**Three Core Capabilities:**
1. **Container Inference**: Identifies unknown UI container types
2. **Target Decision**: Locates elements when rules cannot find them
3. **Exception Handling**: Provides recovery strategies when exception chain is exhausted

**Safety Features:**
- Action whitelist (only allows: click, swipe, back, input_text, no_action)
- Text blacklist (blocks: "恢复出厂设置", "清除数据", "删除所有", etc.)
- Audit logging for all rejected operations
- Automatic fallback to safe defaults

**Configuration:**
```python
config = TraversalConfig(
    enable_ai_advisor=True,          # Enable AI features
    ai_call_timeout=30.0,             # AI call timeout in seconds
    ai_min_confidence=0.7,             # Minimum confidence threshold
    ai_cache_ttl=300,                 # Cache TTL in seconds (5 min)
)
```

**Implementations:**
- `NoOpAIAdvisor`: Default implementation (returns safe defaults)
- `MockAIAdvisor`: Test implementation with predefined responses

### Button Type Differentiation (V5.3) 🆕

The framework now intelligently differentiates button types for optimized interaction:

**Button Types:**
- `MENU_ITEM`: Standard menu items (navigate to new page)
- `TAB`: Tab items (switch content within same page)
- `BACK_BUTTON`: Navigation back buttons
- `TOGGLE`: Toggle switches/checkboxes (no page navigation)
- `LINK`: Links and readonly text
- `READONLY`: Non-interactive elements

**Expected Actions:**
- `NAVIGATE`: Button that navigates to a new page (wait >= 1.0s)
- `TOGGLE`: Button that toggles state without navigation (wait <= 0.3s)
- `ACTION`: Generic action button
- `NONE`: No action expected

**Enhanced MenuItem Structure:**
```python
MenuItem(
    name="WiFi Toggle",
    type=MenuItemType.TOGGLE,
    coordinate=Coordinate(x=0.5, y=0.5),
    expected_action=ExpectedAction.TOGGLE,      # New: Expected behavior
    expects_page_change=False,                   # New: Page change expectation
    expects_state_change=True,                   # New: State change expectation
)
```

**Benefits:**
- **Optimized Wait Times**: Toggle buttons wait <= 0.3s, Navigate buttons wait >= 1.0s
- **Smart Verification**: Toggle verification checks state change, Navigate verification checks path change
- **Read-only Detection`: Automatically skips non-interactive elements
- **Backward Compatible**: Old state files use sensible defaults

## Installation

```bash
# Clone repository
git clone <repository>
cd uni-claw

# Install dependencies (choose one method)

# Method 1: UV (Recommended - faster, modern)
# Install UV first: pip install uv
uv sync

# Method 2: Traditional pip
# Install from pyproject.toml (including development dependencies)
pip install -e ".[dev]"

# Copy environment template
cp .env.example .env

# Edit .env with your API key
# ANTHROPIC_API_KEY=your_key_here
```

## Usage

### Demo Mode (Mock)

Run with mock clients for testing:

```bash
python run.py "VehicleSettings" --mock
```

### Real Device

Run with actual device:

```bash
# Ensure ADB device is connected
adb devices

# Run traversal
python run.py "VehicleSettings" --device <device_id>

# Resume from saved state
python run.py "VehicleSettings"

# Reset state and start fresh
python run.py "VehicleSettings" --reset
```

### Configuration

Options via CLI or environment variables:

| CLI Flag | Environment Variable | Description |
|----------|---------------------|-------------|
| `--device` | `ADB_DEVICE_ID` | ADB device ID |
| `--vision-provider` | `VISION_PROVIDER` | Vision service: anthropic, mimo, mock |
| `--model` | `VISION_MODEL` | Model name |
| `--mock` | - | Use mock clients (same as --vision-provider mock) |
| `--reset` | - | Reset traversal state |
| `--wait-time` | `DEFAULT_WAIT_TIME` | Default wait time after click (seconds) |

### Wait Time Configuration (V5.3)

Wait times are automatically adjusted based on button type:

| Expected Action | Wait Time | Use Case |
|----------------|-----------|----------|
| `NAVIGATE` | >= 1.0s | Menu items, tabs (page change expected) |
| `TOGGLE` | <= 0.3s | Toggle switches, checkboxes (state change only) |
| `ACTION` | Default | Generic buttons (uses config.wait_time) |
| `NONE` | 0.1s | Read-only elements (minimal wait) |

**Override wait times:**
```python
config = TraversalConfig(
    wait_time=0.5,  # Default wait time for ACTION type
    navigate_wait=1.0,  # Wait time for NAVIGATE type
    toggle_wait=0.3,  # Wait time for TOGGLE type
)
```

### Vision Services

**Claude (Anthropic):**
```bash
# .env
ANTHROPIC_API_KEY=sk-ant-xxx
VISION_PROVIDER=anthropic

# Run
python run.py "车辆设置"
```

**MiMo (XiaoMi):**
```bash
# .env
MIMO_API_KEY=your_mimo_key
VISION_PROVIDER=mimo

# Run
python run.py "车辆设置" --vision-provider mimo
```

**Mock (No API required):**
```bash
python run.py "车辆设置" --mock
```

## Testing

```bash
# Run all tests
pytest

# Run with coverage
pytest --cov=src

# Run specific test file
pytest tests/test_adb_client.py

# Run integration tests (requires device)
pytest -m integration
```

## API Usage

### Programmatic Usage

**Using Claude:**
```python
from src.adb import RealADBClient
from src.vision import ClaudeVisionService
from src.state import StateManager
from src.traversal import TraversalConfig, TraversalEngine

adb = RealADBClient()
vision = ClaudeVisionService(api_key="your_anthropic_key")
```

**Using MiMo:**
```python
from src.adb import RealADBClient
from src.vision import MiMoVisionService
from src.state import StateManager
from src.traversal import TraversalConfig, TraversalEngine

adb = RealADBClient()
vision = MiMoVisionService(api_key="your_mimo_key")
```

**Complete Example:**
```python
from src.adb import RealADBClient
from src.vision import MiMoVisionService
from src.state import StateManager
from src.traversal import TraversalConfig, TraversalEngine

# Create clients
adb = RealADBClient()
vision = MiMoVisionService(api_key="your_key")
state_manager = StateManager(".state.json")

# Configure traversal
config = TraversalConfig(
    max_steps=200,
    wait_time=0.5,
)

# Create engine
engine = TraversalEngine(
    adb_client=adb,
    vision_service=vision,
    state=state_manager.state,
    config=config,
    event_callback=lambda e: print(e),
)

# Run traversal
engine.navigate_to_app("TargetApp")
engine.initialize_structure()
summary = engine.run()

# Export results
print(summary['tree'])
```

### Extending the Framework

#### Custom ADB Client

```python
from src.adb.adb_client import ADBClient

class CustomADBClient(ADBClient):
    def execute(self, command: str, timeout: int = 30) -> str:
        # Your implementation
        pass

    def tap(self, x: float, y: float) -> None:
        # Your implementation
        pass
    # ... implement other methods
```

#### Custom Vision Service

```python
from src.vision.vision_service import VisionService
from src.state.content_tree import PageAnalysis

class CustomVisionService(VisionService):
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        # Your implementation using different AI service
        pass

    def find_app_entry(self, image_data: bytes, target: str) -> dict | None:
        # Your implementation
        pass
```

## Data Structures

### PageAnalysis

Complete analysis of a screen page:
- `level1_menus`: Primary navigation menus
- `level2_menus`: Secondary tabs
- `current_path`: Current location
- `items`: Interactive elements
- Special elements: popups, back button

### ContentTree

Hierarchical structure of discovered content:
```markdown
0. VehicleSettings
  1. DiLink
    1.1. 互联
      1.1.1. 移动数据
      1.1.2. 无线网络
    1.2. 音响
  2. DiPilot
```

## Development

```bash
# Format code
black src/ tests/

# Type checking
mypy src/

# Linting
ruff check src/ tests/
```

## Design Principles

1. **Interface-Based Design**: All core components use abstract interfaces
2. **Dependency Injection**: Pass dependencies for testability
3. **State Separation**: State management independent of logic
4. **Event-Driven**: Real-time visibility into traversal progress
5. **Coordinate Caching**: AI identifies once, reuse coordinates
6. **Error Recovery**: Robust handling of unexpected states

## License

[Your License]

## Contributing

Contributions welcome! Please:
1. Write tests for new features
2. Follow existing code style
3. Update documentation as needed
