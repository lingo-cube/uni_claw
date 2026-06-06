#!/usr/bin/env python3
"""
Dashboard Performance Testing Script - Phase 6.2

Tests:
1. Load time measurement (target <1s)
2. Performance with 10+ modules
3. Auto-refresh under load

Generates performance report with metrics and recommendations.
"""

import asyncio
import json
import statistics
import time
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from threading import Thread
from typing import Any, Dict, List
from urllib.parse import urlparse
from urllib.request import urlopen, Request
from dataclasses import dataclass
from datetime import datetime

# Test configuration
TARGET_LOAD_TIME = 1.0  # seconds
MODULE_COUNT = 15  # number of modules to simulate
CONCURRENT_REQUESTS = 20  # for load testing
AUTO_REFRESH_INTERVAL = 5  # seconds


@dataclass
class PerformanceMetric:
    """Performance metric result."""
    name: str
    value: float
    unit: str
    target: float
    passed: bool
    details: str = ""


class DashboardPerformanceTester:
    """Main performance test runner."""

    def __init__(self, dashboard_port: int = 8002, trace_server_port: int = 8080):
        self.dashboard_port = dashboard_port
        self.trace_server_port = trace_server_port
        self.metrics: List[PerformanceMetric] = []
        self.dashboard_server = None
        self.trace_server = None

    def setup_test_data(self):
        """Generate test data for 10+ modules."""
        print(f"Setting up test data for {MODULE_COUNT} modules...")

        # Create test directories
        test_data_dir = Path("/tmp/dashboard_perf_test")
        test_data_dir.mkdir(exist_ok=True)

        # Create traces directory with multiple module traces
        traces_dir = test_data_dir / ".traces"
        traces_dir.mkdir(exist_ok=True)

        # Create traces for 15 modules
        for module_idx in range(MODULE_COUNT):
            trace_file = traces_dir / f"module_{module_idx:02d}.jsonl"
            self._generate_module_trace(trace_file, module_idx)

        # Create results directory
        results_dir = test_data_dir / ".results" / "sessions"
        results_dir.mkdir(parents=True, exist_ok=True)

        # Create session results for each module
        for module_idx in range(MODULE_COUNT):
            result_file = results_dir / f"module_{module_idx:02d}_session.json"
            self._generate_module_result(result_file, module_idx)

        print(f"Created {MODULE_COUNT} module traces and session results")
        return test_data_dir

    def _generate_module_trace(self, filepath: Path, module_idx: int):
        """Generate a trace file for a module."""
        trace_events = []
        base_time = time.time() - 3600  # 1 hour ago

        # Generate session start
        trace_events.append({
            "type": "session_start",
            "trace_id": f"trace_{module_idx:02d}",
            "span_id": f"session_{module_idx:02d}",
            "timestamp": base_time,
            "node_type": "session"
        })

        # Generate spans for different operations
        operations = [
            ("VisionService", "analyze_screen", 50 + module_idx * 10),
            ("AIService", "decide_action", 100 + module_idx * 15),
            ("ActionExecutor", "execute_tap", 30 + module_idx * 5),
            ("StateTracker", "update_state", 20 + module_idx * 3),
            ("GraphEngine", "next_step", 80 + module_idx * 12),
        ]

        for comp, op, base_duration in operations:
            trace_events.append({
                "type": "span_start",
                "trace_id": f"trace_{module_idx:02d}",
                "span_id": f"span_{comp}_{module_idx}",
                "parent_id": f"session_{module_idx:02d}",
                "component": comp,
                "operation": op,
                "timestamp": base_time + len(trace_events) * 0.1,
                "node_type": "span"
            })
            trace_events.append({
                "type": "span_end",
                "trace_id": f"trace_{module_idx:02d}",
                "span_id": f"span_{comp}_{module_idx}",
                "parent_id": f"session_{module_idx:02d}",
                "component": comp,
                "operation": op,
                "duration_ms": base_duration,
                "timestamp": base_time + len(trace_events) * 0.1,
                "status": "success"
            })

        # Write to file
        with open(filepath, "w") as f:
            for event in trace_events:
                f.write(json.dumps(event) + "\n")

    def _generate_module_result(self, filepath: Path, module_idx: int):
        """Generate a session result file."""
        result = {
            "session_id": f"session_module_{module_idx:02d}",
            "status": "success" if module_idx % 3 != 0 else "partial",
            "visited_count": 10 + module_idx * 2,
            "skipped_count": 2 + module_idx,
            "duration_ms": 5000 + module_idx * 500,
            "start_time": time.time() - 3600 + module_idx * 100,
            "instruction": f"Module {module_idx} test instruction"
        }
        with open(filepath, "w") as f:
            json.dump(result, f)

    def start_servers(self, test_data_dir: Path):
        """Start the dashboard and trace servers."""
        import os
        import sys

        # Save original directory
        original_dir = os.getcwd()
        original_path = sys.path.copy()

        # Change to test data directory
        os.chdir(test_data_dir)

        try:
            # Import and start dashboard
            sys.path.insert(0, str(Path(__file__).parent.parent))
            from dashboards.simple_dashboard import DashboardHandler, main as dashboard_main

            # Start dashboard in background thread
            def run_dashboard():
                import socketserver
                PORT = self.dashboard_port
                with socketserver.TCPServer(("", PORT), DashboardHandler) as httpd:
                    self.dashboard_server = httpd
                    httpd.serve_forever()

            dashboard_thread = Thread(target=run_dashboard, daemon=True)
            dashboard_thread.start()
            time.sleep(1)  # Wait for server to start

            print(f"Dashboard server started on port {self.dashboard_port}")
        finally:
            # Restore original directory
            os.chdir(original_dir)
            sys.path = original_path

    def stop_servers(self):
        """Stop running servers."""
        if self.dashboard_server:
            self.dashboard_server.shutdown()
            print("Dashboard server stopped")

    def test_load_time(self) -> PerformanceMetric:
        """Test 1: Measure Dashboard load time."""
        print("\n=== Test 1: Load Time Measurement ===")

        load_times = []
        iterations = 5

        for i in range(iterations):
            start = time.perf_counter()
            try:
                req = Request(
                    f"http://127.0.0.1:{self.dashboard_port}/",
                    headers={"User-Agent": "Performance-Test/1.0"}
                )
                with urlopen(req, timeout=10) as response:
                    # Read entire response
                    response.read()
                elapsed = time.perf_counter() - start
                load_times.append(elapsed)
                print(f"  Iteration {i+1}: {elapsed*1000:.1f}ms")
            except Exception as e:
                print(f"  Iteration {i+1}: ERROR - {e}")
                continue

        if not load_times:
            return PerformanceMetric(
                "Load Time",
                0,
                "ms",
                TARGET_LOAD_TIME,
                False,
                "Failed to measure - connection error"
            )

        avg_load = statistics.mean(load_times)
        max_load = max(load_times)
        min_load = min(load_times)
        std_dev = statistics.stdev(load_times) if len(load_times) > 1 else 0

        passed = avg_load < TARGET_LOAD_TIME
        details = f"Avg: {avg_load*1000:.1f}ms, Min: {min_load*1000:.1f}ms, Max: {max_load*1000:.1f}ms, StdDev: {std_dev*1000:.1f}ms"

        metric = PerformanceMetric(
            "Load Time",
            avg_load * 1000,
            "ms",
            TARGET_LOAD_TIME * 1000,
            passed,
            details
        )

        print(f"  Result: {'PASS' if passed else 'FAIL'} - {details}")
        self.metrics.append(metric)
        return metric

    def test_api_response_time(self) -> PerformanceMetric:
        """Test API response times for all endpoints."""
        print("\n=== Test 2: API Response Times ===")

        endpoints = [
            "/api/results",
            "/api/traces",
            "/api/metrics",
            "/api/logs"
        ]

        all_times = []
        endpoint_times = {}

        for endpoint in endpoints:
            times = []
            for i in range(3):  # 3 iterations per endpoint
                try:
                    start = time.perf_counter()
                    req = Request(
                        f"http://127.0.0.1:{self.dashboard_port}{endpoint}",
                        headers={"User-Agent": "Performance-Test/1.0"}
                    )
                    with urlopen(req, timeout=10) as response:
                        response.read()
                    elapsed = time.perf_counter() - start
                    times.append(elapsed)
                except Exception as e:
                    print(f"  {endpoint}: ERROR - {e}")
                    continue

            if times:
                avg = statistics.mean(times)
                endpoint_times[endpoint] = avg * 1000
                all_times.extend(times)
                print(f"  {endpoint}: {avg*1000:.1f}ms avg")

        if not all_times:
            return PerformanceMetric(
                "API Response Time",
                0,
                "ms",
                500,
                False,
                "All endpoints failed"
            )

        avg_response = statistics.mean(all_times)
        passed = avg_response < 0.5  # 500ms target

        details = f"Overall: {avg_response*1000:.1f}ms avg"
        for endpoint, avg_time in endpoint_times.items():
            details += f", {endpoint}: {avg_time:.1f}ms"

        metric = PerformanceMetric(
            "API Response Time",
            avg_response * 1000,
            "ms",
            500,
            passed,
            details
        )

        print(f"  Result: {'PASS' if passed else 'FAIL'} - {details}")
        self.metrics.append(metric)
        return metric

    def test_multi_module_handling(self) -> PerformanceMetric:
        """Test 3: Dashboard with 10+ modules."""
        print(f"\n=== Test 3: Multi-Module Handling ({MODULE_COUNT} modules) ===")

        try:
            # Fetch results which should contain all modules
            start = time.perf_counter()
            req = Request(
                f"http://127.0.0.1:{self.dashboard_port}/api/results",
                headers={"User-Agent": "Performance-Test/1.0"}
            )
            with urlopen(req, timeout=10) as response:
                data = json.loads(response.read().decode())
            elapsed = time.perf_counter() - start

            session_count = data.get("summary", {}).get("total_sessions", 0)
            visited_count = data.get("summary", {}).get("total_visited", 0)

            passed = session_count >= 10 and elapsed < 2.0  # Should handle 10+ in under 2s
            details = f"Loaded {session_count} sessions, {visited_count} visited items in {elapsed*1000:.1f}ms"

            metric = PerformanceMetric(
                "Multi-Module Handling",
                session_count,
                "sessions",
                10,
                passed,
                details
            )

            print(f"  Result: {'PASS' if passed else 'FAIL'} - {details}")
            self.metrics.append(metric)
            return metric

        except Exception as e:
            metric = PerformanceMetric(
                "Multi-Module Handling",
                0,
                "sessions",
                10,
                False,
                f"Error: {e}"
            )
            print(f"  Result: FAIL - Error: {e}")
            self.metrics.append(metric)
            return metric

    def test_auto_refresh_under_load(self) -> PerformanceMetric:
        """Test 4: Auto-refresh performance under concurrent load."""
        print(f"\n=== Test 4: Auto-Refresh Under Load ===")
        print(f"  Simulating {CONCURRENT_REQUESTS} concurrent requests...")

        async def fetch_endpoint(session, endpoint):
            start = time.perf_counter()
            try:
                async with session.get(f"http://127.0.0.1:{self.dashboard_port}{endpoint}") as response:
                    await response.text()
                return time.perf_counter() - start
            except Exception as e:
                print(f"    Request failed: {e}")
                return None

        async def run_load_test():
            import aiohttp
            endpoints = ["/api/results", "/api/traces", "/api/metrics"]

            times = []
            async with aiohttp.ClientSession() as session:
                # Create concurrent requests
                tasks = []
                for i in range(CONCURRENT_REQUESTS):
                    endpoint = endpoints[i % len(endpoints)]
                    tasks.append(fetch_endpoint(session, endpoint))

                results = await asyncio.gather(*tasks)
                times = [t for t in results if t is not None]

            return times

        try:
            times = asyncio.run(run_load_test())

            if not times:
                return PerformanceMetric(
                    "Auto-Refresh Under Load",
                    0,
                    "ms",
                    1000,
                    False,
                    "All requests failed"
                )

            avg_time = statistics.mean(times)
            max_time = max(times)
            p95_time = statistics.quantiles(times, n=20)[18] if len(times) >= 20 else max_time

            # Auto-refresh should complete within 1s even under load
            passed = avg_time < 1.0 and max_time < 3.0

            details = f"Completed {len(times)}/{CONCURRENT_REQUESTS} requests, Avg: {avg_time*1000:.1f}ms, P95: {p95_time*1000:.1f}ms, Max: {max_time*1000:.1f}ms"

            metric = PerformanceMetric(
                "Auto-Refresh Under Load",
                avg_time * 1000,
                "ms",
                1000,
                passed,
                details
            )

            print(f"  Result: {'PASS' if passed else 'FAIL'} - {details}")
            self.metrics.append(metric)
            return metric

        except Exception as e:
            metric = PerformanceMetric(
                "Auto-Refresh Under Load",
                0,
                "ms",
                1000,
                False,
                f"Error: {e}"
            )
            print(f"  Result: FAIL - Error: {e}")
            self.metrics.append(metric)
            return metric

    def test_memory_efficiency(self) -> PerformanceMetric:
        """Test 5: Memory usage with large datasets."""
        print(f"\n=== Test 5: Memory Efficiency ===")

        import tracemalloc
        tracemalloc.start()

        try:
            # Make several requests to simulate usage
            for endpoint in ["/api/results", "/api/traces", "/api/metrics"]:
                req = Request(
                    f"http://127.0.0.1:{self.dashboard_port}{endpoint}",
                    headers={"User-Agent": "Performance-Test/1.0"}
                )
                with urlopen(req, timeout=10) as response:
                    response.read()

            current, peak = tracemalloc.get_traced_memory()
            tracemalloc.stop()

            # Memory should be under 50MB peak for this test
            peak_mb = peak / 1024 / 1024
            passed = peak_mb < 50

            details = f"Current: {current/1024/1024:.1f}MB, Peak: {peak_mb:.1f}MB"

            metric = PerformanceMetric(
                "Memory Efficiency",
                peak_mb,
                "MB",
                50,
                passed,
                details
            )

            print(f"  Result: {'PASS' if passed else 'FAIL'} - {details}")
            self.metrics.append(metric)
            return metric

        except Exception as e:
            tracemalloc.stop()
            metric = PerformanceMetric(
                "Memory Efficiency",
                0,
                "MB",
                50,
                False,
                f"Error: {e}"
            )
            print(f"  Result: FAIL - Error: {e}")
            self.metrics.append(metric)
            return metric

    def generate_report(self) -> Dict[str, Any]:
        """Generate performance test report."""
        print("\n=== Performance Test Report ===")

        passed = sum(1 for m in self.metrics if m.passed)
        total = len(self.metrics)

        report = {
            "timestamp": datetime.now().isoformat(),
            "summary": {
                "total_tests": total,
                "passed": passed,
                "failed": total - passed,
                "success_rate": f"{(passed/total*100):.1f}%" if total > 0 else "0%"
            },
            "metrics": [
                {
                    "name": m.name,
                    "value": m.value,
                    "unit": m.unit,
                    "target": m.target,
                    "passed": m.passed,
                    "details": m.details
                }
                for m in self.metrics
            ],
            "recommendations": self._generate_recommendations()
        }

        # Print summary
        print(f"\nTotal Tests: {total}")
        print(f"Passed: {passed}")
        print(f"Failed: {total - passed}")
        print(f"Success Rate: {report['summary']['success_rate']}")

        print("\nDetailed Results:")
        for m in self.metrics:
            status = "✓ PASS" if m.passed else "✗ FAIL"
            print(f"  {status} {m.name}: {m.value:.1f}{m.unit} (target: <{m.target}{m.unit})")
            if m.details:
                print(f"       {m.details}")

        print("\nRecommendations:")
        for rec in report["recommendations"]:
            print(f"  • {rec}")

        return report

    def _generate_recommendations(self) -> List[str]:
        """Generate performance optimization recommendations."""
        recommendations = []

        for metric in self.metrics:
            if not metric.passed:
                if "Load Time" in metric.name:
                    recommendations.append(
                        f"Load time ({metric.value:.0f}ms) exceeds target. "
                        "Consider: caching HTML, inlining critical CSS, lazy-loading non-critical resources."
                    )
                elif "API Response" in metric.name:
                    recommendations.append(
                        f"API response time ({metric.value:.0f}ms) is slow. "
                        "Consider: response compression, partial data responses, result pagination."
                    )
                elif "Multi-Module" in metric.name:
                    recommendations.append(
                        f"Multi-module handling loaded only {metric.value:.0f} sessions. "
                        "Consider: virtual scrolling for large lists, lazy loading for historical data."
                    )
                elif "Auto-Refresh" in metric.name:
                    recommendations.append(
                        f"Auto-refresh under load ({metric.value:.0f}ms) is slow. "
                        "Consider: request debouncing, incremental updates, WebSocket for real-time updates."
                    )
                elif "Memory" in metric.name:
                    recommendations.append(
                        f"Memory usage ({metric.value:.1f}MB) is high. "
                        "Consider: stream processing for large files, data pruning, connection pooling."
                    )

        if not recommendations:
            recommendations.append("All performance targets met! Continue monitoring in production.")

        return recommendations

    def save_report(self, report: Dict[str, Any], filepath: Path):
        """Save report to JSON file."""
        with open(filepath, "w") as f:
            json.dump(report, f, indent=2)
        print(f"\nReport saved to: {filepath}")

    def run_all_tests(self) -> Dict[str, Any]:
        """Run all performance tests."""
        print("=" * 60)
        print("Dashboard Performance Testing - Phase 6.2")
        print("=" * 60)

        # Setup
        test_data_dir = self.setup_test_data()

        try:
            self.start_servers(test_data_dir)
            time.sleep(2)  # Wait for servers to stabilize

            # Run tests
            self.test_load_time()
            self.test_api_response_time()
            self.test_multi_module_handling()
            self.test_auto_refresh_under_load()
            self.test_memory_efficiency()

            # Generate report
            report = self.generate_report()
            return report

        finally:
            self.stop_servers()


def main():
    """Main entry point."""
    tester = DashboardPerformanceTester()
    report = tester.run_all_tests()

    # Save report
    report_path = Path(__file__).parent.parent / "test_results" / "dashboard_performance_report.json"
    report_path.parent.mkdir(exist_ok=True)
    tester.save_report(report, report_path)

    # Exit with appropriate code
    failed = report["summary"]["failed"]
    exit(0 if failed == 0 else 1)


if __name__ == "__main__":
    main()
