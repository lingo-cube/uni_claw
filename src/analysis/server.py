"""Web server for traversal analysis dashboard."""

import json
from pathlib import Path
from typing import Dict, List

from starlette.applications import Starlette
from starlette.responses import JSONResponse, HTMLResponse
from starlette.routing import Route
from starlette.staticfiles import StaticFiles

from .trace_analyzer import TraceAnalyzer
from .metrics import MetricsCollector, get_metrics_collector
from .tree import TraversalTreeBuilder, CorrelationEngine


class AnalysisServer:
    """Server for analyzing and serving traversal data."""

    def __init__(self, trace_dir: Path = Path(".traces")):
        """Initialize analysis server.

        Args:
            trace_dir: Directory containing trace files
        """
        self.trace_dir = trace_dir
        self.trace_analyzer = TraceAnalyzer(trace_dir)
        self.metrics_collector = get_metrics_collector()
        self.tree_builder = TraversalTreeBuilder()
        self.correlation_engine = CorrelationEngine(
            self.trace_analyzer,
            self.metrics_collector
        )

        # Load traces
        self.sessions = self.trace_analyzer.load_all_traces()

    def reload_data(self):
        """Reload all trace and metrics data."""
        self.sessions = self.trace_analyzer.load_all_traces()

    def get_dashboard_data(self) -> Dict:
        """Get all data for the dashboard.

        Returns:
            Dictionary with all dashboard data
        """
        return {
            "sessions": [
                {
                    "trace_id": s.trace_id,
                    "duration_ms": s.duration_ms,
                    "span_count": s.span_count,
                    "start_time": s.start_time,
                }
                for s in self.trace_analyzer.get_all_sessions()
            ],
            "timelines": {
                s.trace_id: self.trace_analyzer.get_trace_timeline(s.trace_id)
                for s in self.trace_analyzer.get_all_sessions()
            },
            "metrics": {
                "ai": self.metrics_collector.get_ai_metrics_summary(),
                "traversal": self.metrics_collector.get_traversal_metrics_summary(),
            },
            "performance": self.trace_analyzer.analyze_component_performance(),
            "slowest": self.trace_analyzer.get_slowest_operations(10),
        }

    def create_app(self) -> Starlette:
        """Create Starlette application.

        Returns:
            Starlette application
        """

        def dashboard(request):
            """Serve dashboard HTML."""
            dashboard_path = Path(__file__).parent / "dashboard.html"
            with open(dashboard_path, "r", encoding="utf-8") as f:
                html = f.read()
            return HTMLResponse(html)

        def api_data(request):
            """API endpoint for dashboard data."""
            self.reload_data()
            return JSONResponse(self.get_dashboard_data())

        def api_sessions(request):
            """API endpoint for sessions list."""
            self.reload_data()
            sessions = [
                {
                    "trace_id": s.trace_id,
                    "duration_ms": s.duration_ms,
                    "span_count": s.span_count,
                }
                for s in self.trace_analyzer.get_all_sessions()
            ]
            return JSONResponse(sessions)

        def api_session_detail(request):
            """API endpoint for session detail."""
            trace_id = request.path_params.get("trace_id")
            session = self.trace_analyzer.get_session(trace_id)
            if not session:
                return JSONResponse({"error": "Session not found"}, status_code=404)

            return JSONResponse({
                "trace_id": session.trace_id,
                "duration_ms": session.duration_ms,
                "span_count": session.span_count,
                "timeline": self.trace_analyzer.get_trace_timeline(trace_id),
            })

        def api_metrics(request):
            """API endpoint for metrics."""
            return JSONResponse({
                "ai": self.metrics_collector.get_ai_metrics_summary(),
                "traversal": self.metrics_collector.get_traversal_metrics_summary(),
            })

        def api_performance(request):
            """API endpoint for performance data."""
            return JSONResponse(self.trace_analyzer.analyze_component_performance())

        def api_slowest(request):
            """API endpoint for slowest operations."""
            limit = int(request.query_params.get("limit", 10))
            return JSONResponse(self.trace_analyzer.get_slowest_operations(limit))

        def api_tree(request):
            """API endpoint for tree visualization."""
            # Build tree from all visited items
            all_items = []
            for session in self.trace_analyzer.get_all_sessions():
                for event in session.events:
                    if event.event == "visited":
                        all_items.append({
                            "name": event.data.get("name", "Unknown"),
                            "type": event.data.get("type"),
                            "path": event.data.get("path", []),
                            "coordinate": event.data.get("coordinate"),
                        })

            tree = self.tree_builder.build_from_visited_items(all_items)
            return JSONResponse(tree.to_dict())

        def api_prometheus(request):
            """API endpoint for Prometheus metrics export."""
            return JSONResponse({
                "metrics": self.metrics_collector.export_to_prometheus_format()
            })

        # Create routes
        routes = [
            Route("/", dashboard),
            Route("/api/data", api_data),
            Route("/api/sessions", api_sessions),
            Route("/api/sessions/{trace_id}", api_session_detail),
            Route("/api/metrics", api_metrics),
            Route("/api/performance", api_performance),
            Route("/api/slowest", api_slowest),
            Route("/api/tree", api_tree),
            Route("/api/prometheus", api_prometheus),
        ]

        return Starlette(routes=routes)


def run_server(trace_dir: Path = Path(".traces"), host: str = "127.0.0.1", port: int = 8000):
    """Run the analysis server.

    Args:
        trace_dir: Directory containing trace files
        host: Host to bind to
        port: Port to bind to
    """
    import uvicorn

    server = AnalysisServer(trace_dir)
    app = server.create_app()

    print(f"🚀 Starting Traversal Analysis Dashboard")
    print(f"📊 Dashboard: http://{host}:{port}")
    print(f"📁 Trace directory: {trace_dir}")

    uvicorn.run(app, host=host, port=port, log_level="info")


__all__ = [
    "AnalysisServer",
    "run_server",
]
