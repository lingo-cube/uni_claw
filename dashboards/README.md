# Dashboards Directory

This directory contains various dashboards for visualizing and analyzing traversal results.

## Available Dashboards

### 1. Trace Observatory (`trace_viewer.html` + `trace_server.py`)

A comprehensive distributed tracing visualization dashboard for V6.3 trace data.

**Features:**
- **Trace Browser**: Select and view traces from FileStorage
- **Hierarchical Tree**: Visual trace tree with expand/collapse
- **State Flowchart**: State transition visualization
- **Timeline View**: Chronological action and event timeline
- **AI Calls**: Detailed AI call metrics and latency
- **Error Tracking**: Error identification and analysis
- **Auto-Refresh**: Automatic polling for test run completion
- **Notifications**: Real-time status updates
- **Error Handling**: Graceful API error handling with user feedback

**Usage:**
```bash
python dashboards/trace_server.py [--port 8080] [--trace-dir traces]
# Then open http://localhost:8080
```

**API Endpoints:**
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | HTML dashboard |
| `/api/traces` | GET | List all available traces |
| `/api/trace?id={trace_id}` | GET | Get trace overview |
| `/api/tree?id={trace_id}` | GET | Get hierarchical trace tree |
| `/api/analysis?id={trace_id}` | GET | Get full trace analysis |

**Test Run Integration:**
The dashboard integrates with the test runner API for running tests directly from the interface:
- **Polling**: Automatically polls for active test runs every 2 seconds
- **Auto-Refresh**: Automatically refreshes data when tests complete
- **Notifications**: Shows status messages for test operations
- **Active Runs Display**: Shows currently running test modules

**JavaScript API:**
```javascript
// Trigger a test run
await triggerTestRun('module_name');

// Cancel an active run
await cancelTestRun('run_id');

// Manual refresh
await refreshAll();
```

### 2. Simple Dashboard (`simple_dashboard.py`)

A standalone dashboard with a built-in web server that displays:

A standalone dashboard with a built-in web server that displays:
- **Overview**: Session statistics and recent traversals
- **Traces**: Distributed trace visualization with tree structure
- **Metrics**: Performance metrics per component (call count, duration, success rate)
- **Logs**: Structured logs from traversal sessions

**Features:**
- Self-contained server on port 8002
- Reads from `.results/sessions/`, `.traces/`, and `.logs/` directories
- Real-time data visualization with reload capability

**Usage:**
```bash
python dashboards/simple_dashboard.py
# Then open http://127.0.0.1:8002
```

### 2. Analysis Server (`analysis_server.py`)

An HTTP server that integrates with `src.analysis` modules to provide:
- Trace analysis and visualization
- Component performance metrics
- Tree building from traversal results
- AI metrics collection

**Features:**
- Uses `TraceAnalyzer`, `MetricsCollector`, and `TraversalTreeBuilder` from src.analysis
- Configurable host, port, and trace directory
- API endpoints for data, metrics, and tree data

**Usage:**
```bash
python dashboards/analysis_server.py
# Then open http://127.0.0.1:8000
```

### 3. Analysis Dashboard (`scripts/analysis_dashboard.py`)

A more advanced dashboard that integrates with `src.analysis`:
- Uses the official `src.analysis.server` module
- Configurable host, port, and trace directory
- Better integration with the analysis framework

**Usage:**
```bash
python scripts/analysis_dashboard.py
# Then open http://127.0.0.1:8000
```

### 4. Test Report Server (`test_report_server.py`)

A comprehensive HTTP server for test results visualization and management.

**Features:**
- **Test Results DataSource**: Load and cache test results from standardized JSON files
- **Test Results Analyzer**: Aggregate statistics, identify failures, check freshness
- **Test Runner API**: Trigger and manage test runs via HTTP endpoints
- **Embedded HTML Dashboard**: Auto-refreshing web interface with status color coding
- **Module Status Grid**: Visual breakdown of test results per module
- **Failure Details**: Table showing all failing tests with error messages
- **Test Triggering**: Re-run individual modules or all tests from the web interface
- **Freshness Tracking**: Identify stale test results (>48 hours for unit tests)
- **Result Validation**: Schema validation for test result files

**API Endpoints:**

#### Data Endpoints
| Endpoint | Method | Query Params | Description |
|----------|--------|--------------|-------------|
| `/` | GET | - | HTML dashboard |
| `/api/results` | GET | `module` (optional) | Per-module test results |
| `/api/aggregate` | GET | - | Aggregated statistics |
| `/api/failures` | GET | - | Failing tests grouped by module |
| `/api/freshness` | GET | - | Freshness report |
| `/api/runs` | GET | - | List active and recent runs |
| `/api/run-status/{run_id}` | GET | - | Status of specific run |

#### Control Endpoints
| Endpoint | Method | Body | Description |
|----------|--------|------|-------------|
| `/api/trigger` | POST | `{"module": "name"}` | Trigger test run |
| `/api/cancel` | POST | `{"run_id": "id"}` | Cancel active run |
| `/api/refresh` | POST | - | Invalidate cache |

**Usage:**
```bash
# Start with defaults (port 8003)
python dashboards/test_report_server.py

# Custom port
python dashboards/test_report_server.py --port 9000

# Custom results directory
python dashboards/test_report_server.py --results-dir ../other_project/test_results

# Custom cache TTL
python dashboards/test_report_server.py --cache-ttl 60
```

**API Usage Examples:**

```bash
# Get all test results
curl http://localhost:8003/api/results

# Get results for specific module
curl http://localhost:8003/api/results?module=trace

# Get aggregate statistics
curl http://localhost:8003/api/aggregate

# Get failing tests
curl http://localhost:8003/api/failures

# Trigger a test run
curl -X POST http://localhost:8003/api/trigger \
  -H "Content-Type: application/json" \
  -d '{"module": "trace"}'

# Get status of a run
curl http://localhost:8003/api/run-status/run_abc123

# Cancel a run
curl -X POST http://localhost:8003/api/cancel \
  -H "Content-Type: application/json" \
  -d '{"run_id": "run_abc123"}'

# Refresh cache
curl -X POST http://localhost:8003/api/refresh
```

**Python Client Example:**
```python
import requests

BASE_URL = "http://localhost:8003"

# Get aggregate stats
stats = requests.get(f"{BASE_URL}/api/aggregate").json()
print(f"Pass rate: {stats['overall_pass_rate']}%")

# Trigger test run
response = requests.post(
    f"{BASE_URL}/api/trigger",
    json={"module": "trace"}
)
run_id = response.json()["run_id"]

# Poll for completion
while True:
    status = requests.get(f"{BASE_URL}/api/run-status/{run_id}").json()
    if status["status"] in ["completed", "failed", "cancelled"]:
        break
    time.sleep(2)

print(f"Test run {status['status']} in {status['duration']:.2f}s")
```

**JavaScript Client Example:**
```javascript
const API = '';

async function runTestsAndCheckResults() {
  // Trigger test
  const trigger = await fetch(`${API}/api/trigger`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ module: 'trace' })
  });
  const { run_id } = await trigger.json();

  // Poll for completion
  let status;
  do {
    await new Promise(r => setTimeout(r, 2000));
    const res = await fetch(`${API}/api/run-status/${run_id}`);
    status = await res.json();
  } while (status.status === 'running');

  // Get final results
  const results = await fetch(`${API}/api/results?module=trace`)
    .then(r => r.json());

  return { status, results: results.trace };
}
```

### 5. Legacy Simple Dashboard (`simple.html`)

A minimal static HTML dashboard for basic data viewing. This is a legacy file kept for reference.

## Data Sources

All dashboards read from the following directories:

| Directory | Purpose | Format |
|----------|---------|--------|
| `.results/sessions/` | Traversal results | JSON files |
| `.traces/` | Distributed traces | JSONL files |
| `.logs/` | Structured logs | JSONL files |
| `test_results/` | Test results | JSON files (standardized) |

## Running Dashboards

### Quick Start
```bash
# Simple dashboard (easiest)
python dashboards/simple_dashboard.py

# Analysis dashboard (with src.analysis integration)
python scripts/analysis_dashboard.py

# Test report server (comprehensive test visualization)
python dashboards/test_report_server.py
```

### Custom Port
```bash
# Simple dashboard - edit the PORT variable in the file
# Analysis dashboard - use --port flag
python scripts/analysis_dashboard.py --port 9000
```

## Development

- **Simple Dashboard**: Single-file, self-contained, easy to modify
- **Analysis Dashboard**: Integrated with `src.analysis` module, more extensible
- **Trace Observatory**: Modern, reactive dashboard with polling and auto-refresh
- **Test Report Server**: Full-featured test management with HTTP API

For adding new visualizations, prefer extending the Analysis Dashboard or Trace Observatory as they have better integration with the analysis framework.

## Test Integration

### Dashboard Polling Mechanism

The Trace Observatory implements automatic polling for test runs:

```javascript
// Polling runs every 2 seconds
async function pollTestRuns() {
  while (pollingActive) {
    const runs = await fetchJSON('/api/runs');
    handleTestRunsUpdate(runs);
    await sleep(2000);
  }
}
```

### Auto-Refresh on Completion

When a test run completes, the dashboard automatically refreshes:

```javascript
function handleTestRunStatus(run) {
  const prevStatus = activeTestRuns.get(run.run_id);
  // Detect state change from running to completed
  if (prevStatus?.status === 'running' &&
      run.status === 'completed') {
    // Refresh all data
    setTimeout(refreshAll, 500);
  }
}
```

### Error Handling

All dashboards implement consistent error handling:

**Client-side (JavaScript):**
```javascript
// Enhanced fetch with error handling
async function fetchJSON(url, options = {}) {
  try {
    const response = await fetch(url, options);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    if (data.error) throw new Error(data.error);
    return data;
  } catch (error) {
    showNotification(`Error: ${error.message}`, 'error');
    throw error;
  }
}
```

**Server-side (Python):**
```python
def _serve_error(self, code: int, message: str):
    """Serve error response."""
    self.send_response(code)
    self.send_header("Content-Type", "application/json")
    self.send_header("Access-Control-Allow-Origin", "*")
    self.end_headers()
    self.wfile.write(json.dumps({"error": message}).encode("utf-8"))
```

### Notification System

The dashboard provides real-time feedback:

```javascript
// Show notification with auto-dismiss
showNotification('Test run completed', 'success');
showNotification('Failed to load data', 'error');
showNotification('Running tests...', 'info');
```

Notifications automatically dismiss after 3 seconds and include slide-in animations.
