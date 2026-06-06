"""
Unit tests for trace storage (tasks 10.7-10.9).

Tests:
- 10.7 FileStorage write and read operations
- 10.8 FileStorage queue buffering
- 10.9 MemoryStorage write and read operations
"""

import tempfile
import time
from pathlib import Path

from src.trace.models import SessionNode, SpanNode, StepNode
from src.trace.storage import FileStorage, MemoryStorage


class TestMemoryStorage:
    """10.9: MemoryStorage write and read operations."""

    def test_write_and_read(self):
        ms = MemoryStorage()
        sess = SessionNode()
        ms.write(sess)
        nodes = ms.read(sess.trace_id)
        assert len(nodes) == 1
        assert nodes[0].span_id == sess.span_id

    def test_multi_trace_isolation(self):
        ms = MemoryStorage()
        s1 = SessionNode()
        s2 = SessionNode()
        ms.write(s1)
        ms.write(s2)
        assert len(ms.read(s1.trace_id)) == 1
        assert len(ms.read(s2.trace_id)) == 1
        # They should be different traces
        assert s1.trace_id != s2.trace_id

    def test_multiple_nodes_same_trace(self):
        ms = MemoryStorage()
        tid = "trace-x"
        s = SessionNode(trace_id=tid, span_id=tid)
        step = StepNode(trace_id=tid)
        span = SpanNode(trace_id=tid)
        ms.write(s)
        ms.write(step)
        ms.write(span)
        nodes = ms.read(tid)
        assert len(nodes) == 3

    def test_read_nonexistent_trace(self):
        ms = MemoryStorage()
        assert ms.read("no-such-trace") == []

    def test_clear(self):
        ms = MemoryStorage()
        s = SessionNode()
        ms.write(s)
        assert len(ms.read(s.trace_id)) == 1
        ms.clear(s.trace_id)
        assert len(ms.read(s.trace_id)) == 0

    def test_trace_ids(self):
        ms = MemoryStorage()
        s1 = SessionNode()
        s2 = SessionNode()
        ms.write(s1)
        ms.write(s2)
        ids = ms.trace_ids()
        assert len(ids) == 2
        assert s1.trace_id in ids
        assert s2.trace_id in ids


class TestFileStorage:
    """10.7-10.8: FileStorage write/read and queue buffering."""

    def setup_method(self):
        self._tmp = tempfile.mkdtemp()
        self._fs = FileStorage(base_dir=self._tmp)

    def teardown_method(self):
        import shutil
        self._fs.flush(timeout=5.0)
        shutil.rmtree(self._tmp, ignore_errors=True)

    def test_write_and_read(self):
        sess = SessionNode()
        self._fs.write(sess)
        self._fs.flush(timeout=5.0)
        nodes = self._fs.read(sess.trace_id)
        assert len(nodes) >= 1
        assert nodes[0].span_id == sess.span_id

    def test_write_session_json(self):
        sess = SessionNode(device_model="Pixel 7")
        self._fs.write(sess)
        self._fs.write_session(sess.to_dict(), sess.trace_id)
        self._fs.flush(timeout=5.0)
        sd = self._fs.read_session(sess.trace_id)
        assert sd is not None
        assert sd["device_model"] == "Pixel 7"

    def test_trace_directory_structure(self):
        sess = SessionNode()
        self._fs.write(sess)
        self._fs.flush(timeout=5.0)
        trace_dir = Path(self._tmp) / sess.trace_id
        assert trace_dir.exists()
        assert (trace_dir / "trace.jsonl").exists()

    def test_screenshot_index(self):
        sess = SessionNode()
        self._fs.write(sess)
        self._fs.write_screenshot_index({"s_1": "home_001.png"}, sess.trace_id)
        idx = self._fs.read_screenshot_index(sess.trace_id)
        assert idx == {"s_1": "home_001.png"}

    def test_read_nonexistent_trace(self):
        assert self._fs.read("no-such-trace") == []

    def test_read_nonexistent_session(self):
        assert self._fs.read_session("no-such") is None

    def test_queue_buffering_is_non_blocking(self):
        """10.8: Write should return immediately (queue buffered)."""
        sess = SessionNode()
        t0 = time.time()
        for _ in range(100):
            self._fs.write(sess)
        elapsed = time.time() - t0
        # 100 writes should complete quickly (queue, not disk)
        assert elapsed < 5.0

    def test_queue_drains_on_flush(self):
        sess = SessionNode()
        self._fs.write(sess)
        self._fs.flush(timeout=5.0)
        nodes = self._fs.read(sess.trace_id)
        assert len(nodes) >= 1
