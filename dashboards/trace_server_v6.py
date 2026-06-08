#!/usr/bin/env python3
"""
Enhanced Trace Visualization Server for V6.3+ Distributed Tracing

Serves trace analysis data via HTTP API with extended endpoints for:
- Span chain visualization
- Performance metrics
- Coverage analysis
- Error tracking

Usage:
    python dashboards/trace_server_v6.py [--port 8080] [--trace-dir traces]
"""

import json
import os
import sys
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from typing import Any, Dict, List, Optional
from urllib.parse import parse_qs, urlparse
from datetime import datetime

# Add project root to path
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from src.trace.storage import FileStorage
from src.trace.analyzer import TraceAnalyzer, build_tree


class TraceAPIHandler(SimpleHTTPRequestHandler):
    """Enhanced HTTP handler serving trace analysis API and static files."""

    storage: FileStorage = None

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        params = parse_qs(parsed.query)

        # API routes
        if path == "/api/traces":
            self._serve_json(self._list_traces())
        elif path == "/api/trace":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_trace_overview(tid))
        elif path == "/api/tree":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_tree(tid))
        elif path == "/api/operation-tree":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_operation_tree(tid))
        elif path == "/api/analysis":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_analysis(tid))
        elif path == "/api/span-chain":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_span_chain(tid))
        elif path == "/api/performance":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_performance(tid))
        elif path == "/api/errors":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_errors(tid))
        elif path in ["/", "", "/index.html"]:
            # Serve the V6 enhanced viewer
            self.path = "/trace_viewer_v6.html"
            super().do_GET()
        else:
            # Serve static files (CSS, JS, etc.)
            super().do_GET()

    def _list_traces(self) -> Dict[str, Any]:
        """List all available traces with metadata."""
        traces = []
        base = self.storage._base_dir
        if base.exists():
            for d in sorted(base.iterdir(), reverse=True):
                if d.is_dir() and not d.name.startswith(".") and d.name != "archive":
                    session = self.storage.read_session(d.name)
                    nodes = self.storage.read(d.name)

                    # Determine if it's a simulation trace
                    is_sim = False
                    if session and session.get("traversal_mode") == "simulation":
                        is_sim = True

                    traces.append({
                        "trace_id": d.name,
                        "node_count": len(nodes),
                        "session": session,
                        "is_simulation": is_sim,
                        "created_at": self._get_dir_time(d),
                    })
        return {"traces": traces}

    def _get_trace_overview(self, trace_id: str) -> Dict[str, Any]:
        """Get overview of a single trace."""
        nodes = self.storage.read(trace_id)
        session = self.storage.read_session(trace_id) or {}
        analyzer = TraceAnalyzer(nodes)

        return {
            "trace_id": trace_id,
            "session": session,
            "node_count": len(nodes),
            "node_types": self._count_node_types(nodes),
            "span_types": self._count_span_types(nodes),
            "time_analysis": analyzer.extract_time_analysis(),
            "error_stats": analyzer.extract_error_statistics(),
            "coverage": analyzer.extract_coverage_analysis(),
        }

    def _get_tree(self, trace_id: str) -> Dict[str, Any]:
        """Get hierarchical trace tree."""
        nodes = self.storage.read(trace_id)
        root = build_tree(nodes)
        if root is None:
            return {"error": "No session node found"}
        return {"tree": self._node_to_tree(root), "trace_id": trace_id}

    def _get_operation_tree(self, trace_id: str) -> Dict[str, Any]:
        """Get element-level operation tree."""
        nodes = self.storage.read(trace_id)
        analyzer = TraceAnalyzer(nodes)
        return analyzer.extract_operation_tree()

    def _get_analysis(self, trace_id: str) -> Dict[str, Any]:
        """Get full analysis data with enhanced metrics."""
        nodes = self.storage.read(trace_id)
        analyzer = TraceAnalyzer(nodes)
        session = self.storage.read_session(trace_id) or {}

        return {
            "trace_id": trace_id,
            "session": session,
            "page_tree": analyzer.extract_page_tree(),
            "state_sequence": analyzer.extract_state_sequence(),
            "ai_calls": analyzer.extract_ai_calls(),
            "action_sequence": analyzer.extract_action_sequence(),
            "error_stats": analyzer.extract_error_statistics(),
            "time_analysis": analyzer.extract_time_analysis(),
            "coverage": analyzer.extract_coverage_analysis(),
            "_raw_nodes": nodes,  # For span chain visualization
        }

    def _get_span_chain(self, trace_id: str) -> Dict[str, Any]:
        """Get span chain with full call hierarchy."""
        nodes = self.storage.read(trace_id)
        root = build_tree(nodes)

        def extract_chain(node, depth=0):
            result = {
                "span_id": getattr(node, "span_id", ""),
                "node_type": getattr(node, "node_type", ""),
                "span_type": getattr(node, "span_type", ""),
                "depth": depth,
                "timestamp": getattr(node, "timestamp", 0),
            }

            # Add type-specific data
            if hasattr(node, "action"):
                result["action"] = node.action
            if hasattr(node, "target"):
                result["target"] = node.target
            if hasattr(node, "duration_ms"):
                result["duration_ms"] = node.duration_ms
            if hasattr(node, "latency_ms"):
                result["latency_ms"] = node.latency_ms
            if hasattr(node, "status"):
                result["status"] = node.status

            # Add children
            children = getattr(node, "children", [])
            if children:
                result["children"] = [extract_chain(c, depth + 1) for c in children]

            return result

        if root:
            return {"trace_id": trace_id, "chain": extract_chain(root)}
        return {"error": "No trace data found"}

    def _get_performance(self, trace_id: str) -> Dict[str, Any]:
        """Get detailed performance metrics."""
        nodes = self.storage.read(trace_id)
        analyzer = TraceAnalyzer(nodes)

        time_analysis = analyzer.extract_time_analysis()
        error_stats = analyzer.extract_error_statistics()

        return {
            "trace_id": trace_id,
            "time_analysis": time_analysis,
            "error_stats": error_stats,
            "slowest_spans": self._get_slowest_spans(nodes, limit=10),
            "span_count_by_type": self._count_span_types(nodes),
        }

    def _get_errors(self, trace_id: str) -> Dict[str, Any]:
        """Get detailed error information."""
        nodes = self.storage.read(trace_id)
        analyzer = TraceAnalyzer(nodes)

        error_stats = analyzer.extract_error_statistics()

        # Group errors by type with details
        errors_by_type = {}
        for n in nodes:
            if hasattr(n, "span_type") and n.span_type == "error":
                error_type = getattr(n, "error_type", "unknown")
                if error_type not in errors_by_type:
                    errors_by_type[error_type] = []
                errors_by_type[error_type].append({
                    "span_id": getattr(n, "span_id", ""),
                    "error_message": getattr(n, "error_message", ""),
                    "timestamp": getattr(n, "timestamp", 0),
                    "severity": getattr(n, "severity", "error"),
                })

        return {
            "trace_id": trace_id,
            "error_stats": error_stats,
            "errors_by_type": errors_by_type,
        }

    def _node_to_tree(self, node) -> Dict[str, Any]:
        """Recursively convert a trace node to a tree dict."""
        result = {
            "span_id": getattr(node, "span_id", ""),
            "node_type": getattr(node, "node_type", ""),
            "timestamp": getattr(node, "timestamp", 0),
        }

        if hasattr(node, "span_type"):
            result["span_type"] = node.span_type
        if hasattr(node, "node_id") and node.node_id:
            result["label"] = node.node_id
        if hasattr(node, "step_type") and node.step_type:
            result["label"] = node.step_type
        if hasattr(node, "action") and node.action:
            result["label"] = f"{node.action} → {getattr(node, 'target', '')}"
        if hasattr(node, "status"):
            result["status"] = node.status
        if hasattr(node, "page_path") and node.page_path:
            result["page_path"] = node.page_path
        if hasattr(node, "children") and node.children:
            result["children"] = [
                self._node_to_tree(c) for c in node.children
            ]

        return result

    def _count_node_types(self, nodes) -> Dict[str, int]:
        counts = {}
        for n in nodes:
            nt = getattr(n, "node_type", "")
            if nt:
                counts[nt] = counts.get(nt, 0) + 1
        return counts

    def _count_span_types(self, nodes) -> Dict[str, int]:
        counts = {}
        for n in nodes:
            st = getattr(n, "span_type", "")
            if st:
                counts[st] = counts.get(st, 0) + 1
        return counts

    def _get_slowest_spans(self, nodes, limit=10) -> List[Dict]:
        """Get the slowest spans by duration."""
        spans_with_time = []
        for n in nodes:
            duration = getattr(n, "duration_ms", None) or getattr(n, "latency_ms", None)
            if duration:
                spans_with_time.append({
                    "span_id": getattr(n, "span_id", ""),
                    "span_type": getattr(n, "span_type", ""),
                    "duration_ms": duration,
                    "timestamp": getattr(n, "timestamp", 0),
                })

        spans_with_time.sort(key=lambda x: x["duration_ms"], reverse=True)
        return spans_with_time[:limit]

    def _get_dir_time(self, dir_path: Path) -> float:
        """Get creation/modification time of directory."""
        try:
            return dir_path.stat().st_mtime
        except:
            return 0

    def _serve_json(self, data: Dict[str, Any]):
        body = json.dumps(data, default=str, ensure_ascii=False).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _serve_error(self, message: str, code: int = 400):
        body = json.dumps({"error": message}).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        """Suppress default logging."""
        pass


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Enhanced Trace Visualization Server V6.3+")
    parser.add_argument("--port", type=int, default=8080, help="Server port")
    parser.add_argument(
        "--trace-dir", type=str, default="traces", help="Trace directory"
    )
    args = parser.parse_args()

    # Resolve trace_dir to absolute path
    trace_dir = Path(args.trace_dir).resolve()
    if not trace_dir.is_absolute():
        project_root = Path(__file__).resolve().parent.parent
        trace_dir = (project_root / args.trace_dir).resolve()

    # Setup storage
    TraceAPIHandler.storage = FileStorage(base_dir=str(trace_dir))

    # Serve from dashboards directory
    os.chdir(Path(__file__).resolve().parent)

    server = HTTPServer(("0.0.0.0", args.port), TraceAPIHandler)
    print(f"🔬 Enhanced Trace Observatory: http://localhost:{args.port}")
    print(f"📁 Trace Dir: {trace_dir}")
    print(f"   - V6 Viewer: http://localhost:{args.port}")
    print(f"   - API: http://localhost:{args.port}/api/traces")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n👋 Shutting down.")
        server.server_close()


if __name__ == "__main__":
    main()
