"""Structured result management for traversal operations."""

import json
import time
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Any, Dict, List, Optional
import logging

logger = logging.getLogger(__name__)


class ResultStatus(Enum):
    """Status of a traversal result."""
    SUCCESS = "success"
    PARTIAL = "partial"
    FAILED = "failed"
    CANCELLED = "cancelled"


@dataclass
class StepResult:
    """Result of a single traversal step."""

    step_number: int
    action: str  # tap, back, swipe, scroll
    target: Optional[str]  # Element name or null
    coordinate: Optional[Dict[str, float]]  # {"x": 0.5, "y": 0.5}
    success: bool
    error: Optional[str] = None
    duration_ms: float = 0
    screenshot_path: Optional[str] = None
    screen_analysis: Optional[Dict] = None
    timestamp: float = field(default_factory=time.time)

    def to_dict(self) -> Dict:
        """Convert to dictionary."""
        return {
            "step_number": self.step_number,
            "action": self.action,
            "target": self.target,
            "coordinate": self.coordinate,
            "success": self.success,
            "error": self.error,
            "duration_ms": self.duration_ms,
            "screenshot_path": self.screenshot_path,
            "screen_analysis": self.screen_analysis,
            "timestamp": self.timestamp,
            "timestamp_iso": datetime.fromtimestamp(self.timestamp).isoformat(),
        }


@dataclass
class TraversalResult:
    """Complete traversal result with structured output."""

    session_id: str
    trace_id: str
    status: ResultStatus
    start_time: float
    end_time: float

    # Input parameters
    instruction: str
    entry_app: Optional[str]
    max_steps: int

    # Results
    steps: List[StepResult] = field(default_factory=list)
    visited_items: List[Dict] = field(default_factory=list)
    skipped_items: List[Dict] = field(default_factory=list)
    failed_items: List[Dict] = field(default_factory=list)

    # Metrics
    screens_analyzed: int = 0
    total_duration_ms: float = 0
    ai_calls: Dict[str, int] = field(default_factory=dict)

    # Final state
    final_path: List[str] = field(default_factory=list)
    completion_reason: Optional[str] = None

    # Error information
    error: Optional[str] = None
    error_trace: Optional[str] = None

    def to_dict(self) -> Dict:
        """Convert to dictionary."""
        return {
            "session_id": self.session_id,
            "trace_id": self.trace_id,
            "status": self.status.value,
            "start_time": self.start_time,
            "end_time": self.end_time,
            "start_time_iso": datetime.fromtimestamp(self.start_time).isoformat(),
            "end_time_iso": datetime.fromtimestamp(self.end_time).isoformat(),
            "duration_ms": self.total_duration_ms,
            "instruction": self.instruction,
            "entry_app": self.entry_app,
            "max_steps": self.max_steps,
            "steps": [step.to_dict() for step in self.steps],
            "visited_items": self.visited_items,
            "skipped_items": self.skipped_items,
            "failed_items": self.failed_items,
            "screens_analyzed": self.screens_analyzed,
            "ai_calls": self.ai_calls,
            "final_path": self.final_path,
            "completion_reason": self.completion_reason,
            "error": self.error,
            "error_trace": self.error_trace,
        }

    def to_summary(self) -> str:
        """Generate human-readable summary."""
        lines = [
            "=" * 60,
            f"Traversal Result - {self.session_id}",
            "=" * 60,
            f"Status: {self.status.value.upper()}",
            f"Instruction: {self.instruction}",
            f"Duration: {(self.end_time - self.start_time):.1f}s",
            "",
            f"Steps: {len(self.steps)} (max: {self.max_steps})",
            f"Visited: {len(self.visited_items)} items",
            f"Skipped: {len(self.skipped_items)} items",
            f"Failed: {len(self.failed_items)} items",
            f"Screens analyzed: {self.screens_analyzed}",
            "",
        ]

        if self.visited_items:
            lines.append("✅ Visited Items:")
            for item in self.visited_items[:20]:  # Show first 20
                path_str = " > ".join(item.get("path", []))
                lines.append(f"  - {item.get('name')} [{path_str}]")
            if len(self.visited_items) > 20:
                lines.append(f"  ... and {len(self.visited_items) - 20} more")

        if self.skipped_items:
            lines.append("")
            lines.append("⏭️  Skipped Items:")
            for item in self.skipped_items[:10]:
                lines.append(f"  - {item.get('name')} - {item.get('reason', 'unknown')}")

        if self.failed_items:
            lines.append("")
            lines.append("❌ Failed Items:")
            for item in self.failed_items[:10]:
                lines.append(f"  - {item.get('name')} - {item.get('error', 'unknown')}")

        if self.error:
            lines.append("")
            lines.append(f"❌ Error: {self.error}")

        lines.append("=" * 60)
        return "\n".join(lines)


class ResultManager:
    """Manager for storing and retrieving traversal results."""

    def __init__(self, results_dir: Path = Path(".results")):
        """Initialize result manager.

        Args:
            results_dir: Directory to store results
        """
        self.results_dir = results_dir
        self.results_dir.mkdir(exist_ok=True)

        # Subdirectories
        self.sessions_dir = self.results_dir / "sessions"
        self.sessions_dir.mkdir(exist_ok=True)

        self.reports_dir = self.results_dir / "reports"
        self.reports_dir.mkdir(exist_ok=True)

        self.screenshots_dir = self.results_dir / "screenshots"
        self.screenshots_dir.mkdir(exist_ok=True)

    def save_result(self, result: TraversalResult) -> Path:
        """Save traversal result to file.

        Args:
            result: TraversalResult to save

        Returns:
            Path to saved file
        """
        timestamp = datetime.fromtimestamp(result.start_time).strftime("%Y%m%d_%H%M%S")
        filename = f"{result.session_id}_{timestamp}.json"
        filepath = self.sessions_dir / filename

        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(result.to_dict(), f, indent=2, ensure_ascii=False)

        logger.info(f"Result saved to {filepath}")
        return filepath

    def load_result(self, session_id: str) -> Optional[TraversalResult]:
        """Load traversal result by session ID.

        Args:
            session_id: Session identifier

        Returns:
            TraversalResult if found, None otherwise
        """
        # Find the most recent file for this session
        matching_files = list(self.sessions_dir.glob(f"{session_id}_*.json"))
        if not matching_files:
            return None

        # Sort by modification time, get most recent
        filepath = max(matching_files, key=lambda p: p.stat().st_mtime)

        with open(filepath, "r", encoding="utf-8") as f:
            data = json.load(f)

        return self._dict_to_result(data)

    def get_all_results(self, limit: int = 50) -> List[Dict]:
        """Get all traversal results.

        Args:
            limit: Maximum number of results to return

        Returns:
            List of result summary dictionaries
        """
        results = []

        for filepath in sorted(self.sessions_dir.glob("*.json"), reverse=True)[:limit]:
            try:
                with open(filepath, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    results.append({
                        "session_id": data.get("session_id"),
                        "trace_id": data.get("trace_id"),
                        "status": data.get("status"),
                        "start_time": data.get("start_time_iso"),
                        "duration_ms": data.get("duration_ms"),
                        "instruction": data.get("instruction"),
                        "visited_count": len(data.get("visited_items", [])),
                        "skipped_count": len(data.get("skipped_items", [])),
                        "file": str(filepath),
                    })
            except Exception as e:
                logger.warning(f"Failed to load result from {filepath}: {e}")

        return results

    def save_screenshot(self, session_id: str, step_number: int, image_data: bytes) -> Path:
        """Save screenshot to file.

        Args:
            session_id: Session identifier
            step_number: Step number
            image_data: Image bytes

        Returns:
            Path to saved screenshot
        """
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = f"{session_id}_step{step_number}_{timestamp}.png"
        filepath = self.screenshots_dir / filename

        with open(filepath, "wb") as f:
            f.write(image_data)

        logger.debug(f"Screenshot saved to {filepath}")
        return filepath

    def generate_report(self, result: TraversalResult, format: str = "html") -> Path:
        """Generate traversal report.

        Args:
            result: TraversalResult
            format: Report format (html, markdown, json)

        Returns:
            Path to generated report
        """
        timestamp = datetime.fromtimestamp(result.start_time).strftime("%Y%m%d_%H%M%S")
        filename = f"{result.session_id}_{timestamp}_report.{format}"
        filepath = self.reports_dir / filename

        if format == "html":
            content = self._generate_html_report(result)
        elif format == "markdown":
            content = self._generate_markdown_report(result)
        elif format == "json":
            content = json.dumps(result.to_dict(), indent=2, ensure_ascii=False)
        else:
            raise ValueError(f"Unknown format: {format}")

        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)

        logger.info(f"Report saved to {filepath}")
        return filepath

    def _generate_html_report(self, result: TraversalResult) -> str:
        """Generate HTML report."""
        visited_html = "\n".join([
            f"<li>{item.get('name')} <small>{' > '.join(item.get('path', []))}</small></li>"
            for item in result.visited_items
        ])

        skipped_html = "\n".join([
            f"<li>{item.get('name')} <small>{item.get('reason', 'unknown')}</small></li>"
            for item in result.skipped_items
        ])

        return f"""<!DOCTYPE html>
<html>
<head>
    <title>Traversal Report - {result.session_id}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; background: #f5f5f5; }}
        .container {{ max-width: 1000px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; }}
        h1 {{ color: #333; }}
        .summary {{ background: #f0f0f0; padding: 20px; border-radius: 4px; margin: 20px 0; }}
        .summary-item {{ display: inline-block; margin: 10px 20px; }}
        .summary-label {{ font-weight: bold; color: #666; }}
        .summary-value {{ font-size: 24px; color: #333; }}
        h2 {{ color: #e94560; margin-top: 30px; }}
        ul {{ list-style: none; padding: 0; }}
        li {{ padding: 10px; border-bottom: 1px solid #eee; }}
        small {{ color: #999; }}
        .status-success {{ color: #2ecc71; }}
        .status-partial {{ color: #f39c12; }}
        .status-failed {{ color: #e74c3c; }}
    </style>
</head>
<body>
    <div class="container">
        <h1>Traversal Report</h1>
        <p><strong>Session ID:</strong> {result.session_id}</p>
        <p><strong>Trace ID:</strong> {result.trace_id}</p>
        <p><strong>Instruction:</strong> {result.instruction}</p>

        <div class="summary">
            <div class="summary-item">
                <div class="summary-label">Status</div>
                <div class="summary-value status-{result.status.value}">{result.status.value.upper()}</div>
            </div>
            <div class="summary-item">
                <div class="summary-label">Duration</div>
                <div class="summary-value">{result.total_duration_ms/1000:.1f}s</div>
            </div>
            <div class="summary-item">
                <div class="summary-label">Steps</div>
                <div class="summary-value">{len(result.steps)}/{result.max_steps}</div>
            </div>
            <div class="summary-item">
                <div class="summary-label">Visited</div>
                <div class="summary-value">{len(result.visited_items)}</div>
            </div>
        </div>

        <h2>✅ Visited Items ({len(result.visited_items)})</h2>
        <ul>{visited_html}</ul>

        <h2>⏭️ Skipped Items ({len(result.skipped_items)})</h2>
        <ul>{skipped_html}</ul>

        <h2>📊 Details</h2>
        <p>Screens analyzed: {result.screens_analyzed}</p>
        <p>Final path: {' > '.join(result.final_path)}</p>

        {f'<p><strong>Error:</strong> {result.error}</p>' if result.error else ''}
    </div>
</body>
</html>"""

    def _generate_markdown_report(self, result: TraversalResult) -> str:
        """Generate Markdown report."""
        return f"""# Traversal Report

**Session ID:** {result.session_id}
**Trace ID:** {result.trace_id}
**Status:** {result.status.value.upper()}

## Summary

- **Instruction:** {result.instruction}
- **Entry App:** {result.entry_app or 'Current'}
- **Duration:** {result.total_duration_ms/1000:.1f}s
- **Steps:** {len(result.steps)}/{result.max_steps}
- **Visited:** {len(result.visited_items)} items
- **Skipped:** {len(result.skipped_items)} items
- **Screens:** {result.screens_analyzed} analyzed

## Visited Items

{''.join([f"- {item.get('name')} ({' > '.join(item.get('path', []))})" for item in result.visited_items])}

## Skipped Items

{''.join([f"- {item.get('name')} ({item.get('reason', 'unknown')})" for item in result.skipped_items])}

## Details

- Final path: {' > '.join(result.final_path)}
- Completion reason: {result.completion_reason or 'N/A'}

{f'## Error\n\n{result.error}' if result.error else ''}
"""

    @staticmethod
    def _dict_to_result(data: Dict) -> TraversalResult:
        """Convert dictionary to TraversalResult."""
        steps = [
            StepResult(**step) for step in data.get("steps", [])
        ]

        return TraversalResult(
            session_id=data["session_id"],
            trace_id=data["trace_id"],
            status=ResultStatus(data["status"]),
            start_time=data["start_time"],
            end_time=data["end_time"],
            instruction=data["instruction"],
            entry_app=data.get("entry_app"),
            max_steps=data["max_steps"],
            steps=steps,
            visited_items=data.get("visited_items", []),
            skipped_items=data.get("skipped_items", []),
            failed_items=data.get("failed_items", []),
            screens_analyzed=data.get("screens_analyzed", 0),
            total_duration_ms=data.get("duration_ms", 0),
            ai_calls=data.get("ai_calls", {}),
            final_path=data.get("final_path", []),
            completion_reason=data.get("completion_reason"),
            error=data.get("error"),
            error_trace=data.get("error_trace"),
        )


# Global result manager instance
_result_manager: Optional[ResultManager] = None


def get_result_manager() -> ResultManager:
    """Get global result manager instance."""
    global _result_manager
    if _result_manager is None:
        _result_manager = ResultManager()
    return _result_manager


__all__ = [
    "ResultStatus",
    "StepResult",
    "TraversalResult",
    "ResultManager",
    "get_result_manager",
]
