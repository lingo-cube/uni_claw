# Dashboards Directory

This directory contains various dashboards for visualizing and analyzing traversal results.

## Available Dashboards

### 1. Simple Dashboard (`simple_dashboard.py`)

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

### 3. Legacy Simple Dashboard (`simple.html`)

A minimal static HTML dashboard for basic data viewing. This is a legacy file kept for reference.

## Data Sources

All dashboards read from the following directories:

| Directory | Purpose | Format |
|----------|---------|--------|
| `.results/sessions/` | Traversal results | JSON files |
| `.traces/` | Distributed traces | JSONL files |
| `.logs/` | Structured logs | JSONL files |

## Running Dashboards

### Quick Start
```bash
# Simple dashboard (easiest)
python dashboards/simple_dashboard.py

# Analysis dashboard (with src.analysis integration)
python scripts/analysis_dashboard.py
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

For adding new visualizations, prefer extending the Analysis Dashboard as it has better integration with the analysis framework.
