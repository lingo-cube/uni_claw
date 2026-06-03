"""
Simulation report generator for testing framework.

Provides comprehensive report generation in JSON and HTML formats
for simulation test results.
"""

import json
import time
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional
from datetime import datetime


@dataclass
class TestReport:
    """Comprehensive test report data structure."""
    test_id: str
    description: str
    timestamp: str
    passed: bool
    execution_time: float
    completion_reason: str

    # Test details
    total_steps: int
    unique_nodes: int
    action_count: int

    # Assertion details
    key_events_matched: int
    missing_events: List[str]
    violations: List[str]

    # Performance metrics
    avg_step_time: float
    max_step_time: float

    # Trace summary
    trace_summary: str
    visited_nodes_summary: List[str]


@dataclass
class TestReportDetails:
    """Detailed test report with full information."""
    report: TestReport
    full_trace: List[Dict[str, Any]]
    executed_actions: List[Dict[str, Any]]
    visited_tree: Dict[str, Dict[str, Any]]
    statistics: Dict[str, Any]

    # Timing analysis
    step_timing: List[Dict[str, Any]]
    bottleneck_analysis: str

    # Recommendations
    recommendations: List[str]


class SimulationReportGenerator:
    """
    Generate comprehensive reports for simulation testing.

    Provides JSON and HTML report generation with detailed analysis
    and actionable recommendations.
    """

    def __init__(self):
        """Initialize report generator."""
        self._report_templates = self._load_templates()

    def generate_report(
        self,
        test_id: str,
        result: Any,
        test_case: Dict[str, Any],
        assertion_result: Any,
    ) -> TestReport:
        """
        Generate comprehensive test report.

        Args:
            test_id: Test identifier
            result: SimulationResult from runner
            test_case: Test case specification
            assertion_result: AssertionResult from TraceAsserter

        Returns:
            TestReport with comprehensive information
        """
        # Extract basic information
        trace = result.trace
        actions = result.executed_actions
        stats = result.statistics

        # Calculate timing metrics
        step_count = len(trace)
        execution_time = result.elapsed_seconds
        avg_step_time = execution_time / max(step_count, 1)

        # Calculate max step time
        step_times = []
        for i in range(1, len(trace)):
            if trace[i].get('timestamp') and trace[i-1].get('timestamp'):
                step_time = trace[i]['timestamp'] - trace[i-1]['timestamp']
                step_times.append(step_time)

        max_step_time = max(step_times) if step_times else 0

        # Generate trace summary
        trace_summary = self._generate_trace_summary(trace)

        # Extract visited nodes
        visited_nodes = list(result.visited_tree.keys())

        # Create report
        return TestReport(
            test_id=test_id,
            description=test_case.get('description', 'N/A'),
            timestamp=datetime.now().isoformat(),
            passed=assertion_result.success,
            execution_time=execution_time,
            completion_reason=result.completion_reason,

            total_steps=stats.get('total_steps', 0),
            unique_nodes=stats.get('unique_nodes', 0),
            action_count=stats.get('action_count', 0),

            key_events_matched=assertion_result.key_events_matched,
            missing_events=assertion_result.missing_events,
            violations=assertion_result.violations,

            avg_step_time=avg_step_time,
            max_step_time=max_step_time,

            trace_summary=trace_summary,
            visited_nodes_summary=visited_nodes
        )

    def generate_detailed_report(
        self,
        test_id: str,
        result: Any,
        test_case: Dict[str, Any],
        assertion_result: Any,
    ) -> TestReportDetails:
        """
        Generate detailed test report with full information.

        Args:
            test_id: Test identifier
            result: SimulationResult from runner
            test_case: Test case specification
            assertion_result: AssertionResult from TraceAsserter

        Returns:
            TestReportDetails with comprehensive information
        """
        # Generate basic report
        basic_report = self.generate_report(
            test_id, result, test_case, assertion_result
        )

        # Analyze step timing
        step_timing = self._analyze_step_timing(result.trace)

        # Generate bottleneck analysis
        bottleneck_analysis = self._analyze_bottlenecks(
            result.trace, result.executed_actions
        )

        # Generate recommendations
        recommendations = self._generate_recommendations(
            basic_report, assertion_result
        )

        return TestReportDetails(
            report=basic_report,
            full_trace=result.trace,
            executed_actions=result.executed_actions,
            visited_tree=result.visited_tree,
            statistics=result.statistics,

            step_timing=step_timing,
            bottleneck_analysis=bottleneck_analysis,
            recommendations=recommendations
        )

    def export_json_report(self, report: TestReport) -> str:
        """
        Export report as formatted JSON.

        Args:
            report: TestReport to export

        Returns:
            Formatted JSON string
        """
        return json.dumps({
            "test_id": report.test_id,
            "description": report.description,
            "timestamp": report.timestamp,
            "passed": report.passed,
            "metrics": {
                "execution_time": report.execution_time,
                "total_steps": report.total_steps,
                "unique_nodes": report.unique_nodes,
                "action_count": report.action_count,
                "avg_step_time": report.avg_step_time,
                "max_step_time": report.max_step_time
            },
            "assertions": {
                "key_events_matched": report.key_events_matched,
                "missing_events": report.missing_events,
                "violations": report.violations
            },
            "summary": {
                "completion_reason": report.completion_reason,
                "trace_summary": report.trace_summary,
                "visited_nodes": report.visited_nodes_summary
            }
        }, indent=2)

    def export_html_report(self, report: TestReport) -> str:
        """
        Export report as formatted HTML.

        Args:
            report: TestReport to export

        Returns:
            Formatted HTML string
        """
        # Determine status styling
        status_color = "#28a745" if report.passed else "#dc3545"
        status_text = "PASSED" if report.passed else "FAILED"

        html = f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Simulation Test Report - {report.test_id}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f8f9fa;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .header {{
            border-bottom: 2px solid #e9ecef;
            padding-bottom: 20px;
            margin-bottom: 20px;
        }}
        .status {{
            font-size: 24px;
            font-weight: bold;
            color: {status_color};
            margin-bottom: 10px;
        }}
        .test-info {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 15px;
            margin: 20px 0;
        }}
        .info-card {{
            background: #f8f9fa;
            padding: 15px;
            border-radius: 6px;
            border-left: 4px solid #007bff;
        }}
        .info-card h3 {{
            margin: 0 0 10px 0;
            color: #495057;
            font-size: 14px;
            text-transform: uppercase;
        }}
        .info-card .value {{
            font-size: 24px;
            font-weight: bold;
            color: #212529;
        }}
        .section {{
            margin: 30px 0;
        }}
        .section h2 {{
            color: #495057;
            border-bottom: 1px solid #dee2e6;
            padding-bottom: 10px;
        }}
        .metric-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 10px;
            margin: 20px 0;
        }}
        .metric {{
            background: #f8f9fa;
            padding: 10px;
            border-radius: 4px;
            text-align: center;
        }}
        .event-list {{
            margin: 10px 0;
        }}
        .event {{
            padding: 8px 12px;
            margin: 2px 0;
            border-radius: 4px;
            background: #e9ecef;
        }}
        .missing {{
            background: #f8d7da;
        }}
        .violation {{
            background: #fff3cd;
        }}
        .timestamp {{
            color: #6c757d;
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <div class="status">{status_text}</div>
            <h1>{report.test_id}</h1>
            <p>{report.description}</p>
            <p class="timestamp">Generated: {report.timestamp}</p>
        </div>

        <div class="test-info">
            <div class="info-card">
                <h3>Execution Time</h3>
                <div class="value">{report.execution_time:.3f}s</div>
            </div>
            <div class="info-card">
                <h3>Total Steps</h3>
                <div class="value">{report.total_steps}</div>
            </div>
            <div class="info-card">
                <h3>Unique Nodes</h3>
                <div class="value">{report.unique_nodes}</div>
            </div>
            <div class="info-card">
                <h3>Actions</h3>
                <div class="value">{report.action_count}</div>
            </div>
        </div>

        <div class="section">
            <h2>Performance Metrics</h2>
            <div class="metric-grid">
                <div class="metric">
                    <div>Avg Step Time</div>
                    <strong>{report.avg_step_time*1000:.2f}ms</strong>
                </div>
                <div class="metric">
                    <div>Max Step Time</div>
                    <strong>{report.max_step_time*1000:.2f}ms</strong>
                </div>
            </div>
        </div>

        <div class="section">
            <h2>Assertion Results</h2>
            <p><strong>Key Events Matched:</strong> {report.key_events_matched}</p>

            {self._generate_events_html(report.missing_events, "Missing Events", "missing") if report.missing_events else ""}
            {self._generate_events_html(report.violations, "Violations", "violation") if report.violations else ""}
        </div>

        <div class="section">
            <h2>Execution Summary</h2>
            <p><strong>Completion Reason:</strong> {report.completion_reason}</p>
            <p><strong>Trace Summary:</strong></p>
            <pre>{report.trace_summary}</pre>
        </div>

        <div class="section">
            <h2>Visited Nodes</h2>
            <div class="event-list">
                {"".join(f'<div class="event">{node}</div>' for node in report.visited_nodes_summary)}
            </div>
        </div>
    </div>
</body>
</html>"""

        return html

    def _load_templates(self) -> Dict[str, str]:
        """Load report templates."""
        return {
            "json": "json_format",
            "html": "html_format"
        }

    def _generate_trace_summary(self, trace: List[Dict[str, Any]]) -> str:
        """Generate human-readable trace summary."""
        if not trace:
            return "Empty trace"

        summary_lines = []
        for step in trace[:10]:  # Show first 10 steps
            action_type = step.get("action_type", "unknown")
            current_node = step.get("current_node", "unknown")
            summary_lines.append(f"- {action_type} on {current_node}")

        if len(trace) > 10:
            summary_lines.append(f"... and {len(trace) - 10} more steps")

        return "\n".join(summary_lines)

    def _analyze_step_timing(self, trace: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Analyze step timing patterns."""
        timing_analysis = []

        for i in range(1, len(trace)):
            if trace[i].get('timestamp') and trace[i-1].get('timestamp'):
                step_time = trace[i]['timestamp'] - trace[i-1]['timestamp']
                timing_analysis.append({
                    "step_number": i,
                    "action": trace[i].get('action_type', 'unknown'),
                    "duration": step_time,
                    "duration_ms": step_time * 1000
                })

        return timing_analysis

    def _analyze_bottlenecks(
        self,
        trace: List[Dict[str, Any]],
        actions: List[Dict[str, Any]]
    ) -> str:
        """Analyze performance bottlenecks."""
        bottlenecks = []

        # Check for slow steps
        if len(trace) > 1:
            avg_time = sum(trace[i].get('timestamp', trace[i-1].get('timestamp', 0)) - trace[i-1].get('timestamp', 0)
                           for i in range(1, len(trace))) / max(len(trace)-1, 1)

            for i in range(1, len(trace)):
                step_time = trace[i].get('timestamp', 0) - trace[i-1].get('timestamp', 0)
                if step_time > avg_time * 2:
                    bottlenecks.append(f"Slow step {i}: {step_time*1000:.2f}ms")

        if bottlenecks:
            return "Bottlenecks: " + ", ".join(bottlenecks)
        else:
            return "No significant bottlenecks detected"

    def _generate_recommendations(
        self,
        report: TestReport,
        assertion_result: Any
    ) -> List[str]:
        """Generate actionable recommendations."""
        recommendations = []

        # Performance recommendations
        if report.avg_step_time > 0.001:  # >1ms
            recommendations.append("Consider caching page analyses to improve performance")

        if report.execution_time > 5.0:
            recommendations.append("Test execution time exceeds 5s target - consider optimization")

        # Assertion recommendations
        if assertion_result.missing_events:
            recommendations.append("Review expected events - some may be too specific")

        if assertion_result.violations:
            recommendations.append("Fix violations detected during execution")

        # Coverage recommendations
        if report.unique_nodes < 3:
            recommendations.append("Low node coverage - consider expanding test scope")

        if not recommendations:
            recommendations.append("Test looks good! No immediate improvements needed.")

        return recommendations

    def _generate_events_html(
        self,
        events: List[str],
        title: str,
        css_class: str
    ) -> str:
        """Generate HTML for events list."""
        if not events:
            return ""

        events_html = f"<p><strong>{title}:</strong></p><div class='event-list'>"
        for event in events:
            events_html += f"<div class='event {css_class}'>{event}</div>"
        events_html += "</div>"

        return events_html