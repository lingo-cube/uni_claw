# Test Report Server - Implementation Guide

**Version**: 1.0
**Date**: 2026-06-06
**Status**: Ready for Implementation

---

## Quick Reference

### File Structure

```
dashboards/
├── test_report_server.py          # Main server implementation
├── test_report_dashboard.html     # Web dashboard (embedded or separate)
└── test_report_server_implementation_guide.md  # This file

docs/architecture/modules/
├── test-report-server-design.md  # Detailed design document
└── test-report-server-mermaid.md  # Architecture diagrams
```

### Component Summary

| Component | Lines (est) | Dependencies | Complexity |
|-----------|-------------|--------------|------------|
| TestResultsDataSource | ~200 | pathlib, json, time | Medium |
| TestResultValidator | ~100 | json, datetime | Low |
| TestResultsAnalyzer | ~250 | typing, collections | Medium |
| TestRunnerAPI | ~200 | subprocess, uuid | Medium |
| RequestHandler | ~300 | http.server, urllib | Medium |
| Dashboard HTML | ~400 | HTML, CSS, JS | Medium |
| **Total** | **~1450** | - | **Medium** |

---

## Implementation Checklist

### Phase 1: Core Data Layer (2 hours)

- [ ] Create `TestResultValidator` class
  - [ ] `validate_required_fields()` method
  - [ ] `validate_summary_counts()` method
  - [ ] `validate_timestamp_format()` method
  - [ ] Unit tests

- [ ] Create `TestResultsDataSource` class
  - [ ] `__init__()` with results_dir and cache_ttl
  - [ ] `load_results()` method
  - [ ] `get_cached_data()` method
  - [ ] `invalidate_cache()` method
  - [ ] `list_modules()` method
  - [ ] `validate_schema()` method
  - [ ] Unit tests

**Acceptance Tests**:
```python
# Test file loading
ds = TestResultsDataSource(Path("test_results"))
results = ds.load_results()
assert "trace" in results
assert results["trace"]["summary"]["total"] == 123

# Test caching
cached = ds.get_cached_data()
assert cached is not None  # Fresh cache

# Test cache expiration
ds.cache_ttl = 0
cached = ds.get_cached_data()
assert cached is None  # Expired cache
```

### Phase 2: Analysis Layer (2 hours)

- [ ] Create `TestResultsAnalyzer` class
  - [ ] `__init__()` with data_source
  - [ ] `aggregate_statistics()` method
  - [ ] `calculate_pass_rate()` method
  - [ ] `identify_failing_tests()` method
  - [ ] `get_freshness_report()` method
  - [ ] `detect_trends()` method (optional v1)
  - [ ] `generate_summary()` method
  - [ ] Unit tests

**Acceptance Tests**:
```python
# Test aggregation
analyzer = TestResultsAnalyzer(data_source)
stats = analyzer.aggregate_statistics()
assert stats["total_tests"] == 241
assert stats["overall_pass_rate"] == 0.86

# Test failures
failures = analyzer.identify_failing_tests()
assert "graph_engine" in failures
assert len(failures["graph_engine"]) > 0

# Test freshness
freshness = analyzer.get_freshness_report()
assert "trace" in freshness["fresh_modules"]
```

### Phase 3: Test Runner Integration (2 hours)

- [ ] Create `TestRunnerAPI` class
  - [ ] `__init__()` with project_root
  - [ ] `run_module_tests()` method
  - [ ] `run_all_tests()` method
  - [ ] `cancel_run()` method
  - [ ] `get_run_status()` method
  - [ ] `list_active_runs()` method
  - [ ] Background thread for monitoring (optional)
  - [ ] Integration tests

**Acceptance Tests**:
```python
# Test triggering
runner = TestRunnerAPI(Path("."))
run_id = runner.run_module_tests("trace")
assert run_id.startswith("run_")

# Test status
status = runner.get_run_status(run_id)
assert status["status"] in ["running", "completed"]
```

### Phase 4: HTTP Server (3 hours)

- [ ] Create `TestReportRequestHandler` class
  - [ ] Class variables for dependencies
  - [ ] `do_GET()` method with routing
  - [ ] `do_POST()` method with routing
  - [ ] `_serve_results()` endpoint
  - [ ] `_serve_aggregate()` endpoint
  - [ ] `_serve_failures()` endpoint
  - [ ] `_serve_freshness()` endpoint
  - [ ] `_serve_runs()` endpoint
  - [ ] `_serve_run_status()` endpoint
  - [ ] `_handle_trigger()` endpoint
  - [ ] `_handle_cancel()` endpoint
  - [ ] `_serve_error()` helper
  - [ ] `_serve_json()` helper
  - [ ] `log_message()` override

- [ ] Create `main()` function
  - [ ] Argument parsing
  - [ ] Component initialization
  - [ ] Handler factory
  - [ ] Server startup
  - [ ] Graceful shutdown

**Acceptance Tests**:
```bash
# Start server
python dashboards/test_report_server.py --port 9999

# Test endpoints
curl http://localhost:9999/api/results
curl http://localhost:9999/api/aggregate
curl http://localhost:9999/api/failures
```

### Phase 5: Web Dashboard (3 hours)

- [ ] Create HTML structure
  - [ ] Header with title
  - [ ] Overall metrics card
  - [ ] Module breakdown section
  - [ ] Failing tests table
  - [ ] Active runs panel
  - [ ] Control buttons

- [ ] Add CSS styling
  - [ ] Modern, clean design
  - [ ] Status colors (green, yellow, red)
  - [ ] Responsive layout
  - [ ] Card styling

- [ ] Implement JavaScript
  - [ ] API client functions
  - [ ] Data loading functions
  - [ ] Auto-refresh logic
  - [ ] Button handlers
  - [ ] Error handling

**Acceptance Tests**:
```bash
# Open dashboard
open http://localhost:8003/

# Verify:
# - Dashboard loads without errors
# - Data displays correctly
# - Auto-refresh works
# - Buttons trigger API calls
```

### Phase 6: Integration (2 hours)

- [ ] End-to-end testing
- [ ] Performance testing
- [ ] Documentation updates
- [ ] README.md updates

---

## Code Skeletons

### TestResultsDataSource

```python
class TestResultsDataSource:
    """Data source for standardized test results."""

    def __init__(self, results_dir: Path, cache_ttl: int = 30):
        self.results_dir = results_dir
        self.cache_ttl = cache_ttl
        self._cache: Dict[str, Tuple[dict, float]] = {}
        self._validator = TestResultValidator()

    def load_results(self, module: Optional[str] = None) -> Dict[str, Any]:
        """Load test results for a module or all modules."""
        # Implementation...
        pass

    # ... other methods
```

### TestResultsAnalyzer

```python
class TestResultsAnalyzer:
    """Analyzer for test results data."""

    def __init__(self, data_source: TestResultsDataSource):
        self.data_source = data_source

    def aggregate_statistics(self) -> Dict[str, Any]:
        """Aggregate statistics across all modules."""
        results = self.data_source.load_results()
        total_tests = sum(r["summary"]["total"] for r in results.values())
        # ... calculation logic
        return {
            "total_modules": len(results),
            "total_tests": total_tests,
            # ... more fields
        }
```

### TestRunnerAPI

```python
class TestRunnerAPI:
    """API for triggering and managing test runs."""

    def __init__(self, project_root: Path):
        self.project_root = project_root
        self.test_runner_path = project_root / ".claude/skills/module-test/test_runner.py"
        self._active_runs: Dict[str, subprocess.Popen] = {}
        self._run_status: Dict[str, dict] = {}

    def run_module_tests(self, module: str) -> str:
        """Run tests for a specific module."""
        run_id = f"run_{ulid.new()}"
        cmd = [sys.executable, str(self.test_runner_path), module]
        process = subprocess.Popen(cmd, cwd=self.project_root)
        self._active_runs[run_id] = process
        return run_id
```

### RequestHandler

```python
class TestReportRequestHandler(SimpleHTTPRequestHandler):
    """HTTP request handler for test report server."""

    data_source: TestResultsDataSource = None
    analyzer: TestResultsAnalyzer = None
    test_runner: TestRunnerAPI = None

    def do_GET(self):
        """Handle GET requests."""
        if self.path == "/api/results":
            self._serve_results()
        elif self.path == "/api/aggregate":
            self._serve_aggregate()
        # ... more routes

    def _serve_results(self):
        """Serve per-module test results."""
        params = parse_qs(urlparse(self.path).query)
        module = params.get("module", [None])[0]
        results = self.data_source.load_results(module)
        self._serve_json(results)

    def _serve_json(self, data: dict):
        """Serve JSON response."""
        body = json.dumps(data, default=str, ensure_ascii=False).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)
```

---

## Testing Strategy

### Unit Tests

Create `tests/test_test_report_server.py`:

```python
import pytest
from pathlib import Path
from dashboards.test_report_server import (
    TestResultsDataSource,
    TestResultsAnalyzer,
    TestRunnerAPI
)

class TestTestResultsDataSource:
    def test_load_results(self, tmp_path):
        # Create test JSON file
        (tmp_path / "trace_unit.json").write_text('{"module":"trace",...}')
        ds = TestResultsDataSource(tmp_path)
        results = ds.load_results()
        assert "trace" in results

    def test_cache_expiration(self, tmp_path):
        ds = TestResultsDataSource(tmp_path, cache_ttl=0)
        # Cache should expire immediately
        assert ds.get_cached_data() is None

class TestTestResultsAnalyzer:
    def test_aggregate_statistics(self, tmp_path):
        # Setup data source with test data
        ds = TestResultsDataSource(tmp_path)
        analyzer = TestResultsAnalyzer(ds)
        stats = analyzer.aggregate_statistics()
        assert "total_tests" in stats
```

### Integration Tests

```python
def test_end_to_end_workflow():
    # Start server in background
    # Trigger test run
    # Poll for completion
    # Verify results
    pass
```

---

## Performance Benchmarks

### Expected Performance

| Operation | Target | Max |
|-----------|--------|-----|
| Load results (10 files) | <50ms | 100ms |
| Aggregate statistics | <10ms | 50ms |
| API response time | <100ms | 200ms |
| Dashboard load | <500ms | 1s |

### Load Testing

```python
# Test with 100 test result files
def test_load_performance():
    ds = TestResultsDataSource(results_dir)
    start = time.time()
    results = ds.load_results()
    duration = time.time() - start
    assert duration < 0.5  # 500ms max
```

---

## Configuration Examples

### Development

```bash
# Start with defaults
python dashboards/test_report_server.py

# Custom port
python dashboards/test_report_server.py --port 9000

# Different results directory
python dashboards/test_report_server.py --results-dir ../other_project/test_results
```

### Production (Docker)

```dockerfile
FROM python:3.10-slim
WORKDIR /app
COPY . .
RUN pip install pytest pytest-json-report pytest-cov
EXPOSE 8003
CMD ["python", "dashboards/test_report_server.py", "--host", "0.0.0.0"]
```

---

## Troubleshooting

### Common Issues

**Issue**: Port already in use
```bash
# Solution: Use different port
python dashboards/test_report_server.py --port 8004
```

**Issue**: No test results found
```bash
# Solution: Run tests first
python .claude/skills/module-test/test_runner.py trace
```

**Issue**: Test runner not found
```bash
# Solution: Verify path
ls .claude/skills/module-test/test_runner.py
```

---

## Next Steps

1. Start Phase 1 implementation
2. Create unit tests as you go
3. Test each phase before moving to next
4. Update documentation with any deviations
5. Perform end-to-end testing

---

**Status**: ✅ Design complete, ready for implementation
