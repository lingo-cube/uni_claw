"""
Pluggable trace storage backends.

Provides:
- TraceStorage: Abstract interface for trace persistence
- FileStorage: Buffered async JSONL file writer (production)
- MemoryStorage: In-memory store (simulation / testing)
"""

import json
import os
import queue
import threading
from abc import ABC, abstractmethod
from pathlib import Path
from typing import Any, Dict, List, Optional

from .models import TraceNode


# ── Abstract storage interface ──────────────────────────────────────────────


class TraceStorage(ABC):
    """Abstract interface for pluggable trace storage backends."""

    @abstractmethod
    def write(self, node: TraceNode) -> None:
        """Write a trace node to storage."""

    @abstractmethod
    def read(self, trace_id: str) -> List[TraceNode]:
        """Read all nodes for a given trace_id."""


# ── File-based storage ──────────────────────────────────────────────────────


class FileStorage(TraceStorage):
    """Buffered file-based trace storage.

    Writes trace nodes to a JSONL file via an in-memory queue and a
    background writer thread so that the main traversal thread never
    blocks on I/O.

    Directory layout::

        traces/{trace_id}/
        ├── session.json
        ├── trace.jsonl
        └── screenshots/
            └── index.json
    """

    _MAX_QUEUE_SIZE = 10_000

    def __init__(self, base_dir: str = "traces"):
        self._base_dir = Path(base_dir)
        self._base_dir.mkdir(parents=True, exist_ok=True)

        self._queue: queue.Queue[Optional[str]] = queue.Queue(
            maxsize=self._MAX_QUEUE_SIZE
        )
        self._writer_thread: Optional[threading.Thread] = None
        self._current_trace_id: Optional[str] = None
        self._file_handle: Optional[Any] = None
        self._shutdown_requested = False

    # -- directory helpers ----------------------------------------------------

    def _trace_dir(self, trace_id: str) -> Path:
        return self._base_dir / trace_id

    def _ensure_trace_dir(self, trace_id: str) -> Path:
        d = self._trace_dir(trace_id)
        d.mkdir(parents=True, exist_ok=True)
        screenshots = d / "screenshots"
        screenshots.mkdir(exist_ok=True)
        return d

    # -- TraceStorage interface ------------------------------------------------

    def write(self, node: TraceNode) -> None:
        """Enqueue a node for writing.

        If the queue is full, block until space is available (backpressure).

        If writing fails, the error is logged and traversal continues
        ("log and continue").
        """
        if self._shutdown_requested:
            return

        # Auto-detect trace_id and start background writer on first write
        if self._current_trace_id is None:
            self._current_trace_id = node.trace_id
            trace_dir = self._ensure_trace_dir(node.trace_id)
            self._file_handle = open(trace_dir / "trace.jsonl", "a")
            self._start_writer()

        try:
            line = json.dumps(node.to_dict(), default=str) + "\n"
            self._queue.put(line, timeout=1.0)
        except queue.Full:
            import logging
            logging.getLogger(__name__).warning(
                "Trace write queue full — dropping node span_id=%s", node.span_id
            )
        except Exception as exc:
            import logging
            logging.getLogger(__name__).warning(
                "Trace write failed for span_id=%s: %s", node.span_id, exc
            )

    def read(self, trace_id: str) -> List[TraceNode]:
        """Read all nodes from a trace.jsonl file."""
        trace_file = self._trace_dir(trace_id) / "trace.jsonl"
        if not trace_file.exists():
            return []

        nodes: List[TraceNode] = []
        with open(trace_file, "r") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                data = json.loads(line)
                nodes.append(TraceNode.from_dict(data))
        return nodes

    # -- background writer ----------------------------------------------------

    def _start_writer(self) -> None:
        if self._writer_thread and self._writer_thread.is_alive():
            return
        self._writer_thread = threading.Thread(
            target=self._writer_loop, daemon=True, name="trace-writer"
        )
        self._writer_thread.start()

    def _writer_loop(self) -> None:
        """Background thread: drain the queue and write lines to disk."""
        logger = __import__("logging").getLogger(__name__)
        while not self._shutdown_requested:
            try:
                line = self._queue.get(timeout=0.5)
            except queue.Empty:
                continue

            if line is None:  # sentinel
                break

            try:
                if self._file_handle:
                    self._file_handle.write(line)
                    self._file_handle.flush()
            except Exception as exc:
                logger.warning("Trace writer flush failed: %s", exc)

            self._queue.task_done()

        # Final drain
        while not self._queue.empty():
            try:
                line = self._queue.get_nowait()
                if line and self._file_handle:
                    self._file_handle.write(line)
                    self._file_handle.flush()
                self._queue.task_done()
            except Exception:
                break

        if self._file_handle:
            self._file_handle.close()
            self._file_handle = None

    def flush(self, timeout: float = 5.0) -> None:
        """Wait for the queue to drain.

        Sends a sentinel to stop the writer thread after draining.
        """
        try:
            self._queue.put(None, timeout=timeout)
        except queue.Full:
            import logging
            logging.getLogger(__name__).warning("Could not send shutdown sentinel")
        if self._writer_thread and self._writer_thread.is_alive():
            self._writer_thread.join(timeout=timeout)
        self._shutdown_requested = True

    # -- session.json ---------------------------------------------------------

    def write_session(self, session_data: Dict[str, Any], trace_id: str) -> None:
        """Write or update session.json for a trace."""
        trace_dir = self._ensure_trace_dir(trace_id)
        session_file = trace_dir / "session.json"
        with open(session_file, "w") as f:
            json.dump(session_data, f, indent=2, default=str)

    def read_session(self, trace_id: str) -> Optional[Dict[str, Any]]:
        """Read session.json for a trace."""
        session_file = self._trace_dir(trace_id) / "session.json"
        if not session_file.exists():
            return None
        with open(session_file, "r") as f:
            return json.load(f)

    # -- screenshot index -----------------------------------------------------

    def write_screenshot_index(
        self, index: Dict[str, str], trace_id: str
    ) -> None:
        """Write the screenshot index mapping (ref_id → filename)."""
        trace_dir = self._ensure_trace_dir(trace_id)
        index_file = trace_dir / "screenshots" / "index.json"
        with open(index_file, "w") as f:
            json.dump(index, f, indent=2)

    def read_screenshot_index(self, trace_id: str) -> Dict[str, str]:
        """Read the screenshot index mapping."""
        index_file = (
            self._trace_dir(trace_id) / "screenshots" / "index.json"
        )
        if not index_file.exists():
            return {}
        with open(index_file, "r") as f:
            return json.load(f)


# ── In-memory storage (simulation) ───────────────────────────────────────────


class MemoryStorage(TraceStorage):
    """In-memory trace storage for simulation and testing.

    Nodes are stored in per-trace-id lists. No I/O is performed.
    """

    def __init__(self):
        self._store: Dict[str, List[TraceNode]] = {}

    def write(self, node: TraceNode) -> None:
        tid = node.trace_id or "__default__"
        if tid not in self._store:
            self._store[tid] = []
        self._store[tid].append(node)

    def read(self, trace_id: str) -> List[TraceNode]:
        return list(self._store.get(trace_id, []))

    def clear(self, trace_id: Optional[str] = None) -> None:
        """Clear stored traces."""
        if trace_id:
            self._store.pop(trace_id, None)
        else:
            self._store.clear()

    def trace_ids(self) -> List[str]:
        """Return all known trace IDs."""
        return list(self._store.keys())
