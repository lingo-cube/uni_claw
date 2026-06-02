#!/usr/bin/env python3
"""Simple HTTP server for traversal analysis dashboard (built-in http.server only)."""

import argparse
import http.server
import json
import socketserver
import sys
from pathlib import Path
from typing import Dict

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from src.analysis.trace_analyzer import TraceAnalyzer
from src.analysis.metrics import get_metrics_collector
from src.analysis.tree import TraversalTreeBuilder


class AnalysisRequestHandler(http.server.SimpleHTTPRequestHandler):
    """HTTP request handler that serves dashboard and API endpoints."""

    def __init__(self, *args, trace_dir: Path, analyzer, metrics, tree_builder, **kwargs):
        self.trace_dir = trace_dir
        self.analyzer = analyzer
        self.metrics = metrics
        self.tree_builder = tree_builder
        super().__init__(*args, **kwargs)

    def do_GET(self):
        """Handle GET requests."""
        if self.path == "/":
            self.path = "/dashboard.html"
        elif self.path == "/api/data":
            self.serve_dashboard_data()
            return
        elif self.path == "/api/metrics":
            self.serve_metrics()
            return
        elif self.path == "/api/tree":
            self.serve_tree()
            return

        # Try to serve static files from results directories
        if self.path.startswith("/api/results/"):
            self.serve_result_file(self.path[13:])
            return

        # Default to SimpleHTTPRequestHandler behavior
        return http.server.SimpleHTTPRequestHandler.do_GET(self)

    def serve_dashboard_data(self):
        """Serve dashboard data as JSON."""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        sessions = self.analyzer.load_all_traces()
        perf = self.analyzer.analyze_component_performance()
        slowest = self.analyzer.get_slowest_operations(10)

        data = {
            "sessions": [
                {
                    "id": s.session_id,
                    "start_time": s.start_time,
                    "component_count": len(s.components),
                    "span_count": len(s.spans),
                }
                for s in sessions
            ],
            "performance": perf,
            "slowest": slowest,
            "metrics": {
                "ai_calls": self.metrics.get_ai_metrics_summary(),
            }
        }

        self.wfile.write(json.dumps(data, ensure_ascii=False).encode())

    def serve_metrics(self):
        """Serve metrics as JSON."""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        data = self.metrics.get_ai_metrics_summary()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode())

    def serve_tree(self):
        """Serve tree data as JSON."""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        # Load from results if available
        results_dir = Path(".results/sessions")
        if results_dir.exists():
            results = list(results_dir.glob("*.json"))
            if results:
                with open(results[0], "r") as f:
                    result_data = json.load(f)
                    tree = self.tree_builder.build_from_visited_items(
                        result_data.get("visited_items", [])
                    )
                    self.wfile.write(tree.to_json().encode())
                    return

        # Fallback
        self.wfile.write(json.dumps({"nodes": [], "edges": []}).encode())

    def serve_result_file(self, filename: str):
        """Serve a result file."""
        results_dir = Path(".results")
        file_path = results_dir / filename

        if file_path.exists():
            self.send_response(200)
            if filename.endswith(".json"):
                self.send_header("Content-Type", "application/json")
            elif filename.endswith(".html"):
                self.send_header("Content-Type", "text/html")
            else:
                self.send_header("Content-Type", "text/plain")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            with open(file_path, "rb") as f:
                self.wfile.write(f.read())
        else:
            self.send_response(404)
            self.end_headers()
            self.wfile.write(b"File not found")

    def log_message(self, format, *args):
        """Override to use simpler logging."""
        print(f"[{self.log_date_time_string()}] {format % args}")


def create_handler(trace_dir):
    """Create handler with bound dependencies."""
    analyzer = TraceAnalyzer(trace_dir)
    metrics = get_metrics_collector()
    tree_builder = TraversalTreeBuilder()

    def handler(*args, **kwargs):
        AnalysisRequestHandler(*args, trace_dir=trace_dir, analyzer=analyzer,
                              metrics=metrics, tree_builder=tree_builder, **kwargs)

    return handler


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(description="Simple Traversal Analysis Dashboard")
    parser.add_argument(
        "--trace-dir",
        type=str,
        default=".traces",
        help="Directory containing trace files (default: .traces)"
    )
    parser.add_argument(
        "--host",
        type=str,
        default="127.0.0.1",
        help="Host to bind to (default: 127.0.0.1)"
    )
    parser.add_argument(
        "--port",
        type=int,
        default=8000,
        help="Port to bind to (default: 8000)"
    )

    args = parser.parse_args()

    trace_dir = Path(args.trace_dir)

    if not trace_dir.exists():
        print(f"⚠️  Trace directory '{trace_dir}' does not exist. Creating it...")
        trace_dir.mkdir(parents=True, exist_ok=True)

    # Note: Dashboard HTML is served from src/analysis/dashboard.html
    # If you want a simple dashboard, use: python dashboards/simple_dashboard.py

    handler = create_handler(trace_dir)

    with socketserver.TCPServer((args.host, args.port), handler) as httpd:
        print(f"\n{'=' * 60}")
        print(f"🚀 Analysis Dashboard Running")
        print(f"{'=' * 60}")
        print(f"URL: http://{args.host}:{args.port}")
        print(f"Trace directory: {trace_dir.absolute()}")
        print(f"{'=' * 60}\n")
        print("Available endpoints:")
        print(f"  - http://{args.host}:{args.port}/           - Dashboard")
        print(f"  - http://{args.host}:{args.port}/api/data    - All data")
        print(f"  - http://{args.host}:{args.port}/api/metrics - Metrics")
        print(f"  - http://{args.host}:{args.port}/api/tree    - Tree data")
        print(f"{'=' * 60}\n")

        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n👋 Server stopped.")


def create_simple_dashboard(path: Path):
    """Create a simple dashboard HTML file."""
    html = """<!DOCTYPE html>
<html>
<head>
    <title>Traversal Analysis Dashboard</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; }
        h1 { color: #333; }
        .card { background: #f9f9f9; padding: 15px; margin: 10px 0; border-radius: 4px; border-left: 4px solid #007bff; }
        .metric { display: inline-block; margin: 10px 20px; }
        .metric-value { font-size: 24px; font-weight: bold; color: #007bff; }
        .metric-label { color: #666; font-size: 14px; }
        button { background: #007bff; color: white; border: none; padding: 10px 20px; border-radius: 4px; cursor: pointer; margin: 5px; }
        button:hover { background: #0056b3; }
        pre { background: #f0f0f0; padding: 10px; border-radius: 4px; overflow-x: auto; }
        #data { margin-top: 20px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>🔍 Traversal Analysis Dashboard</h1>

        <div style="margin: 20px 0;">
            <button onclick="loadData()">🔄 Reload Data</button>
            <button onclick="loadMetrics()">📊 Load Metrics</button>
            <button onclick="loadTree()">🌳 Load Tree</button>
        </div>

        <div id="metrics" style="margin: 20px 0;"></div>

        <div id="data"></div>
    </div>

    <script>
        async function loadData() {
            const response = await fetch('/api/data');
            const data = await response.json();
            document.getElementById('data').innerHTML = '<h3>All Data</h3><pre>' + JSON.stringify(data, null, 2) + '</pre>';
        }

        async function loadMetrics() {
            const response = await fetch('/api/metrics');
            const data = await response.json();

            let html = '<div class="card"><h3>📊 AI Metrics</h3>';
            for (const [key, metrics] of Object.entries(data)) {
                html += '<div class="metric">';
                html += '<div class="metric-value">' + metrics.total_calls + '</div>';
                html += '<div class="metric-label">' + key + '</div>';
                html += '</div>';
            }
            html += '</div>';
            document.getElementById('metrics').innerHTML = html;
        }

        async function loadTree() {
            const response = await fetch('/api/tree');
            const data = await response.json();
            document.getElementById('data').innerHTML = '<h3>Traversal Tree</h3><pre>' + JSON.stringify(data, null, 2) + '</pre>';
        }

        // Auto-load on start
        loadData();
    </script>
</body>
</html>"""
    path.write_text(html)


if __name__ == "__main__":
    main()
