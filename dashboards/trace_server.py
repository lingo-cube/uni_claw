"""
Trace visualization server for V6.3 distributed tracing.

Serves trace analysis data via HTTP API for the trace viewer dashboard.
Reads from FileStorage (traces/{trace_id}/) and uses TraceAnalyzer.

Usage:
    python dashboards/trace_server.py [--port 8080] [--trace-dir traces]
"""

import json
import os
import sys
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from typing import Any, Dict, List
from urllib.parse import parse_qs, urlparse

# Add project root to path
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from src.trace.storage import FileStorage
from src.trace.analyzer import TraceAnalyzer, build_tree


class TraceAPIHandler(SimpleHTTPRequestHandler):
    """HTTP handler serving trace analysis API and static files."""

    storage: FileStorage = None  # Set by server setup

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
        elif path == "/api/analysis":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_analysis(tid))
        elif path == "/api/elements":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_elements(tid))
        elif path == "/api/actions":
            tid = params.get("id", [None])[0]
            if not tid:
                self._serve_error("Missing ?id= parameter", 400)
                return
            self._serve_json(self._get_actions(tid))
        elif path == "/" or path == "":
            # Serve the viewer HTML
            self.path = "/trace_viewer.html"
            super().do_GET()
        else:
            # Serve static files (CSS, JS, etc.)
            super().do_GET()

    def _list_traces(self) -> Dict[str, Any]:
        """List all available traces."""
        traces = []
        base = self.storage._base_dir
        if base.exists():
            for d in sorted(base.iterdir(), reverse=True):
                if d.is_dir() and not d.name.startswith(".") and d.name != "archive":
                    session = self.storage.read_session(d.name)
                    nodes = self.storage.read(d.name)
                    traces.append({
                        "trace_id": d.name,
                        "node_count": len(nodes),
                        "session": session,
                    })
        return {"traces": traces}

    def _get_trace_overview(self, trace_id: str) -> Dict[str, Any]:
        """Get overview of a single trace."""
        nodes = self.storage.read(trace_id)
        session = self.storage.read_session(trace_id)
        analyzer = TraceAnalyzer(nodes)

        return {
            "trace_id": trace_id,
            "session": session,
            "node_count": len(nodes),
            "node_types": {
                "session": len([n for n in nodes if n.node_type == "session"]),
                "step": len([n for n in nodes if n.node_type == "step"]),
                "span": len([n for n in nodes if n.node_type == "span"]),
            },
            "span_types": self._count_span_types(nodes),
            "time_analysis": analyzer.extract_time_analysis(),
            "error_stats": analyzer.extract_error_statistics(),
        }

    def _get_tree(self, trace_id: str) -> Dict[str, Any]:
        """Get hierarchical trace tree."""
        nodes = self.storage.read(trace_id)
        root = build_tree(nodes)
        if root is None:
            return {"error": "No session node found"}
        return {"tree": self._node_to_tree(root), "trace_id": trace_id}

    def _get_analysis(self, trace_id: str) -> Dict[str, Any]:
        """Get full analysis data."""
        nodes = self.storage.read(trace_id)
        analyzer = TraceAnalyzer(nodes)

        return {
            "trace_id": trace_id,
            "page_tree": analyzer.extract_page_tree(),
            "state_sequence": analyzer.extract_state_sequence(),
            "ai_calls": analyzer.extract_ai_calls(),
            "action_sequence": analyzer.extract_action_sequence(),
            "error_stats": analyzer.extract_error_statistics(),
            "time_analysis": analyzer.extract_time_analysis(),
            "coverage": analyzer.extract_coverage_analysis(),
        }

    def _get_elements(self, trace_id: str) -> Dict[str, Any]:
        """Get element tree data from page_snapshot spans."""
        nodes = self.storage.read(trace_id)

        # Extract page_snapshot spans with element data
        element_trees = []
        for node in nodes:
            if node.node_type == "span" and hasattr(node, "span_type") and node.span_type == "page_snapshot":
                metadata = node.metadata if hasattr(node, "metadata") else {}
                elements = metadata.get("elements", [])
                element_trees.append({
                    "timestamp": metadata.get("timestamp"),
                    "page_id": metadata.get("page_id"),
                    "page_name": metadata.get("page_id"),  # Use page_id as page_name for now
                    "navigation_path": metadata.get("page_path", []),
                    "elements": elements,
                    "element_count": len(elements),
                })

        # Build per-page element summary
        page_elements: Dict[str, List[Dict[str, Any]]] = {}
        for tree in element_trees:
            page_id = tree.get("page_id")
            if page_id:
                if page_id not in page_elements:
                    page_elements[page_id] = tree.get("elements", [])

        return {
            "trace_id": trace_id,
            "total_analyses": len(element_trees),
            "element_trees": element_trees,
            "page_elements": page_elements,
        }

    def _get_actions(self, trace_id: str) -> Dict[str, Any]:
        """Get action execution timeline from action_execution spans."""
        nodes = self.storage.read(trace_id)

        # Extract action_execution spans
        action_timeline = []
        for node in nodes:
            if node.node_type == "span" and hasattr(node, "span_type") and node.span_type == "action_execution":
                metadata = node.metadata if hasattr(node, "metadata") else {}
                action_timeline.append({
                    "timestamp": metadata.get("timestamp", node.timestamp),
                    "action": metadata.get("action"),
                    "target": metadata.get("target"),
                    "element_id": metadata.get("element_id"),
                    "page_id": metadata.get("page_id"),
                    "success": metadata.get("success", True),
                })

        # Build action statistics
        action_stats: Dict[str, int] = {}
        for action_data in action_timeline:
            action_type = action_data.get("action", "unknown")
            action_stats[action_type] = action_stats.get(action_type, 0) + 1

        return {
            "trace_id": trace_id,
            "total_actions": len(action_timeline),
            "action_timeline": action_timeline,
            "action_stats": action_stats,
        }

    def _node_to_tree(self, node) -> Dict[str, Any]:
        """Recursively convert a trace node to a tree dict."""
        result = {
            "span_id": node.span_id,
            "node_type": node.node_type,
            "timestamp": node.timestamp,
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

    def _count_span_types(self, nodes) -> Dict[str, int]:
        counts: Dict[str, int] = {}
        for n in nodes:
            if hasattr(n, "span_type") and n.span_type:
                counts[n.span_type] = counts.get(n.span_type, 0) + 1
        return counts

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
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        """Suppress default logging — use our own."""
        pass


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Trace Visualization Server")
    parser.add_argument("--port", type=int, default=8080, help="Server port")
    parser.add_argument(
        "--trace-dir", type=str, default="traces", help="Trace directory"
    )
    args = parser.parse_args()

    # Resolve trace_dir to absolute path before changing directory
    trace_dir = Path(args.trace_dir).resolve()
    if not trace_dir.is_absolute():
        # If relative, resolve from project root (parent of dashboards/)
        project_root = Path(__file__).resolve().parent.parent
        trace_dir = (project_root / args.trace_dir).resolve()

    # Setup storage with absolute path
    TraceAPIHandler.storage = FileStorage(base_dir=str(trace_dir))

    # Serve from dashboards directory for static files
    os.chdir(Path(__file__).resolve().parent)

    server = HTTPServer(("0.0.0.0", args.port), TraceAPIHandler)
    print(f"Trace Viewer: http://localhost:{args.port}")
    print(f"Trace Dir:   {trace_dir}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down.")
        server.server_close()


if __name__ == "__main__":
    main()
