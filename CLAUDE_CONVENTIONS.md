# Uni-Claw Code Standards and Conventions

> **Version**: 1.0
> **Last Updated**: 2026-06-08
> **Scope**: All code in `src/`, `tests/`

---

## 1. Strong Typing Requirements (MANDATORY) ⭐

### 1.1 Functions MUST Have Type Annotations

```python
# REQUIRED
def process_action(action: Action) -> ActionResult:
    ...

# REQUIRED for class methods
class TraversalEngine:
    def execute(self, plan: TraversalPlan) -> None:
        ...
```

### 1.2 Use Concrete Types, Disable Any

```python
# FORBIDDEN
def handle(data: Any) -> Any:
    ...

# REQUIRED
def handle(data: dict[str, str]) -> list[ActionResult]:
    ...
```

### 1.3 Generic Types Need Bounds

```python
# REQUIRED
class Repository[T: Model]:
    def find(self, id: str) -> T | None:
        ...
```

### 1.4 Return Types Must Be Explicit

```python
# FORBIDDEN (implicit None)
def log_action(action: Action):
    print(action)

# REQUIRED
def log_action(action: Action) -> None:
    print(action)
```

---

## 2. Design Patterns

### 2.1 Interface-First Principle

Always define protocols before implementations:

```python
from typing import Protocol

class VisionService(Protocol):
    async def analyze(self, image: bytes) -> ScreenElement:
        """Analyze screen image and return elements."""
        ...

class DeepSeekVision:
    async def analyze(self, image: bytes) -> ScreenElement:
        # Implementation
        ...
```

### 2.2 Dependency Injection

Inject dependencies via constructor, never instantiate inside methods:

```python
# GOOD
class GraphTraversalEngine:
    def __init__(
        self,
        vision: VisionService,
        executor: ActionExecutor,
        tracer: TraceRecorder,
    ) -> None:
        self.vision = vision
        self.executor = executor
        self.tracer = tracer

# BAD - tight coupling
class GraphTraversalEngine:
    def __init__(self) -> None:
        self.vision = DeepSeekVision()  # Don't do this
```

---

## 3. Naming Conventions

| Category | Convention | Example |
|----------|------------|---------|
| Classes | PascalCase | `GraphTraversalEngine`, `ActionResult` |
| Functions/Methods | snake_case | `execute_plan`, `find_element` |
| Constants | UPPER_SNAKE_CASE | `MAX_RETRY_COUNT`, `DEFAULT_TIMEOUT` |
| Private members | leading underscore | `_internal_state`, `_helper()` |
| Type aliases | PascalCase | `NodeId = str`, `ActionMap = dict[str, Action]` |

---

## 4. File Organization

### 4.1 Module Structure

Each module follows this pattern:

```
src/feature/
├── __init__.py       # Public exports only
├── models.py          # Data models
├── interface.py      # Protocol/ABC definitions
├── implementation.py  # Concrete implementations
├── utils.py           # Helper functions
└── README.md          # Module documentation
```

### 4.2 Import Order

```python
# 1. Standard library
import asyncio
from typing import Protocol

# 2. Third-party imports
from anthropic import AsyncAnthropic

# 3. Local imports
from src.graph.models import TraversalPlan
from src.trace.recorder import TraceRecorder
```

---

## 5. Testing Conventions

### 5.1 Test File Placement

```
tests/
├── unit/              # Fast, isolated tests
├── integration/       # Cross-module tests
├── v6/                # V6-specific tests
└── fixtures/          # Test data and mocks
```

### 5.2 Test Naming

```python
# Pattern: test_<method>_<scenario>_<expected_result>
def test_execute_plan_with_invalid_graph_raises_error():
    ...

def test_analyze_screen_returns_elements():
    ...
```

### 5.3 Test Structure

```python
def test_feature():
    # Arrange
    plan = TraversalPlan(...)

    # Act
    result = engine.execute(plan)

    # Assert
    assert result.success
    assert len(result.steps) == 3
```

---

## 6. File Placement Conventions

### 6.1 Temp Files Directory

**ALL temporary files MUST go to `temp/` directory:**

```bash
temp/
├── traces/           # Generated trace files
├── mock_data/        # Temporary test data
├── visualizations/   # Temporary debug outputs
└── debug/            # Debug dumps
```

### 6.2 Never Create Temp Files in:

- `src/` (source code)
- `tests/` (test files)
- `docs/` (documentation)
- Root directory

---

## 7. Code Quality Gates

1. **mypy strict mode** - All code must pass strict type checking
2. **pytest coverage > 80%** - Maintain high test coverage
3. **ruff linting** - Zero lint warnings
4. **pre-commit hooks** - Run before every commit

---

## 8. Quick Reference

| Rule | Status |
|------|--------|
| Type annotations on functions | MANDATORY ⭐ |
| Explicit return types | MANDATORY ⭐ |
| No `Any` types | MANDATORY ⭐ |
| Protocol-first design | RECOMMENDED |
| Dependency injection | RECOMMENDED |
| Temp files in `temp/` | MANDATORY ⭐ |

---

**Enforcement**: These conventions are enforced via CI/CD pipeline and pre-commit hooks.
