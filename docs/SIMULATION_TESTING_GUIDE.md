# Simulation Testing System - Complete Guide

**Uni-Claw V6.0 Simulation Testing Framework**

## 🎯 Overview

The Simulation Testing System is a **production-ready, zero-cost testing framework** that provides:

- **⚡ Fast execution** - Tests run in seconds, not minutes
- **💰 Zero operational costs** - No AI API calls, no real devices needed
- **🔒 Deterministic results** - Same test, same result, every time
- **🤖 AI-friendly format** - JSON-based test cases easy to generate and validate
- **🔄 CI/CD ready** - Perfect for automated pipelines

## 🏗️ System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                   Simulation Testing Architecture                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  📝 Test Specification Layer                                     │
│  ├── AI-Friendly JSON Test Cases                                │
│  ├── Intent Slots (target_app, scope, navigation, etc.)          │
│  └── Expected Behaviors (key_events, completion_reason)         │
│                            ↓                                      │
│  🧪 Test Execution Layer                                         │
│  ├── SimulationRunner (main orchestrator)                       │
│  ├── MockVisionService + PageAnalyzer (intelligent analysis)     │
│  ├── MockActionExecutor (comprehensive recording)                │
│  └── InMemoryTracer (trace visualization)                        │
│                            ↓                                      │
│  ✅ Assertion & Validation Layer                                  │
│  ├── TraceAsserter (automated comparison)                         │
│  ├── Natural Language Events (step_to_nl)                        │
│  └── Violation Detection (must_not_contain)                      │
│                            ↓                                      │
│  📊 Reporting & Integration Layer                                │
│  ├── simtest CLI Tool                                            │
│  ├── SimulationReportGenerator (JSON/HTML)                       │
│  └── CI/CD Integration (GitHub Actions, Pre-commit hooks)         │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## 🚀 Quick Start

### Installation & Setup

```bash
# Verify setup
python scripts/verify_simulation_setup.py

# Run a quick test
python cli/simtest.py run tests/simulation/fixtures/template

# Run full test suite
python cli/simtest.py suite tests/simulation/fixtures
```

### Create Your First Test

**1. Create Test Directory**
```bash
mkdir tests/simulation/fixtures/my_test
```

**2. Create test_case.json**
```json
{
  "test_id": "my_first_test",
  "description": "Test my app navigation",
  "test_dir": "tests/simulation/fixtures/my_test",
  "intent_slots": {
    "target_app": "MyApp",
    "scope": "all_menus",
    "element_handling": "full_interaction",
    "navigation": "adaptive",
    "restore": true,
    "depth": 2
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
    "total_steps_max": 10
  }
}
```

**3. Run Your Test**
```bash
python cli/simtest.py run tests/simulation/fixtures/my_test
```

## 📝 Test Case Format Deep Dive

### Intent Slots (Testing Strategy)

| Slot | Purpose | Values | Impact |
|------|---------|--------|--------|
| **target_app** | Application under test | App name | Scope definition |
| **scope** | Traversal scope | `all_menus`, `target_search`, `static_path` | Navigation strategy |
| **element_handling** | Interaction depth | `full_interaction`, `minimal`, `smart` | Thoroughness vs speed |
| **navigation** | Path selection | `adaptive`, `direct`, `fixed` | Exploration vs efficiency |
| **restore** | State restoration | `true`, `false` | Back navigation |
| **depth** | Maximum depth | `1`, `2`, `3`, `5` | Coverage vs time |

### Expected Behaviors (Assertions)

```json
{
  "expected": {
    "completion_reason": "completed",
    "key_events": [
      "进入 root",
      "点击 SettingsButton"
    ],
    "total_steps_min": 2,
    "total_steps_max": 20,
    "must_not_contain": ["错误", "崩溃", "异常"]
  }
}
```

**Validation Rules:**
- ✅ All `key_events` must appear in trace (subsequence)
- ✅ No `must_not_contain` items may appear
- ✅ Step count must be within range
- ✅ Completion reason must match

## 🧪 Core Test Scenarios

### Included Test Fixtures

#### 1. E2E All Traversal (`e2e_all_traversal`)
**Purpose**: Comprehensive DFS navigation testing  
**Coverage**: Complete menu traversal, restore operations, all interaction types  
**Use Case**: Full app navigation validation

#### 2. E2E Target Found (`e2e_target_found`)
**Purpose**: Target search and early termination  
**Coverage**: Target detection, efficient path finding  
**Use Case**: Finding specific elements quickly

#### 3. E2E Static Path (`e2e_static_path`)
**Purpose**: Predefined workflow testing  
**Coverage**: Fixed path execution, exact sequence validation  
**Use Case**: Critical user journey testing

#### 4. E2E Popup Handling (`e2e_popup_handling`)
**Purpose**: System interruption handling  
**Coverage**: Dialog detection, dismissal, navigation resumption  
**Use Case**: Permission dialogs, alerts, system interruptions

#### 5. E2E Auto Escape (`e2e_auto_escape`)
**Purpose**: Automatic depth management  
**Coverage**: Deep nesting, auto-back navigation, recovery  
**Use Case**: Complex menu structures, recursive navigation

## 🔍 Assertion Engine

### TraceAsserter Features

#### 1. Natural Language Conversion
```python
TraceAsserter.step_to_nl({
    "action_type": "click",
    "target_info": {"element_id": "SettingsButton"}
})
# Returns: "点击 SettingsButton"
```

#### 2. Subsequence Matching
```python
# Expected: ["进入 root", "点击 Settings"]
# Actual: ["进入 root", "滑动 list", "点击 Settings"]
# Result: ✅ PASS (expected is subsequence of actual)
```

#### 3. Violation Detection
```python
"must_not_contain": ["错误", "崩溃", "超时"]
# Automatically checks none of these appear in trace
```

#### 4. Comprehensive Validation
- **Step count validation** - Within expected range
- **Completion reason** - Matches expected outcome
- **Coverage metrics** - Minimum node/feature coverage

## 🛠️ CLI Tool Usage

### simtest Command Reference

#### Run Single Test
```bash
simtest run <test_path> [options]

# Options:
#   --output <file>     Save results to file
#   --format <format>   Output format (json|text)
#   --verbose           Show detailed output
#   --quiet             Minimal output
```

#### Run Test Suite
```bash
simtest suite <tests_dir> [options]

# Options:
#   --report <file>     Generate aggregated report
#   --pattern <pattern> File pattern (default: test_case.json)
#   --config <json>     Configuration override
```

#### View Results
```bash
simtest show <report_path>

# Displays formatted test results with diagnostics
```

## 📊 Performance & Metrics

### Execution Targets

| Metric | Target | How to Measure |
|--------|--------|----------------|
| **Single test** | < 5 seconds | `time simtest run <test>` |
| **Test suite (5 tests)** | < 2 minutes | `time simtest suite <fixtures>` |
| **Page analysis** | < 10ms | PageAnalyzer performance test |
| **Report generation** | < 1 second | Report generation timing |

### Memory & Resources

| Resource | Limit | Monitoring |
|----------|-------|------------|
| **Memory per test** | < 100MB | System monitoring |
| **Disk usage (results)** | < 50MB | `du -sh results/` |
| **CPU usage** | < 50% per test | `top -b -n 1` |

## 🔄 CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Simulation Tests
on: [push, pull_request]

jobs:
  test:
    strategy:
      matrix:
        python-version: [3.10, 3.11]
    
    steps:
    - uses: actions/checkout@v3
    - uses: actions/setup-python@v4
      with:
        python-version: ${{ matrix.python-version }}
    
    - name: Install & Test
      run: |
        pip install -e .
        pip install pytest pytest-cov
        pytest tests/simulation/ --cov=src/simulation
    
    - name: Upload Results
      uses: actions/upload-artifact@v3
      with:
        name: simulation-results
        path: reports/
```

### Pre-commit Integration

```bash
#!/bin/bash
# .git/hooks/pre-commit
echo "🧪 Running simulation tests..."

python cli/simtest.py suite tests/simulation/fixtures --fast

if [ $? -ne 0 ]; then
    echo "❌ Tests failed. Commit aborted."
    exit 1
fi

echo "✅ Tests passed."
```

## 🐛 Troubleshooting

### Common Issues & Solutions

#### 1. Test Not Found
```bash
# Solution: Verify file structure
python scripts/verify_simulation_setup.py
```

#### 2. Assertion Failures
```bash
# Solution: Check detailed results
python scripts/check_simulation_results.py results/failed_test.json
```

#### 3. Performance Issues
```bash
# Solution: Check page analysis caching
python -c "
from src.simulation.page_analyzer import PageAnalyzer
# Verify caching is working
"
```

### Debug Mode

```python
import logging
logging.basicConfig(level=logging.DEBUG)

# Run with detailed logging
result = runner.run()
```

## 📈 Best Practices

### 1. Test Design
- **Focus on intent** - Test *what* should happen, not *how*
- **Be specific but flexible** - Key events, not every step
- **Use appropriate scope** - Match test goal to intent slots

### 2. Assertion Strategy
- **Essential events only** - Don't over-specify
- **Allow variation** - Use ranges for counts/times
- **Meaningful violations** - Test what truly matters

### 3. Performance
- **Reuse fixtures** - Share plan/pages files
- **Optimize fixtures** - Only essential elements
- **Cache analyses** - Leverage PageAnalyzer caching

### 4. Maintenance
- **Version control** - Track test changes
- **Document intent** - Explain *why*, not just *what*
- **Regular updates** - Keep aligned with app changes

## 🎓 Advanced Topics

### Custom Test Runners

```python
from tests.simulation.helpers import SimulationTestRunner

class DomainSpecificRunner(SimulationTestRunner):
    def run_domain_scenario(self, domain_config):
        """Custom domain-specific logic."""
        # Your implementation
        pass
```

### Extended Assertions

```python
from tests.simulation.helpers.assertions import TraceAsserter

class ExtendedAsserter(TraceAsserter):
    @staticmethod
    def assert_performance(trace, max_time_ms):
        """Custom performance assertion."""
        total_time = trace[-1]['timestamp'] - trace[0]['timestamp']
        return total_time <= max_time_ms
```

## 📚 Additional Resources

### Documentation
- **[Testing Framework README](../tests/simulation/README.md)** - Detailed framework guide
- **[Architecture](../ARCHITECTURE.md)** - System architecture
- **[Component Design](../core_business_models.md)** - Data models

### Tools & Scripts
- **[verify_simulation_setup.py](../scripts/verify_simulation_setup.py)** - Environment validation
- **[check_simulation_results.py](../scripts/check_simulation_results.py)** - Result diagnostics

## ❓ FAQ

**Q: How do I test network-dependent features?**  
A: Consider higher-level mocking or integration testing for network features.

**Q: Can I run tests in parallel?**  
A: Yes, independent tests can run in parallel using `pytest-xdist`.

**Q: How do I handle dynamic UI elements?**  
A: Use flexible assertions with patterns rather than exact matches.

**Q: What's the testing limit?**  
A: Tests are limited only by memory and time, not by API costs.

---

**Version:** 1.0.0  
**Last Updated:** 2026-06-03  
**Part of:** Uni-Claw V6.0 Simulation Testing System