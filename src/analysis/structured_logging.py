"""Structured logging system for traversal operations."""

import json
import logging
import sys
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional
import traceback


class StructuredLogger:
    """Structured logger with JSON output and file writing."""

    def __init__(self, name: str, log_dir: Path = Path(".logs")):
        """Initialize structured logger.

        Args:
            name: Logger name
            log_dir: Directory to store log files
        """
        self.name = name
        self.log_dir = log_dir
        self.log_dir.mkdir(exist_ok=True)

        # Create file handler
        log_file = self.log_dir / f"{name}_{datetime.now().strftime('%Y%m%d')}.log"
        self.file_handler = logging.FileHandler(log_file, encoding="utf-8")

        # Create console handler with colors
        self.console_handler = logging.StreamHandler(sys.stdout)

        # Configure formatting
        self.file_handler.setFormatter(
            logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
        )

    def log_event(self, level: str, event: str, **data):
        """Log a structured event.

        Args:
            level: Log level (debug, info, warning, error)
            event: Event name
            **data: Event data
        """
        log_entry = {
            "timestamp": datetime.now().isoformat(),
            "level": level.upper(),
            "logger": self.name,
            "event": event,
            "data": data
        }

        # Log as JSON for file
        self.file_handler.handle(
            logging.LogRecord(
                name=self.name,
                level=getattr(logging, level.upper()),
                pathname="",
                lineno=0,
                msg=json.dumps(log_entry),
                args=(),
                exc_info=None,
            )
        )

        # Log formatted message to console
        msg = f"[{event}] " + " ".join([f"{k}={v}" for k, v in data.items()])
        getattr(logging.getLogger(level.upper()), level.lower())(msg)


class TraversalLogger(StructuredLogger):
    """Specialized logger for traversal operations."""

    def __init__(self, session_id: str, log_dir: Path = Path(".logs")):
        """Initialize traversal logger.

        Args:
            session_id: Traversal session ID
            log_dir: Directory to store log files
        """
        super().__init__(f"traversal_{session_id}", log_dir)
        self.session_id = session_id
        self.step_count = 0
        self.log_file = self.log_dir / f"traversal_{session_id}.jsonl"

    def log_session_start(self, instruction: str, max_steps: int, entry_app: Optional[str]):
        """Log session start.

        Args:
            instruction: User instruction
            max_steps: Maximum steps
            entry_app: Entry application
        """
        self.log_event("info", "session_start",
            session_id=self.session_id,
            instruction=instruction,
            max_steps=max_steps,
            entry_app=entry_app,
        )

        # Also write to JSONL file
        self._write_jsonl({
            "type": "session_start",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "instruction": instruction,
            "max_steps": max_steps,
            "entry_app": entry_app,
        })

    def log_session_end(self, status: str, steps: int, visited: int, duration_ms: float):
        """Log session end.

        Args:
            status: Final status
            steps: Total steps taken
            visited: Total items visited
            duration_ms: Total duration
        """
        self.log_event("info", "session_end",
            session_id=self.session_id,
            status=status,
            steps=steps,
            visited=visited,
            duration_ms=duration_ms,
        )

        self._write_jsonl({
            "type": "session_end",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "status": status,
            "steps": steps,
            "visited": visited,
            "duration_ms": duration_ms,
        })

    def log_step(self, action: str, target: Optional[str], coordinate: Optional[Dict], success: bool, duration_ms: float):
        """Log a traversal step.

        Args:
            action: Action performed (tap, back, etc.)
            target: Target element name
            coordinate: Click coordinates
            success: Whether step succeeded
            duration_ms: Step duration
        """
        self.step_count += 1

        self.log_event("info", "step",
            step_number=self.step_count,
            action=action,
            target=target,
            coordinate=coordinate,
            success=success,
            duration_ms=duration_ms,
        )

        self._write_jsonl({
            "type": "step",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "step_number": self.step_count,
            "action": action,
            "target": target,
            "coordinate": coordinate,
            "success": success,
            "duration_ms": duration_ms,
        })

    def log_screen_analysis(self, items_count: int, path: List[str], duration_ms: float):
        """Log screen analysis.

        Args:
            items_count: Number of items detected
            path: Current path
            duration_ms: Analysis duration
        """
        self.log_event("debug", "screen_analysis",
            items_count=items_count,
            path=path,
            duration_ms=duration_ms,
        )

        self._write_jsonl({
            "type": "screen_analysis",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "items_count": items_count,
            "path": path,
            "duration_ms": duration_ms,
        })

    def log_ai_call(self, service: str, operation: str, duration_ms: float, success: bool, confidence: Optional[float]):
        """Log AI service call.

        Args:
            service: Service name
            operation: Operation name
            duration_ms: Call duration
            success: Whether call succeeded
            confidence: Optional confidence score
        """
        self.log_event("debug", "ai_call",
            service=service,
            operation=operation,
            duration_ms=duration_ms,
            success=success,
            confidence=confidence,
        )

        self._write_jsonl({
            "type": "ai_call",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "service": service,
            "operation": operation,
            "duration_ms": duration_ms,
            "success": success,
            "confidence": confidence,
        })

    def log_error(self, error: Exception, context: Dict):
        """Log error with context.

        Args:
            error: Exception object
            context: Error context
        """
        self.log_event("error", "error",
            error_type=type(error).__name__,
            error_message=str(error),
            error_trace=traceback.format_exc(),
            context=context,
        )

        self._write_jsonl({
            "type": "error",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "error_type": type(error).__name__,
            "error_message": str(error),
            "error_trace": traceback.format_exc(),
            "context": context,
        })

    def log_visited_item(self, item_name: str, item_type: str, path: List[str], coordinate: Dict):
        """Log visited item.

        Args:
            item_name: Item name
            item_type: Item type
            path: Current path
            coordinate: Click coordinate
        """
        self.log_event("info", "visited_item",
            name=item_name,
            type=item_type,
            path=path,
            coordinate=coordinate,
        )

        self._write_jsonl({
            "type": "visited_item",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "name": item_name,
            "type": item_type,
            "path": path,
            "coordinate": coordinate,
        })

    def log_skipped_item(self, item_name: str, reason: str):
        """Log skipped item.

        Args:
            item_name: Item name
            reason: Skip reason
        """
        self.log_event("info", "skipped_item",
            name=item_name,
            reason=reason,
        )

        self._write_jsonl({
            "type": "skipped_item",
            "timestamp": datetime.now().isoformat(),
            "session_id": self.session_id,
            "name": item_name,
            "reason": reason,
        })

    def _write_jsonl(self, data: Dict):
        """Write data to JSONL file.

        Args:
            data: Data to write
        """
        try:
            with open(self.log_file, "a", encoding="utf-8") as f:
                f.write(json.dumps(data, ensure_ascii=False) + "\n")
        except Exception as e:
            logging.warning(f"Failed to write JSONL: {e}")


class LoggerFactory:
    """Factory for creating loggers."""

    _loggers: Dict[str, TraversalLogger] = {}

    @classmethod
    def get_logger(cls, session_id: str, log_dir: Path = Path(".logs")) -> TraversalLogger:
        """Get or create a logger for a session.

        Args:
            session_id: Session ID
            log_dir: Log directory

        Returns:
            TraversalLogger instance
        """
        if session_id not in cls._loggers:
            cls._loggers[session_id] = TraversalLogger(session_id, log_dir)
        return cls._loggers[session_id]

    @classmethod
    def remove_logger(cls, session_id: str):
        """Remove a logger from cache.

        Args:
            session_id: Session ID
        """
        if session_id in cls._loggers:
            del cls._loggers[session_id]


__all__ = [
    "StructuredLogger",
    "TraversalLogger",
    "LoggerFactory",
]
