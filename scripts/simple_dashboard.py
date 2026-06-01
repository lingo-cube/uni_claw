#!/usr/bin/env python3
"""Simple traversal analysis dashboard with traces and metrics."""

import http.server
import json
import socketserver
from pathlib import Path
from collections import defaultdict
from typing import Dict, List

PORT = 8002


class DashboardHandler(http.server.SimpleHTTPRequestHandler):
    """Simple dashboard handler."""

    def do_GET(self):
        """Handle GET requests."""
        if self.path == "/" or self.path == "/dashboard.html":
            self.serve_dashboard()
        elif self.path == "/api/results":
            self.serve_results()
        elif self.path == "/api/logs":
            self.serve_logs()
        elif self.path == "/api/traces":
            self.serve_traces()
        elif self.path == "/api/metrics":
            self.serve_metrics()
        elif self.path.startswith("/api/results/"):
            self.serve_result_file(self.path[13:])
        else:
            super().do_GET()

    def serve_dashboard(self):
        """Serve dashboard HTML."""
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.end_headers()

        html = """<!DOCTYPE html>
<html>
<head>
    <title>Traversal Analysis Dashboard</title>
    <meta charset="utf-8">
    <style>
        * { box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f7; }
        .container { max-width: 1400px; margin: 0 auto; }
        .header { background: white; padding: 20px; border-radius: 12px; margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        h1 { margin: 0; color: #1d1d1f; }
        .subtitle { color: #6e6e73; margin-top: 5px; }
        .tabs { display: flex; gap: 10px; margin-top: 15px; }
        .tab { padding: 8px 16px; background: #f5f5f7; border: none; border-radius: 8px; cursor: pointer; }
        .tab.active { background: #0071e3; color: white; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px; margin-bottom: 20px; }
        .card { background: white; padding: 20px; border-radius: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        .card h2 { margin: 0 0 15px 0; font-size: 18px; color: #1d1d1f; }
        .metric { display: flex; align-items: baseline; gap: 8px; margin-bottom: 10px; }
        .metric-value { font-size: 32px; font-weight: 600; color: #1d1d1f; }
        .metric-label { font-size: 13px; color: #6e6e73; }
        .metric-sub { font-size: 11px; color: #8e8e93; }
        table { width: 100%; border-collapse: collapse; font-size: 13px; }
        th, td { padding: 10px 12px; text-align: left; border-bottom: 1px solid #e8e8ed; }
        th { font-weight: 600; color: #6e6e73; font-size: 11px; text-transform: uppercase; }
        .status { padding: 4px 8px; border-radius: 4px; font-size: 11px; font-weight: 500; }
        .status.success { background: #e8f5e9; color: #2e7d32; }
        .status.failed { background: #ffebee; color: #c62828; }
        .status.partial { background: #fff3e0; color: #f57c00; }
        .trace-tree { font-family: monospace; font-size: 12px; }
        .trace-node { padding: 4px 0; }
        .trace-children { margin-left: 20px; border-left: 1px solid #e8e8ed; padding-left: 10px; }
        .trace-item { padding: 8px; border-bottom: 1px solid #e8e8ed; font-family: monospace; font-size: 11px; }
        .trace-time { color: #8e8e93; }
        .trace-comp { color: #0071e3; font-weight: 500; }
        .trace-op { color: #6e6e73; }
        .tree-toggle { cursor: pointer; user-select: none; display: inline-block; width: 12px; color: #6e6e73; }
        .tree-toggle:hover { color: #0071e3; }
        .bar-chart { display: flex; height: 20px; border-radius: 4px; overflow: hidden; margin: 5px 0; }
        .bar { height: 100%; transition: width 0.3s; }
        pre { background: #f5f5f7; padding: 12px; border-radius: 8px; overflow-x: auto; font-size: 11px; }
        .tab-content { display: none; }
        .tab-content.active { display: block; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🔍 Traversal Analysis Dashboard</h1>
            <div class="subtitle">分析遍历结果、日志、追踪和指标</div>
            <div class="tabs">
                <button class="tab active" onclick="showTab('overview')">📊 总览</button>
                <button class="tab" onclick="showTab('traces')">🔗 追踪</button>
                <button class="tab" onclick="showTab('metrics')">📈 指标</button>
                <button class="tab" onclick="showTab('logs')">📋 日志</button>
            </div>
        </div>

        <div id="tab-overview" class="tab-content active">
            <div class="grid" id="overview-metrics"></div>
            <div class="card">
                <h2>最近遍历</h2>
                <div id="results-table"></div>
            </div>
        </div>

        <div id="tab-traces" class="tab-content">
            <div class="card">
                <h2>分布式追踪</h2>
                <div id="traces-list"></div>
            </div>
        </div>

        <div id="tab-metrics" class="tab-content">
            <div class="grid" id="metrics-grid"></div>
        </div>

        <div id="tab-logs" class="tab-content">
            <div class="card">
                <h2>结构化日志</h2>
                <div id="logs-list"></div>
            </div>
        </div>
    </div>

    <script>
        function showTab(tabName) {
            document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
            event.target.classList.add('active');
            document.getElementById('tab-' + tabName).classList.add('active');

            if (tabName === 'overview') loadOverview();
            if (tabName === 'traces') loadTraces();
            if (tabName === 'metrics') loadMetrics();
            if (tabName === 'logs') loadLogs();
        }

        async function loadOverview() {
            const response = await fetch('/api/results');
            const data = await response.json();

            let html = '<div class="card"><h2>总览</h2>' +
                '<div class="metric"><span class="metric-value">' + data.summary.total_sessions + '</span><span class="metric-label">会话总数</span></div>' +
                '<div class="metric"><span class="metric-value">' + data.summary.total_visited + '</span><span class="metric-label">访问项目</span></div>' +
                '<div class="metric"><span class="metric-value">' + data.summary.total_skipped + '</span><span class="metric-label">跳过项目</span></div>' +
                '</div>';
            document.getElementById('overview-metrics').innerHTML = html;

            let tableHtml = '<table><thead><tr><th>会话ID</th><th>状态</th><th>访问数</th><th>耗时</th><th>时间</th></tr></thead><tbody>';
            data.results.forEach(r => {
                tableHtml += '<tr><td>' + r.session_id.substring(0, 12) + '...</td>' +
                    '<td><span class="status ' + r.status + '">' + r.status + '</span></td>' +
                    '<td>' + r.visited_count + '</td>' +
                    '<td>' + (r.duration_ms / 1000).toFixed(1) + 's</td>' +
                    '<td>' + new Date(r.start_time * 1000).toLocaleTimeString() + '</td></tr>';
            });
            tableHtml += '</tbody></table>';
            document.getElementById('results-table').innerHTML = tableHtml;
        }

        async function loadTraces() {
            const response = await fetch('/api/traces');
            const data = await response.json();

            // Build tree structure
            const spans = {};
            const roots = [];

            // First pass: collect all spans
            data.traces.forEach(t => {
                spans[t.span_id] = {
                    ...t,
                    children: []
                };
            });

            // Second pass: build tree
            data.traces.forEach(t => {
                const span = spans[t.span_id];
                if (t.parent_id && spans[t.parent_id]) {
                    spans[t.parent_id].children.push(span);
                } else {
                    roots.push(span);
                }
            });

            // Render tree
            const renderNode = (node, depth = 0) => {
                const indent = depth * 16;
                const hasChildren = node.children && node.children.length > 0;
                const icon = hasChildren ? '▼' : '•';

                let html = '<div class="trace-node" style="margin-left: ' + indent + 'px;">' +
                    '<span class="tree-toggle" onclick="this.nextElementSibling.nextElementSibling.classList.toggle(\'collapsed\')">' + icon + '</span> ' +
                    '<span class="trace-time">' + new Date(node.timestamp * 1000).toLocaleTimeString() + '</span> ' +
                    '<span class="trace-comp">' + node.component + '</span>.' +
                    '<span class="trace-op">' + node.operation + '</span> ' +
                    '<span style="color: #8e8e93;">' + node.duration_ms.toFixed(1) + 'ms</span>';

                if (node.status) {
                    html += ' <span style="color: ' + (node.status === 'success' ? '#2e7d32' : '#c62828') + ';">' + node.status + '</span>';
                }

                html += '</div>';

                if (hasChildren) {
                    html += '<div class="trace-children">';
                    node.children.forEach(child => {
                        html += renderNode(child, depth + 1);
                    });
                    html += '</div>';
                }

                return html;
            };

            let html = '<div class="trace-tree">';
            roots.forEach(root => {
                html += renderNode(root);
            });
            html += '</div>';

            document.getElementById('traces-list').innerHTML = html || '<div style="padding: 20px; text-align: center; color: #8e8e93;">暂无追踪数据</div>';
        }

        async function loadMetrics() {
            const response = await fetch('/api/metrics');
            const data = await response.json();

            let html = '';
            for (const [component, stats] of Object.entries(data.components)) {
                const maxDuration = Math.max(...Object.values(data.components).map(s => s.max_duration_ms));
                html += '<div class="card"><h2>' + component + '</h2>' +
                    '<div class="metric"><span class="metric-value">' + stats.call_count + '</span><span class="metric-label">调用次数</span></div>' +
                    '<div class="metric"><span class="metric-value">' + stats.avg_duration_ms.toFixed(1) + 'ms</span><span class="metric-label">平均耗时</span></div>' +
                    '<div class="metric"><span class="metric-value">' + stats.max_duration_ms.toFixed(1) + 'ms</span><span class="metric-label">最大耗时</span></div>' +
                    '<div class="bar-chart">' +
                    '<div class="bar" style="width: ' + (stats.max_duration_ms / maxDuration * 100) + '%; background: ' + (stats.avg_duration_ms > 1000 ? '#c62828' : '#0071e3') + ';"></div>' +
                    '</div>' +
                    '<div class="metric-sub">成功率: ' + (stats.success_rate * 100).toFixed(1) + '%</div>' +
                    '</div>';
            }
            document.getElementById('metrics-grid').innerHTML = html || '<div style="padding: 20px; text-align: center; color: #8e8e93;">暂无指标数据</div>';
        }

        async function loadLogs() {
            const response = await fetch('/api/logs');
            const data = await response.json();

            let html = '';
            data.logs.forEach(log => {
                html += '<div class="trace-item">' +
                    '<span class="trace-time">' + new Date(log.timestamp).toLocaleTimeString() + '</span> ' +
                    '<span class="trace-comp">[' + log.type + ']</span> ';
                if (log.session_id) html += '<span style="color: #0071e3;">' + log.session_id.substring(0, 8) + '</span> ';
                if (log.action) html += log.action + ' ';
                if (log.target) html += '→ ' + log.target;
                html += '</div>';
            });
            document.getElementById('logs-list').innerHTML = html || '<div style="padding: 20px; text-align: center; color: #8e8e93;">暂无日志数据</div>';
        }

        // Auto-load overview on start
        loadOverview();
    </script>
</body>
</html>"""
        self.wfile.write(html.encode('utf-8'))

    def serve_results(self):
        """Serve traversal results."""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        results = []
        total_visited = 0
        total_skipped = 0

        results_dir = Path(".results/sessions")
        if results_dir.exists():
            for json_file in sorted(results_dir.glob("*.json"), reverse=True)[:20]:
                try:
                    with open(json_file, "r", encoding="utf-8") as f:
                        data = json.load(f)
                        visited_count = len(data.get("visited_items", []))
                        skipped_count = len(data.get("skipped_items", []))

                        total_visited += visited_count
                        total_skipped += skipped_count

                        results.append({
                            "session_id": data.get("session_id", json_file.stem),
                            "status": data.get("status", "unknown"),
                            "visited_count": visited_count,
                            "skipped_count": skipped_count,
                            "duration_ms": data.get("duration_ms", 0),
                            "start_time": data.get("start_time", 0),
                            "instruction": data.get("instruction", "")
                        })
                except Exception as e:
                    print(f"Error reading {json_file}: {e}")

        response_data = {
            "summary": {
                "total_sessions": len(results),
                "total_visited": total_visited,
                "total_skipped": total_skipped
            },
            "results": results
        }

        self.wfile.write(json.dumps(response_data, ensure_ascii=False).encode('utf-8'))

    def serve_traces(self):
        """Serve trace data with tree structure."""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        traces = []
        traces_dir = Path(".traces")

        if traces_dir.exists():
            for jsonl_file in sorted(traces_dir.glob("*.jsonl"), reverse=True)[:10]:
                try:
                    with open(jsonl_file, "r", encoding="utf-8") as f:
                        for line in f:
                            if line.strip():
                                try:
                                    entry = json.loads(line)
                                    # Extract trace events with parent relationship
                                    if entry.get("type") == "span_end":
                                        traces.append({
                                            "trace_id": entry.get("trace_id"),
                                            "span_id": entry.get("span_id"),
                                            "parent_id": entry.get("parent_id"),
                                            "component": entry.get("component"),
                                            "operation": entry.get("operation"),
                                            "duration_ms": entry.get("duration_ms", 0),
                                            "timestamp": entry.get("timestamp", 0),
                                            "status": entry.get("status")
                                        })
                                except json.JSONDecodeError:
                                    pass
                    if len(traces) > 100:
                        break
                except Exception as e:
                    print(f"Error reading {jsonl_file}: {e}")

        # Sort by timestamp (most recent first)
        traces.sort(key=lambda x: x.get("timestamp", 0), reverse=True)
        traces = traces[:100]  # Most recent 100

        self.wfile.write(json.dumps({"traces": traces}, ensure_ascii=False).encode('utf-8'))

    def serve_metrics(self):
        """Serve metrics derived from traces."""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        components = defaultdict(lambda: {
            "call_count": 0,
            "total_duration_ms": 0,
            "max_duration_ms": 0,
            "success_count": 0,
            "failure_count": 0
        })

        traces_dir = Path(".traces")
        if traces_dir.exists():
            for jsonl_file in traces_dir.glob("*.jsonl"):
                try:
                    with open(jsonl_file, "r", encoding="utf-8") as f:
                        for line in f:
                            if line.strip():
                                try:
                                    entry = json.loads(line)
                                    if entry.get("type") == "span_end":
                                        comp = entry.get("component", "unknown")
                                        duration = entry.get("duration_ms", 0)
                                        status = entry.get("status", "unknown")

                                        components[comp]["call_count"] += 1
                                        components[comp]["total_duration_ms"] += duration
                                        components[comp]["max_duration_ms"] = max(
                                            components[comp]["max_duration_ms"], duration
                                        )

                                        if status == "success":
                                            components[comp]["success_count"] += 1
                                        else:
                                            components[comp]["failure_count"] += 1
                                except json.JSONDecodeError:
                                    pass
                except Exception as e:
                    print(f"Error reading {jsonl_file}: {e}")

        # Calculate averages and success rates
        for comp in components.values():
            if comp["call_count"] > 0:
                comp["avg_duration_ms"] = comp["total_duration_ms"] / comp["call_count"]
                comp["success_rate"] = comp["success_count"] / comp["call_count"]
            else:
                comp["avg_duration_ms"] = 0
                comp["success_rate"] = 0

        self.wfile.write(json.dumps({"components": dict(components)}, ensure_ascii=False).encode('utf-8'))

    def serve_logs(self):
        """Serve structured logs."""
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()

        logs = []
        logs_dir = Path(".logs")

        if logs_dir.exists():
            for jsonl_file in sorted(logs_dir.glob("*.jsonl"), reverse=True)[:5]:
                try:
                    with open(jsonl_file, "r", encoding="utf-8") as f:
                        for line in f:
                            if line.strip():
                                try:
                                    logs.append(json.loads(line))
                                except json.JSONDecodeError:
                                    pass
                    if len(logs) > 100:
                        break
                except Exception as e:
                    print(f"Error reading {jsonl_file}: {e}")

        logs = logs[-100:] if len(logs) > 100 else logs

        self.wfile.write(json.dumps({"logs": logs}, ensure_ascii=False).encode('utf-8'))

    def serve_result_file(self, filename: str):
        """Serve a specific result file."""
        results_dir = Path(".results")
        file_path = results_dir / filename

        if file_path.exists():
            self.send_response(200)
            if filename.endswith(".json"):
                self.send_header("Content-Type", "application/json; charset=utf-8")
            elif filename.endswith(".html"):
                self.send_header("Content-Type", "text/html; charset=utf-8")
            else:
                self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            with open(file_path, "rb") as f:
                self.wfile.write(f.read())
        else:
            self.send_response(404)
            self.end_headers()
            self.wfile.write(b"File not found")

    def log_message(self, format, *args):
        """Suppress log messages."""
        pass


def main():
    """Start the dashboard server."""
    print("\n" + "=" * 60)
    print("🚀 Traversal Analysis Dashboard")
    print("=" * 60)

    Path(".results/sessions").mkdir(parents=True, exist_ok=True)
    Path(".logs").mkdir(parents=True, exist_ok=True)
    Path(".traces").mkdir(parents=True, exist_ok=True)

    with socketserver.TCPServer(("", PORT), DashboardHandler) as httpd:
        print(f"URL: http://127.0.0.1:{PORT}")
        print(f"Serving .results/, .logs/, and .traces/ directories")
        print("=" * 60 + "\n")

        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n👋 Server stopped.")


if __name__ == "__main__":
    main()
