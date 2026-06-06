#!/usr/bin/env python3
"""
Test Report Server - HTTP server for test results visualization.

Provides:
- TestResultsDataSource: Load and cache test results from JSON files
- TestResultsAnalyzer: Aggregate statistics and identify failures
- TestRunnerAPI: Trigger and manage test runs
- HTTP endpoints: Serve results, trigger tests, manage runs
- Embedded HTML dashboard: Auto-refreshing web interface

Usage:
    python dashboards/test_report_server.py [--port 8003] [--results-dir PATH]
"""

import argparse
import asyncio
import json
import logging
import os
import subprocess
import sys
import time
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone, timedelta
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple
from urllib.parse import parse_qs, urlparse
import threading
import shutil


# ── Logging ─────────────────────────────────────────────────────────────────

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


# ── Constants ────────────────────────────────────────────────────────────────

DEFAULT_PORT = 8003
DEFAULT_RESULTS_DIR = Path("test_results")
CACHE_TTL_DEFAULT = 30  # seconds
FRESHNESS_UNIT_HOURS = 48
FRESHNESS_INTEGRATION_HOURS = 7 * 24


# ── Test Result Validator ───────────────────────────────────────────────────

@dataclass
class ValidationResult:
    """Result of test result validation."""
    is_valid: bool
    errors: List[str] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)


class TestResultValidator:
    """Validates test result JSON files against the minimal contract."""

    REQUIRED_FIELDS = ["module", "timestamp"]
    SUMMARY_FIELDS = ["total", "passed", "failed", "error", "skipped"]

    def validate(self, data: Dict[str, Any]) -> ValidationResult:
        """Validate a test result data structure."""
        result = ValidationResult(is_valid=True)

        # Check required fields
        for field in self.REQUIRED_FIELDS:
            if field not in data:
                result.is_valid = False
                result.errors.append(f"Missing required field: {field}")

        # Validate summary consistency
        if "summary" in data:
            summary = data["summary"]
            if "total" in summary:
                total = summary["total"]
                passed = summary.get("passed", 0)
                failed = summary.get("failed", 0)
                error = summary.get("error", 0)
                skipped = summary.get("skipped", 0)

                if total != passed + failed + error + skipped:
                    result.warnings.append(
                        f"Summary total ({total}) != sum of parts ({passed + failed + error + skipped})"
                    )

        # Validate timestamp format
        if "timestamp" in data:
            timestamp = data["timestamp"]
            try:
                # Try parsing ISO 8601 format
                if isinstance(timestamp, str):
                    datetime.fromisoformat(timestamp.replace('Z', '+00:00'))
            except (ValueError, AttributeError) as e:
                result.is_valid = False
                result.errors.append(f"Invalid timestamp format: {timestamp}")

        return result


# ── Test Results Data Source ────────────────────────────────────────────────

@dataclass
class ModuleTestResult:
    """Parsed test result for a single module."""
    module: str
    total: int
    passed: int
    failed: int
    error: int
    skipped: int
    timestamp: str
    failures: List[Dict[str, Any]] = field(default_factory=list)
    coverage: Dict[str, Any] = field(default_factory=dict)
    raw_data: Dict[str, Any] = field(default_factory=dict)


class TestResultsDataSource:
    """Data source for standardized test results with caching."""

    def __init__(
        self,
        results_dir: Path,
        cache_ttl: int = CACHE_TTL_DEFAULT,
        validator: Optional[TestResultValidator] = None
    ):
        self.results_dir = results_dir
        self.cache_ttl = cache_ttl
        self._validator = validator or TestResultValidator()
        self._cache: Dict[str, Tuple[Dict[str, Any], float]] = {}
        self._load_time: float = 0

    def load_results(self, module: Optional[str] = None) -> Dict[str, ModuleTestResult]:
        """Load test results for a module or all modules.

        Args:
            module: Optional module name to filter results

        Returns:
            Dictionary mapping module names to ModuleTestResult instances
        """
        cache_key = f"results_{module or 'all'}"
        now = time.time()

        # Check cache
        if cache_key in self._cache:
            data, cached_at = self._cache[cache_key]
            if now - cached_at < self.cache_ttl:
                logger.debug(f"Using cached data for {cache_key}")
                return data

        # Load from disk
        results = {}
        pattern = f"{module}_unit.json" if module else "*_unit.json"

        for json_file in self.results_dir.glob(pattern):
            try:
                with open(json_file, 'r', encoding='utf-8') as f:
                    raw_data = json.load(f)

                # Validate
                validation = self._validator.validate(raw_data)
                if not validation.is_valid:
                    logger.warning(f"Invalid test result in {json_file}: {validation.errors}")
                    continue

                # Parse
                module_name = raw_data.get("module", json_file.stem.replace("_unit", ""))
                summary = raw_data.get("summary", {})

                result = ModuleTestResult(
                    module=module_name,
                    total=summary.get("total", 0),
                    passed=summary.get("passed", 0),
                    failed=summary.get("failed", 0),
                    error=summary.get("error", 0),
                    skipped=summary.get("skipped", 0),
                    timestamp=raw_data.get("timestamp", ""),
                    failures=raw_data.get("failures", []),
                    coverage=raw_data.get("coverage", {}),
                    raw_data=raw_data
                )

                results[module_name] = result

            except (json.JSONDecodeError, KeyError) as e:
                logger.warning(f"Failed to parse {json_file}: {e}")

        # Cache results
        self._cache[cache_key] = (results, now)
        self._load_time = now

        logger.info(f"Loaded test results for {len(results)} modules")
        return results

    def get_cached_data(self) -> Optional[Dict[str, ModuleTestResult]]:
        """Get cached data if fresh, otherwise None."""
        if self._load_time > 0:
            age = time.time() - self._load_time
            if age < self.cache_ttl:
                return self.load_results()
        return None

    def invalidate_cache(self) -> None:
        """Invalidate the cache."""
        self._cache.clear()
        self._load_time = 0
        logger.info("Cache invalidated")

    def list_modules(self) -> List[str]:
        """List all available modules with test results."""
        modules = set()
        for json_file in self.results_dir.glob("*_unit.json"):
            module_name = json_file.stem.replace("_unit", "")
            modules.add(module_name)
        return sorted(modules)

    def validate_schema(self, module: Optional[str] = None) -> Dict[str, ValidationResult]:
        """Validate all test result files against the schema.

        Returns:
            Dictionary mapping file names to ValidationResult
        """
        results = {}
        pattern = f"{module}_unit.json" if module else "*_unit.json"

        for json_file in self.results_dir.glob(pattern):
            try:
                with open(json_file, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                results[json_file.name] = self._validator.validate(data)
            except Exception as e:
                results[json_file.name] = ValidationResult(
                    is_valid=False,
                    errors=[f"Failed to load file: {e}"]
                )

        return results


# ── Test Results Analyzer ───────────────────────────────────────────────────

@dataclass
class AggregateStatistics:
    """Aggregated test statistics."""
    total_modules: int
    total_tests: int
    passed_tests: int
    failed_tests: int
    error_tests: int
    skipped_tests: int
    overall_pass_rate: float
    failing_modules: List[str]
    fresh_modules: List[str]
    stale_modules: List[str]


class TestResultsAnalyzer:
    """Analyzer for test results data."""

    def __init__(self, data_source: TestResultsDataSource):
        self.data_source = data_source

    def aggregate_statistics(self) -> AggregateStatistics:
        """Aggregate statistics across all modules."""
        results = self.data_source.load_results()

        total_modules = len(results)
        total_tests = sum(r.total for r in results.values())
        passed_tests = sum(r.passed for r in results.values())
        failed_tests = sum(r.failed for r in results.values())
        error_tests = sum(r.error for r in results.values())
        skipped_tests = sum(r.skipped for r in results.values())

        overall_pass_rate = (
            passed_tests / total_tests if total_tests > 0 else 0.0
        )

        failing_modules = [
            m for m, r in results.items() if r.failed > 0 or r.error > 0
        ]

        # Check freshness
        fresh_modules, stale_modules = self._check_freshness(results)

        return AggregateStatistics(
            total_modules=total_modules,
            total_tests=total_tests,
            passed_tests=passed_tests,
            failed_tests=failed_tests,
            error_tests=error_tests,
            skipped_tests=skipped_tests,
            overall_pass_rate=overall_pass_rate,
            failing_modules=failing_modules,
            fresh_modules=fresh_modules,
            stale_modules=stale_modules
        )

    def calculate_pass_rate(self, module: Optional[str] = None) -> float:
        """Calculate pass rate for a module or overall."""
        results = self.data_source.load_results(module)

        if module:
            result = results.get(module)
            if not result or result.total == 0:
                return 0.0
            return result.passed / result.total

        # Overall pass rate
        total = sum(r.total for r in results.values())
        passed = sum(r.passed for r in results.values())
        return passed / total if total > 0 else 0.0

    def identify_failing_tests(self) -> Dict[str, List[Dict[str, Any]]]:
        """Identify all failing tests grouped by module."""
        results = self.data_source.load_results()
        failures = {}

        for module, result in results.items():
            if result.failures:
                failures[module] = result.failures

        return failures

    def get_freshness_report(self) -> Dict[str, Any]:
        """Get freshness report for all test results."""
        results = self.data_source.load_results()
        fresh_modules = []
        stale_modules = []
        freshness_by_module = {}

        now = datetime.now(timezone.utc)

        for module, result in results.items():
            try:
                timestamp = datetime.fromisoformat(
                    result.timestamp.replace('Z', '+00:00')
                )
                age_hours = (now - timestamp).total_seconds() / 3600

                is_fresh = age_hours < FRESHNESS_UNIT_HOURS
                freshness_by_module[module] = {
                    "timestamp": result.timestamp,
                    "age_hours": round(age_hours, 1),
                    "is_fresh": is_fresh
                }

                if is_fresh:
                    fresh_modules.append(module)
                else:
                    stale_modules.append(module)

            except ValueError:
                freshness_by_module[module] = {
                    "timestamp": result.timestamp,
                    "age_hours": None,
                    "is_fresh": False
                }
                stale_modules.append(module)

        return {
            "fresh_modules": fresh_modules,
            "stale_modules": stale_modules,
            "freshness_by_module": freshness_by_module,
            "total_fresh": len(fresh_modules),
            "total_stale": len(stale_modules)
        }

    def generate_summary(self) -> Dict[str, Any]:
        """Generate a comprehensive summary of test results."""
        stats = self.aggregate_statistics()
        failures = self.identify_failing_tests()
        freshness = self.get_freshness_report()

        return {
            "statistics": {
                "total_modules": stats.total_modules,
                "total_tests": stats.total_tests,
                "passed_tests": stats.passed_tests,
                "failed_tests": stats.failed_tests,
                "error_tests": stats.error_tests,
                "skipped_tests": stats.skipped_tests,
                "overall_pass_rate": round(stats.overall_pass_rate * 100, 1),
                "failing_module_count": len(stats.failing_modules)
            },
            "failing_modules": stats.failing_modules,
            "failures": failures,
            "freshness": freshness
        }

    def _check_freshness(
        self, results: Dict[str, ModuleTestResult]
    ) -> Tuple[List[str], List[str]]:
        """Check freshness of test results."""
        fresh, stale = [], []
        now = datetime.now(timezone.utc)

        for module, result in results.items():
            try:
                timestamp = datetime.fromisoformat(
                    result.timestamp.replace('Z', '+00:00')
                )
                age_hours = (now - timestamp).total_seconds() / 3600

                if age_hours < FRESHNESS_UNIT_HOURS:
                    fresh.append(module)
                else:
                    stale.append(module)
            except ValueError:
                stale.append(module)

        return fresh, stale


# ── Test Runner API ───────────────────────────────────────────────────────────

@dataclass
class TestRun:
    """Active test run information."""
    run_id: str
    module: str
    status: str  # running, completed, failed, cancelled
    start_time: float
    end_time: Optional[float] = None
    output: List[str] = field(default_factory=list)
    result: Optional[Dict[str, Any]] = None


class TestRunnerAPI:
    """API for triggering and managing test runs."""

    def __init__(self, project_root: Path):
        self.project_root = project_root
        self.test_runner_path = (
            project_root / ".claude" / "skills" / "module-test" / "test_runner.py"
        )
        self._active_runs: Dict[str, TestRun] = {}
        self._processes: Dict[str, subprocess.Popen] = {}
        self._lock = threading.Lock()

        # Verify test runner exists
        if not self.test_runner_path.exists():
            logger.warning(f"Test runner not found at {self.test_runner_path}")

    def run_module_tests(self, module: str) -> str:
        """Run tests for a specific module.

        Returns:
            run_id: Unique identifier for this test run
        """
        run_id = f"run_{uuid.uuid4().hex[:12]}"

        with self._lock:
            # Create run record
            self._active_runs[run_id] = TestRun(
                run_id=run_id,
                module=module,
                status="running",
                start_time=time.time()
            )

        # Start test process in background
        cmd = [sys.executable, str(self.test_runner_path), module]

        try:
            process = subprocess.Popen(
                cmd,
                cwd=self.project_root,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True
            )

            with self._lock:
                self._processes[run_id] = process

            # Start monitoring thread
            threading.Thread(
                target=self._monitor_run,
                args=(run_id, process),
                daemon=True
            ).start()

            logger.info(f"Started test run {run_id} for module {module}")
            return run_id

        except Exception as e:
            with self._lock:
                self._active_runs[run_id].status = "failed"
                self._active_runs[run_id].result = {"error": str(e)}

            logger.error(f"Failed to start test run {run_id}: {e}")
            return run_id

    def run_all_tests(self) -> str:
        """Run tests for all available modules."""
        modules = self._get_available_modules()
        return self.run_module_tests(" ".join(modules))

    def cancel_run(self, run_id: str) -> bool:
        """Cancel an active test run."""
        with self._lock:
            if run_id not in self._active_runs:
                return False

            run = self._active_runs[run_id]
            if run.status != "running":
                return False

            # Terminate process
            process = self._processes.get(run_id)
            if process:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()

            run.status = "cancelled"
            run.end_time = time.time()

            logger.info(f"Cancelled test run {run_id}")
            return True

    def get_run_status(self, run_id: str) -> Optional[Dict[str, Any]]:
        """Get status of a test run."""
        with self._lock:
            run = self._active_runs.get(run_id)
            if not run:
                return None

            return {
                "run_id": run.run_id,
                "module": run.module,
                "status": run.status,
                "start_time": run.start_time,
                "end_time": run.end_time,
                "duration": (
                    (run.end_time or time.time()) - run.start_time
                    if run.end_time or run.status != "running"
                    else time.time() - run.start_time
                ),
                "output": run.output,
                "result": run.result
            }

    def list_active_runs(self) -> List[Dict[str, Any]]:
        """List all active and recent test runs."""
        with self._lock:
            runs = []
            for run in self._active_runs.values():
                runs.append({
                    "run_id": run.run_id,
                    "module": run.module,
                    "status": run.status,
                    "start_time": run.start_time,
                    "duration": time.time() - run.start_time
                })

            # Sort by start time, most recent first
            runs.sort(key=lambda r: r["start_time"], reverse=True)
            return runs

    def _monitor_run(self, run_id: str, process: subprocess.Popen):
        """Monitor a test run and capture output."""
        try:
            stdout, stderr = process.communicate()

            with self._lock:
                run = self._active_runs.get(run_id)
                if not run:
                    return

                # Capture output
                if stdout:
                    run.output.extend(stdout.strip().split('\n'))
                if stderr:
                    run.output.extend(stderr.strip().split('\n'))

                # Update status
                run.end_time = time.time()

                if run.status == "cancelled":
                    return

                if process.returncode == 0:
                    run.status = "completed"
                else:
                    run.status = "failed"

                # Try to parse result from output
                run.result = self._parse_result_from_output(run.output, run.module)

            logger.info(f"Test run {run_id} completed with status {run.status}")

        except Exception as e:
            with self._lock:
                run = self._active_runs.get(run_id)
                if run:
                    run.status = "failed"
                    run.result = {"error": str(e)}

            logger.error(f"Error monitoring run {run_id}: {e}")

    def _parse_result_from_output(self, output: List[str], module: str) -> Dict[str, Any]:
        """Parse test result from output lines."""
        import re

        result = {
            "module": module,
            "summary": {"total": 0, "passed": 0, "failed": 0, "error": 0, "skipped": 0}
        }

        for line in output:
            # Look for pytest summary line
            if " passed" in line or " failed" in line:
                match = re.search(r'(\d+) passed', line)
                if match:
                    result["summary"]["passed"] = int(match.group(1))

                match = re.search(r'(\d+) failed', line)
                if match:
                    result["summary"]["failed"] = int(match.group(1))

        result["summary"]["total"] = (
            result["summary"]["passed"] + result["summary"]["failed"]
        )

        return result

    def _get_available_modules(self) -> List[str]:
        """Get list of modules with test results."""
        results_dir = self.project_root / "test_results"
        modules = set()

        for json_file in results_dir.glob("*_unit.json"):
            module_name = json_file.stem.replace("_unit", "")
            modules.add(module_name)

        return sorted(modules)


# ── HTTP Request Handler ─────────────────────────────────────────────────────

class TestReportRequestHandler(SimpleHTTPRequestHandler):
    """HTTP request handler for test report server."""

    # Class variables set by main()
    data_source: TestResultsDataSource = None
    analyzer: TestResultsAnalyzer = None
    test_runner: TestRunnerAPI = None

    def log_message(self, format, *args):
        """Override to use custom logger."""
        logger.info(f"{self.address_string()} - {format % args}")

    def do_GET(self):
        """Handle GET requests."""
        parsed = urlparse(self.path)
        path = parsed.path

        if path == "/" or path == "/index.html":
            self._serve_dashboard()
        elif path == "/api/results":
            self._serve_results(parsed.query)
        elif path == "/api/aggregate":
            self._serve_aggregate()
        elif path == "/api/failures":
            self._serve_failures()
        elif path == "/api/freshness":
            self._serve_freshness()
        elif path == "/api/runs":
            self._serve_runs()
        elif path.startswith("/api/run-status/"):
            run_id = path.split("/")[-1]
            self._serve_run_status(run_id)
        else:
            self._serve_error(404, "Not found")

    def do_POST(self):
        """Handle POST requests."""
        parsed = urlparse(self.path)
        path = parsed.path

        if path == "/api/trigger":
            self._handle_trigger()
        elif path == "/api/cancel":
            self._handle_cancel()
        elif path == "/api/refresh":
            self._handle_refresh()
        else:
            self._serve_error(404, "Not found")

    def _serve_dashboard(self):
        """Serve the embedded HTML dashboard."""
        html = self._get_dashboard_html()
        self._send_html(html)

    def _serve_results(self, query: str):
        """Serve per-module test results."""
        params = parse_qs(query)
        module = params.get("module", [None])[0]

        results = self.data_source.load_results(module)

        # Convert to dict for JSON serialization
        output = {}
        for mod, result in results.items():
            output[mod] = {
                "module": result.module,
                "total": result.total,
                "passed": result.passed,
                "failed": result.failed,
                "error": result.error,
                "skipped": result.skipped,
                "timestamp": result.timestamp,
                "failures": result.failures,
                "coverage": result.coverage
            }

        self._serve_json(output)

    def _serve_aggregate(self):
        """Serve aggregated statistics."""
        stats = self.analyzer.aggregate_statistics()
        self._serve_json({
            "total_modules": stats.total_modules,
            "total_tests": stats.total_tests,
            "passed_tests": stats.passed_tests,
            "failed_tests": stats.failed_tests,
            "error_tests": stats.error_tests,
            "skipped_tests": stats.skipped_tests,
            "overall_pass_rate": round(stats.overall_pass_rate * 100, 1),
            "failing_modules": stats.failing_modules,
            "fresh_modules": stats.fresh_modules,
            "stale_modules": stats.stale_modules
        })

    def _serve_failures(self):
        """Serve failing tests grouped by module."""
        failures = self.analyzer.identify_failing_tests()
        self._serve_json(failures)

    def _serve_freshness(self):
        """Serve freshness report."""
        freshness = self.analyzer.get_freshness_report()
        self._serve_json(freshness)

    def _serve_runs(self):
        """Serve list of active and recent runs."""
        runs = self.test_runner.list_active_runs()
        self._serve_json(runs)

    def _serve_run_status(self, run_id: str):
        """Serve status of a specific run."""
        status = self.test_runner.get_run_status(run_id)
        if status:
            self._serve_json(status)
        else:
            self._serve_error(404, f"Run {run_id} not found")

    def _handle_trigger(self):
        """Handle test trigger request."""
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length).decode("utf-8")

        try:
            data = json.loads(body) if body else {}
            module = data.get("module", "")

            if not module:
                self._serve_error(400, "Module name is required")
                return

            run_id = self.test_runner.run_module_tests(module)
            self._serve_json({"run_id": run_id, "status": "started"})

        except json.JSONDecodeError:
            self._serve_error(400, "Invalid JSON")

    def _handle_cancel(self):
        """Handle test cancel request."""
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length).decode("utf-8")

        try:
            data = json.loads(body) if body else {}
            run_id = data.get("run_id", "")

            if not run_id:
                self._serve_error(400, "Run ID is required")
                return

            cancelled = self.test_runner.cancel_run(run_id)
            self._serve_json({"cancelled": cancelled})

        except json.JSONDecodeError:
            self._serve_error(400, "Invalid JSON")

    def _handle_refresh(self):
        """Handle cache refresh request."""
        self.data_source.invalidate_cache()
        self._serve_json({"refreshed": True})

    def _serve_json(self, data: Any):
        """Serve JSON response."""
        body = json.dumps(data, default=str, ensure_ascii=False).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _serve_error(self, code: int, message: str):
        """Serve error response."""
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(json.dumps({"error": message}).encode("utf-8"))

    def _send_html(self, html: str):
        """Serve HTML response."""
        body = html.encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _get_dashboard_html(self) -> str:
        """Generate the embedded HTML dashboard."""
        return """<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Test Report Dashboard</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
        }

        .header {
            text-align: center;
            color: white;
            margin-bottom: 30px;
        }

        .header h1 {
            font-size: 2.5rem;
            margin-bottom: 10px;
        }

        .header p {
            opacity: 0.9;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            border-radius: 12px;
            padding: 24px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            text-align: center;
        }

        .stat-value {
            font-size: 2.5rem;
            font-weight: 700;
            margin-bottom: 8px;
        }

        .stat-label {
            color: #6b7280;
            font-size: 0.9rem;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .stat-value.passed { color: #10b981; }
        .stat-value.failed { color: #ef4444; }
        .stat-value.total { color: #6366f1; }
        .stat-value.rate { color: #8b5cf6; }

        .module-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .module-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        }

        .module-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 16px;
        }

        .module-name {
            font-size: 1.2rem;
            font-weight: 600;
            color: #1f2937;
        }

        .status-badge {
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 0.8rem;
            font-weight: 600;
        }

        .status-badge.passed {
            background: #d1fae5;
            color: #065f46;
        }

        .status-badge.failed {
            background: #fee2e2;
            color: #991b1b;
        }

        .status-badge.stale {
            background: #fef3c7;
            color: #92400e;
        }

        .module-stats {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 12px;
        }

        .module-stat {
            text-align: center;
        }

        .module-stat-value {
            font-size: 1.5rem;
            font-weight: 600;
        }

        .module-stat-label {
            font-size: 0.8rem;
            color: #6b7280;
        }

        .failures-section {
            background: white;
            border-radius: 12px;
            padding: 24px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            margin-bottom: 30px;
        }

        .section-title {
            font-size: 1.3rem;
            font-weight: 600;
            margin-bottom: 20px;
            color: #1f2937;
        }

        .failures-table {
            width: 100%;
            border-collapse: collapse;
        }

        .failures-table th {
            text-align: left;
            padding: 12px;
            border-bottom: 2px solid #e5e7eb;
            color: #6b7280;
            font-weight: 600;
        }

        .failures-table td {
            padding: 12px;
            border-bottom: 1px solid #f3f4f6;
        }

        .failure-message {
            max-width: 500px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }

        .actions-section {
            background: white;
            border-radius: 12px;
            padding: 24px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            margin-bottom: 30px;
        }

        .actions-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 12px;
        }

        .btn {
            padding: 12px 24px;
            border: none;
            border-radius: 8px;
            font-size: 1rem;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        }

        .btn:disabled {
            opacity: 0.5;
            cursor: not-allowed;
            transform: none;
        }

        .btn-primary {
            background: #6366f1;
            color: white;
        }

        .btn-secondary {
            background: #8b5cf6;
            color: white;
        }

        .btn-success {
            background: #10b981;
            color: white;
        }

        .active-runs {
            background: white;
            border-radius: 12px;
            padding: 24px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        }

        .run-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 12px;
            border-bottom: 1px solid #f3f4f6;
        }

        .run-item:last-child {
            border-bottom: none;
        }

        .run-status {
            padding: 4px 12px;
            border-radius: 12px;
            font-size: 0.8rem;
            font-weight: 600;
        }

        .run-status.running {
            background: #dbeafe;
            color: #1e40af;
        }

        .run-status.completed {
            background: #d1fae5;
            color: #065f46;
        }

        .run-status.failed {
            background: #fee2e2;
            color: #991b1b;
        }

        .loading {
            text-align: center;
            padding: 40px;
            color: white;
        }

        .spinner {
            border: 4px solid rgba(255, 255, 255, 0.3);
            border-top-color: white;
            border-radius: 50%;
            width: 40px;
            height: 40px;
            animation: spin 1s linear infinite;
            margin: 0 auto 16px;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }

        .last-updated {
            text-align: center;
            color: rgba(255, 255, 255, 0.8);
            font-size: 0.9rem;
            margin-top: 20px;
        }

        .empty-state {
            text-align: center;
            padding: 40px;
            color: #6b7280;
        }

        @media (max-width: 768px) {
            .stats-grid {
                grid-template-columns: repeat(2, 1fr);
            }

            .header h1 {
                font-size: 1.8rem;
            }
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🧪 Test Report Dashboard</h1>
            <p>Real-time test results and analytics</p>
        </div>

        <div id="loading" class="loading">
            <div class="spinner"></div>
            <p>Loading test results...</p>
        </div>

        <div id="content" style="display: none;">
            <div class="stats-grid">
                <div class="stat-card">
                    <div class="stat-value total" id="totalTests">-</div>
                    <div class="stat-label">Total Tests</div>
                </div>
                <div class="stat-card">
                    <div class="stat-value passed" id="passedTests">-</div>
                    <div class="stat-label">Passed</div>
                </div>
                <div class="stat-card">
                    <div class="stat-value failed" id="failedTests">-</div>
                    <div class="stat-label">Failed</div>
                </div>
                <div class="stat-card">
                    <div class="stat-value rate" id="passRate">-</div>
                    <div class="stat-label">Pass Rate</div>
                </div>
            </div>

            <div class="actions-section">
                <h3 class="section-title">Actions</h3>
                <div class="actions-grid">
                    <button class="btn btn-primary" onclick="refreshData()">
                        🔄 Refresh Data
                    </button>
                    <button class="btn btn-secondary" onclick="runAllTests()">
                        ▶️ Run All Tests
                    </button>
                    <button class="btn btn-success" onclick="toggleAutoRefresh()" id="autoRefreshBtn">
                        ⏱️ Enable Auto-Refresh
                    </button>
                </div>
            </div>

            <div class="active-runs" id="activeRuns" style="display: none;">
                <h3 class="section-title">Active Runs</h3>
                <div id="runsList"></div>
            </div>

            <h3 class="section-title" style="color: white; margin-top: 30px;">Module Status</h3>
            <div class="module-grid" id="moduleGrid"></div>

            <div class="failures-section" id="failuresSection" style="display: none;">
                <h3 class="section-title">Failed Tests</h3>
                <table class="failures-table">
                    <thead>
                        <tr>
                            <th>Module</th>
                            <th>Test</th>
                            <th>Message</th>
                        </tr>
                    </thead>
                    <tbody id="failuresTable"></tbody>
                </table>
            </div>
        </div>

        <div class="last-updated">
            Last updated: <span id="lastUpdated">-</span>
        </div>
    </div>

    <script>
        let autoRefreshEnabled = false;
        let autoRefreshInterval = null;
        const API_BASE = '';

        async function fetchJSON(url, options = {}) {
            try {
                const response = await fetch(url, options);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return await response.json();
            } catch (error) {
                console.error(`API error: ${error.message}`);
                return null;
            }
        }

        function updateStats(stats) {
            document.getElementById('totalTests').textContent = stats.total_tests || 0;
            document.getElementById('passedTests').textContent = stats.passed_tests || 0;
            document.getElementById('failedTests').textContent = (stats.failed_tests + stats.error_tests) || 0;
            document.getElementById('passRate').textContent = (stats.overall_pass_rate || 0) + '%';
        }

        function updateModuleResults(results) {
            const grid = document.getElementById('moduleGrid');
            grid.innerHTML = '';

            const freshness = fetchJSON(`${API_BASE}/api/freshness`) || { freshness_by_module: {} };
            const staleModules = new Set((freshness.stale_modules || []).map(m => m.toLowerCase()));

            for (const [module, data] of Object.entries(results)) {
                const isFailed = data.failed > 0 || data.error > 0;
                const isStale = staleModules.has(module.toLowerCase());
                const statusClass = isFailed ? 'failed' : (isStale ? 'stale' : 'passed');
                const statusText = isFailed ? 'Failed' : (isStale ? 'Stale' : 'Passed');

                const card = document.createElement('div');
                card.className = 'module-card';
                card.innerHTML = `
                    <div class="module-header">
                        <div class="module-name">${module}</div>
                        <span class="status-badge ${statusClass}">${statusText}</span>
                    </div>
                    <div class="module-stats">
                        <div class="module-stat">
                            <div class="module-stat-value">${data.passed}</div>
                            <div class="module-stat-label">Passed</div>
                        </div>
                        <div class="module-stat">
                            <div class="module-stat-value" style="color: ${isFailed ? '#ef4444' : '#10b981'}">${data.failed + data.error}</div>
                            <div class="module-stat-label">Failed</div>
                        </div>
                        <div class="module-stat">
                            <div class="module-stat-value">${data.skipped}</div>
                            <div class="module-stat-label">Skipped</div>
                        </div>
                        <div class="module-stat">
                            <div class="module-stat-value">${data.total}</div>
                            <div class="module-stat-label">Total</div>
                        </div>
                    </div>
                    <button class="btn btn-secondary" style="width: 100%; margin-top: 16px;" onclick="runModuleTest('${module}')">
                        ▶️ Re-run
                    </button>
                `;
                grid.appendChild(card);
            }
        }

        function updateFailures(failures) {
            const section = document.getElementById('failuresSection');
            const table = document.getElementById('failuresTable');

            if (!failures || Object.keys(failures).length === 0) {
                section.style.display = 'none';
                return;
            }

            section.style.display = 'block';
            table.innerHTML = '';

            for (const [module, tests] of Object.entries(failures)) {
                for (const test of tests) {
                    const row = document.createElement('tr');
                    row.innerHTML = `
                        <td><strong>${module}</strong></td>
                        <td>${test.name || 'Unknown'}</td>
                        <td><div class="failure-message">${test.message || 'No message'}</div></td>
                    `;
                    table.appendChild(row);
                }
            }
        }

        async function refreshData() {
            const stats = await fetchJSON(`${API_BASE}/api/aggregate`);
            const results = await fetchJSON(`${API_BASE}/api/results`);
            const failures = await fetchJSON(`${API_BASE}/api/failures`);

            if (stats) updateStats(stats);
            if (results) updateModuleResults(results);
            if (failures) updateFailures(failures);

            document.getElementById('lastUpdated').textContent = new Date().toLocaleTimeString();
        }

        async function runModuleTest(module) {
            const response = await fetchJSON(`${API_BASE}/api/trigger`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ module })
            });

            if (response && response.run_id) {
                alert(`Test run started: ${response.run_id}`);
                setTimeout(refreshData, 2000);
            }
        }

        async function runAllTests() {
            if (!confirm('Run all tests? This may take a while.')) return;

            const response = await fetchJSON(`${API_BASE}/api/trigger`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ module: 'all' })
            });

            if (response && response.run_id) {
                alert(`Test run started: ${response.run_id}`);
                setTimeout(refreshData, 2000);
            }
        }

        function toggleAutoRefresh() {
            const btn = document.getElementById('autoRefreshBtn');
            autoRefreshEnabled = !autoRefreshEnabled;

            if (autoRefreshEnabled) {
                autoRefreshInterval = setInterval(refreshData, 30000);
                btn.textContent = '⏱️ Disable Auto-Refresh';
                btn.style.background = '#ef4444';
            } else {
                clearInterval(autoRefreshInterval);
                btn.textContent = '⏱️ Enable Auto-Refresh';
                btn.style.background = '#10b981';
            }
        }

        // Initial load
        refreshData().then(() => {
            document.getElementById('loading').style.display = 'none';
            document.getElementById('content').style.display = 'block';
        });
    </script>
</body>
</html>"""


# ── Main ─────────────────────────────────────────────────────────────────────

def main():
    """Start the test report server."""
    parser = argparse.ArgumentParser(
        description="Test Report Server - HTTP server for test results visualization"
    )
    parser.add_argument(
        "--port", type=int, default=DEFAULT_PORT,
        help=f"Port to listen on (default: {DEFAULT_PORT})"
    )
    parser.add_argument(
        "--host", default="127.0.0.1",
        help="Host to bind to (default: 127.0.0.1)"
    )
    parser.add_argument(
        "--results-dir", type=Path, default=DEFAULT_RESULTS_DIR,
        help=f"Directory containing test results (default: {DEFAULT_RESULTS_DIR})"
    )
    parser.add_argument(
        "--cache-ttl", type=int, default=CACHE_TTL_DEFAULT,
        help=f"Cache TTL in seconds (default: {CACHE_TTL_DEFAULT})"
    )

    args = parser.parse_args()

    # Validate results directory
    if not args.results_dir.exists():
        logger.warning(f"Results directory does not exist: {args.results_dir}")
        args.results_dir.mkdir(parents=True, exist_ok=True)

    # Initialize components
    project_root = Path.cwd()
    data_source = TestResultsDataSource(args.results_dir, args.cache_ttl)
    analyzer = TestResultsAnalyzer(data_source)
    test_runner = TestRunnerAPI(project_root)

    # Set up handler
    TestReportRequestHandler.data_source = data_source
    TestReportRequestHandler.analyzer = analyzer
    TestReportRequestHandler.test_runner = test_runner

    # Start server
    server = HTTPServer((args.host, args.port), TestReportRequestHandler)

    logger.info(f"Starting Test Report Server on http://{args.host}:{args.port}")
    logger.info(f"Results directory: {args.results_dir.absolute()}")
    logger.info(f"Project root: {project_root.absolute()}")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        logger.info("Shutting down server...")
        server.shutdown()


if __name__ == "__main__":
    main()
