# Simulation Testing Guide

**Uni-Claw Simulation Testing Framework Documentation**

Welcome to the comprehensive guide for the Uni-Claw Simulation Testing Framework. This guide will help you understand, use, and extend the simulation testing system for mobile UI automation.

## 🎯 What is Simulation Testing?

Simulation testing is a **zero-cost, offline testing approach** that eliminates the need for:

- ❌ Real devices
- ❌ AI API calls  
- ❌ Network connectivity
- ❌ Expensive infrastructure

Instead, it provides:

- ✅ **Fast execution** - Tests run in seconds, not minutes
- ✅ **Deterministic results** - Same test, same result, every time
- ✅ **CI/CD ready** - Perfect for automated pipelines
- ✅ **Comprehensive coverage** - Test complex scenarios without limits

## 🏗️ Architecture Overview

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                    Simulation Testing Ecosystem              │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Test Case Layer                                             │
│  ├── AI-friendly JSON format                                │
│  └── 5 core test fixtures                                    │
│        ↓                                                     │
│  Test Execution Layer                                        │
│  ├── SimulationRunner (main orchestrator)                   │
│  ├── MockVisionService (page analysis)                      │
│  ├── MockActionExecutor (operation recording)                │
│  └── InMemoryTracer (trace visualization)                   │
│        ↓                                                     │
│  Assertion Layer                                             │
│  ├── TraceAsserter (automated comparison)                    │
│  └── AssertionResult (detailed feedback)                     │
│        ↓                                                     │
│  Reporting Layer                                             │
│  ├── CLI tools (simtest)                                    │
│  └── CI/CD integration                                       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Key Features |
|-----------|---------------|--------------|
| **PageAnalyzer** | Intelligent page analysis | Type inference, caching, element processing |
| **MockVisionService** | Path-aware visual analysis | Context integration, PageAnalyzer integration |
| **MockActionExecutor** | Comprehensive operation recording | OperationRecord TypedDict, context tracking |
| **SimulationRunner** | Complete simulation orchestration | GraphTraversalEngine wrapping, result extraction |
| **TraceAsserter** | Automated trace comparison | Natural language conversion, violation detection |
| **SimulationTestRunner** | Simplified test execution | Fixture loading, result validation |

## 🚀 Quick Start

### 1. Run Your First Test

```bash
# Run a single test
python cli/simtest.py run tests/simulation/fixtures/e2e_all_traversal

# Run test suite
python cli/simtest.py suite tests/simulation/fixtures --report results.json

# View test results
python cli/simtest.py show results.json
```

### 2. Create Your Own Test Case

```json
{
  "test_id": "my_first_test",
  "description": "Test my mobile app navigation",
  "test_dir": "tests/simulation/fixtures/my_test",
  "intent_slots": {
    "target_app": "MyApp",
    "scope": "all_menus",
    "element_handling": "full_interaction",
    "navigation": "adaptive",
    "restore": true,
    "depth": 3
  },
  "fixtures": {
    "plan_file": "plan.json",
    "pages_file": "pages.json"
  },
  "expected": {
    "completion_reason": "completed",
    "key_events": [
      "进入 root",
      "点击 SettingsButton",
      "遍历完成"
    ],
    "total_steps_min": 3,
    "total_steps_max": 10,
    "must_not_contain": ["错误", "崩溃"]
  }
}
```

### 3. Run with Pytest

```bash
# Run all simulation tests
pytest tests/simulation/ -v

# Run specific test categories
pytest tests/simulation/ -m "not integration"  # Skip integration tests
pytest tests/simulation/ -m "simulation"        # Run only simulation tests

# Generate coverage report
pytest tests/simulation/ --cov=src/simulation --cov-report=html
```

## 📝 Test Case Format

### AI-Friendly JSON Structure

The test case format is designed to be:

- **🤖 AI-readable** - Easy for AI tools to generate and validate
- **👤 Human-readable** - Clear structure for developers
- **🔧 Maintanable** - Modular and extensible

### Complete Test Case Example

```json
{
  "test_id": "e2e_settings_navigation",
  "description": "验证设置页面的完整导航流程",
  "test_dir": "tests/simulation/fixtures/settings_test",
  "intent_slots": {
    "target_app": "Settings",
    "scope": "all_menus",
    "element_handling": "full_interaction",
    "navigation": "adaptive",
    "restore": true,
    "depth": 3
  },
  "fixtures": {
    "plan_file": "plan.json",
    "pages_file": "pages.json"
  },
  "expected": {
    "completion_reason": "completed",
    "key_events": [
      "进入 homescreen",
      "点击 SettingsButton",
      "进入 settings",
      "点击 DisplayOption",
      "滑动 brightness_slider",
      "返回上一级",
      "点击 SoundOption",
      "滑动 volume_slider",
      "返回 homescreen",
      "遍历完成"
    ],
    "total_steps_min": 8,
    "total_steps_max": 20,
    "must_not_contain": ["错误", "异常", "崩溃"]
  },
  "assertions": {
    "visited_nodes_min": 3,
    "restore_operations_count": 2,
    "navigation_correctness": "depth_first"
  }
}
```

### Intent Slots Explained

Intent slots define the **testing strategy** and **scope**:

| Slot | Description | Example Values |
|------|-------------|----------------|
| `target_app` | Application being tested | `"Settings"`, `"Browser"`, `"Calculator"` |
| `scope` | Traversal scope | `"all_menus"`, `"target_search"`, `"static_path"` |
| `element_handling` | How to interact with elements | `"full_interaction"`, `"minimal"`, `"smart"` |
| `navigation` | Navigation strategy | `"adaptive"`, `"direct"`, `"fixed"` |
| `restore` | Whether to restore state | `true`, `false` |
| `depth` | Maximum traversal depth | `1`, `2`, `3`, `5` |

## 🧪 Core Test Scenarios

The framework includes **5 pre-built test scenarios** covering the most common testing patterns:

### 1. E2E All Traversal (`e2e_all_traversal`)
- **Purpose**: Validate complete menu traversal
- **Coverage**: DFS navigation, restore operations, all interaction types
- **Use Case**: Comprehensive navigation testing

### 2. E2E Target Found (`e2e_target_found`)
- **Purpose**: Test targeted search functionality
- **Coverage**: Target detection, early termination, efficiency
- **Use Case**: Finding specific elements quickly

### 3. E2E Static Path (`e2e_static_path`)
- **Purpose**: Test predefined navigation paths
- **Coverage**: Fixed path execution, exact sequence validation
- **Use Case**: Critical workflow testing

### 4. E2E Popup Handling (`e2e_popup_handling`)
- **Purpose**: Test system interruption handling
- **Coverage**: Dialog detection, dismissal, resumption
- **Use Case**: Permission dialogs, alerts, popups

### 5. E2E Auto Escape (`e2e_auto_escape`)
- **Purpose**: Test automatic depth management
- **Coverage**: Deep nesting, automatic back navigation, recovery
- **Use Case**: Deep menu structures, recursive navigation

## 🔍 Assertion and Validation

### Trace Assertion

The `TraceAsserter` automatically compares **expected vs actual** traces:

```python
from tests.simulation.helpers import TraceAsserter

# Automatic comparison
result = TraceAsserter.assert_trace_matches_expected(
    actual_trace,
    expected_behavior
)

# Check results
if result.success:
    print("✅ Test passed!")
else:
    print(f"❌ Test failed: {result.missing_events}")
```

### Assertion Types

| Assertion Type | What It Checks | Example |
|----------------|----------------|---------|
| **Key Events** | Expected events occurred | "进入 settings" in trace |
| **Violations** | No forbidden events | No "错误" or "崩溃" |
| **Step Count** | Within expected range | Between 5-20 steps |
| **Completion** | Correct completion reason | "completed" not "error" |
| **Coverage** | Minimum node coverage | At least 3 nodes visited |

### Natural Language Events

Events are expressed in **natural Chinese** for better readability:

```
进入 settings        → Enter settings page
点击 SettingsButton   → Click Settings button
滑动 brightness_slider → Scroll brightness slider
返回上一级           → Go back to previous level
遍历完成             → Traversal completed
```

## 🛠️ CLI Usage

### simtest Commands

#### Run Single Test
```bash
simtest run <test_path> [options]
```

**Options:**
- `--output <file>` - Save results to file
- `--format <json|text>` - Output format (default: json)

**Example:**
```bash
simtest run tests/simulation/fixtures/e2e_all_traversal --output result.json
```

#### Run Test Suite
```bash
simtest suite <tests_dir> [options]
```

**Options:**
- `--report <file>` - Generate aggregated report
- `--pattern <pattern>` - File pattern (default: test_case.json)

**Example:**
```bash
simtest suite tests/simulation/fixtures --report report.json
```

#### Show Results
```bash
simtest show <report_path>
```

**Example:**
```bash
simtest show reports/latest_report.json
```

## 🔄 CI/CD Integration

### GitHub Actions Integration

The framework includes **ready-to-use CI/CD workflows**:

```yaml
# .github/workflows/simulation-tests.yml
name: Simulation Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        python-version: [3.10, 3.11]
    
    steps:
    - uses: actions/checkout@v3
    - uses: actions/setup-python@v4
      with:
        python-version: ${{ matrix.python-version }}
    
    - name: Install dependencies
      run: |
        pip install -e .
        pip install pytest pytest-cov
    
    - name: Run simulation tests
      run: |
        pytest tests/simulation/ --cov=src/simulation
    
    - name: Upload coverage
      uses: actions/upload-artifact@v3
      with:
        name: coverage-report
        path: coverage.xml
```

### Pre-commit Hooks

Add **automated testing** to your development workflow:

```bash
#!/bin/bash
# .git/hooks/pre-commit

echo "🧪 Running simulation tests..."

python cli/simtest.py suite tests/simulation/fixtures --fast

if [ $? -ne 0 ]; then
    echo "❌ Tests failed. Commit aborted."
    exit 1
fi

echo "✅ Tests passed. Proceeding with commit."
```

## 📊 Performance Metrics

### Execution Time Targets

| Operation | Target Time | Measurement Method |
|------------|-------------|-------------------|
| **Single test** | < 5 seconds | `time simtest run <test>` |
| **Test suite** | < 2 minutes | `time simtest suite <dir>` |
| **Page analysis** | < 10ms | PageAnalyzer performance test |
| **Report generation** | < 1 second | Report generation timing |

### Resource Usage

| Resource | Limit | Monitoring |
|----------|-------|------------|
| **Memory per test** | < 100MB | `/usr/bin/time -v` |
| **Disk usage** | < 50MB | `du -sh results/` |
| **CPU usage** | < 50% | `top -b -n 1` |

## 🐛 Debugging and Troubleshooting

### Common Issues

#### 1. Test Not Loading
```bash
# Verify test case format
python scripts/verify_simulation_setup.py

# Check test case validation
python -c "
from tests.simulation.helpers import SimulationTestRunner
runner = SimulationTestRunner()
errors = runner.validate_test_case(test_case)
print(f'Errors: {errors}')
"
```

#### 2. Simulation Not Completing
```bash
# Check simulation results
python scripts/check_simulation_results.py results/test_result.json

# Verify component integration
python -c "
from src.simulation.runner import SimulationRunner
# Check initialization and setup
"
```

#### 3. CI Failures
```bash
# Test locally first
pytest tests/simulation/ -v

# Check environment
python scripts/verify_simulation_setup.py

# Run with verbose output
pytest tests/simulation/ -vv --tb=long
```

### Debug Mode

Enable detailed logging:

```python
import logging
logging.basicConfig(level=logging.DEBUG)

# Run simulation
result = runner.run()
```

## 📈 Best Practices

### 1. Test Organization
- **One scenario per test case** - Keep tests focused
- **Descriptive names** - Use clear, action-oriented names
- **Modular fixtures** - Reuse plan and pages files

### 2. Assertion Strategy
- **Focus on key events** - Don't over-specify
- **Allow flexibility** - Use ranges rather than exact values
- **Test intent, not implementation** - What, not how

### 3. Performance
- **Cache page analyses** - Reuse PageAnalyzer results
- **Minimize fixture size** - Only include essential elements
- **Parallel execution** - Run independent tests concurrently

### 4. Maintenance
- **Version test cases** - Track changes with Git
- **Document intent** - Explain *why* not *what*
- **Regular updates** - Keep fixtures aligned with app changes

## 🎓 Advanced Usage

### Custom Test Scenarios

Create **domain-specific test patterns**:

```python
from tests.simulation.helpers import SimulationTestRunner

class CustomTestRunner(SimulationTestRunner):
    def run_custom_scenario(self, scenario_config):
        """Run custom scenario with special logic."""
        # Your custom logic here
        pass
```

### Extending the Framework

#### Add New Assertion Types

```python
from tests.simulation.helpers.assertions import TraceAsserter

class ExtendedTraceAsserter(TraceAsserter):
    @staticmethod
    def assert_performance(trace, max_time_ms):
        """Custom performance assertion."""
        # Your logic here
        pass
```

#### Custom Report Generators

```python
from tests.simulation.helpers.report_generator import SimulationReportGenerator

class CustomReportGenerator(SimulationReportGenerator):
    def generate_custom_report(self, results):
        """Generate custom report format."""
        # Your logic here
        pass
```

## 📚 Additional Resources

### Internal Documentation
- **[Architecture Overview](../ARCHITECTURE.md)** - Complete system architecture
- **[Component Design](../core_business_models.md)** - Data models and structures
- **[State Machine Design](../state_machine_design.md)** - State management

### Test Documentation
- **[Test Guide](../../tests/README.md)** - Testing conventions
- **[Dashboard Documentation](../../dashboards/README.md)** - Visualization tools

### Related Tools
- **[Graph Traversal Engine](../src/graph/)** - Core traversal logic
- **[State Machine Simulator](../src/state_machine/)** - State validation
- **[AI Integration](../src/ai/)** - AI-powered testing (optional)

## 🤝 Contributing

When contributing to the simulation testing framework:

1. **Follow existing patterns** - Match code style and structure
2. **Add tests** - Include tests for new functionality
3. **Update documentation** - Keep docs in sync with code
4. **Test thoroughly** - Run full test suite before committing

## ❓ FAQ

### Q: How do I create a new test case?
**A:** Copy the template fixture and modify the intent slots, plan, and pages for your scenario.

### Q: Can I use simulation testing for performance testing?
**A:** Yes! The framework includes performance metrics and timing tracking.

### Q: How do I integrate with existing CI/CD?
**A:** Use the provided GitHub Actions workflow or adapt the CLI commands for your pipeline.

### Q: What if my app has complex state management?
**A:** Use the state management features in MockActionExecutor and SimulationRunner.

### Q: Can I test network-dependent features?
**A:** For network features, consider mocking at a higher level or using integration testing.

---

**Version:** 1.0.0  
**Last Updated:** 2026-06-03  
**Maintained By:** Uni-Claw Development Team