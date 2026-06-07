"""
V6.6: Trace handler metrics enhancement tests.

Tests:
- _record_metrics_as_spans (3 span types + empty)
- _build_ai_call_metrics (with/without PageAnalysis, last_call_metrics)
- VisionService.last_call_metrics (default + override)
- SpanNode page_id / element_count serialization
- MockVisionService elements fix
"""

from src.state_machine.traversal_fsm import TraversalStateMachine, TraversalState
from src.simulation.mock_vision import MockVisionService
from src.state.content_tree import PageAnalysis, Direction
from src.trace.models import SpanNode, SessionNode, generate_id
from src.trace.recorder import TraceRecorder
from src.trace.storage import MemoryStorage
from src.vision.vision_service import VisionService


class TestRecordMetricsAsSpans:
    """_record_metrics_as_spans via engine's span generation methods."""

    def test_ai_call_span_generated(self):
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)
        step = SpanNode(span_type="state_transition")
        rec.record_span(step)

        span = SpanNode(
            span_type="ai_call", capability="vision", success=True,
            latency_ms=350.0, provider_id="claude",
            input_tokens=1200, output_tokens=80,
            page_id="home/settings", element_count=5,
        )
        rec.record_span(span)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        ai = [n for n in nodes if hasattr(n, 'span_type') and n.span_type == "ai_call"]
        assert len(ai) == 1
        assert ai[0].capability == "vision"
        assert ai[0].page_id == "home/settings"
        assert ai[0].element_count == 5

    def test_execution_span_generated(self):
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        span = SpanNode(
            span_type="execution", action="click", status="success",
            target="btn_wifi", duration_ms=150.0,
        )
        rec.record_span(span)
        rec.finalize("completed")

        nodes = ms.read(sess.trace_id)
        ex = [n for n in nodes if hasattr(n, 'span_type') and n.span_type == "execution"]
        assert len(ex) == 1
        assert ex[0].action == "click"

    def test_error_span_generated(self):
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        sess = SessionNode()
        rec.init(sess)

        span = SpanNode(
            span_type="error", error_type="TimeoutError",
            error_message="timed out", severity="critical",
        )
        rec.record_span(span)
        rec.finalize("error")

        nodes = ms.read(sess.trace_id)
        err = [n for n in nodes if hasattr(n, 'span_type') and n.span_type == "error"]
        assert len(err) == 1
        assert err[0].error_type == "TimeoutError"

    def test_empty_metrics_noop(self):
        """_record_metrics_as_spans({}) should not raise."""
        ms = MemoryStorage()
        rec = TraceRecorder(storage=ms)
        rec.init(SessionNode())
        # Should not raise when recording nothing
        rec.finalize("completed")
        assert len(ms.read(rec.trace_id)) >= 1


class TestBuildAICallMetrics:
    """_build_ai_call_metrics helper."""

    def test_with_page_analysis(self):
        page = PageAnalysis(
            level1_dir=Direction.RIGHT, level1_menus=[],
            level2_dir=Direction.BOTTOM, level2_menus=[],
            current_path=["home", "settings"], items=[],
        )
        result = TraversalStateMachine._build_ai_call_metrics(page, 200.0, None)
        assert result["success"] is True
        assert result["latency_ms"] == 200.0
        assert result["page_id"] == "home/settings"
        assert result["element_count"] == 0

    def test_with_items(self):
        from src.state.content_tree import MenuItem, Coordinate
        page = PageAnalysis(
            level1_dir=Direction.RIGHT, level1_menus=[],
            level2_dir=Direction.BOTTOM, level2_menus=[],
            current_path=["home"],
            items=[
                MenuItem(name="WiFi", type="menu_item", coordinate=Coordinate(x=0.5, y=0.3)),
                MenuItem(name="BT", type="menu_item", coordinate=Coordinate(x=0.5, y=0.5)),
            ],
        )
        result = TraversalStateMachine._build_ai_call_metrics(page, 100.0, None)
        assert result["element_count"] == 2

    def test_with_none_page(self):
        result = TraversalStateMachine._build_ai_call_metrics(None, 50.0, None)
        assert result["success"] is False
        assert result.get("page_id") is None
        assert result.get("element_count") is None

    def test_with_vision_metrics(self):
        page = PageAnalysis(
            level1_dir=Direction.RIGHT, level1_menus=[],
            level2_dir=Direction.BOTTOM, level2_menus=[],
            current_path=["home"], items=[],
        )

        class MockVisionWithMetrics(VisionService):
            def analyze_screenshot(self, image_data):
                return page
            def find_app_entry(self, image_data, target):
                return None
            @property
            def last_call_metrics(self):
                return {"provider_id": "deepseek", "input_tokens": 500, "output_tokens": 60}

        vision = MockVisionWithMetrics()
        result = TraversalStateMachine._build_ai_call_metrics(page, 300.0, vision)
        assert result["provider_id"] == "deepseek"
        assert result["input_tokens"] == 500
        assert result["output_tokens"] == 60


class TestVisionServiceLastCallMetrics:
    """VisionService.last_call_metrics property."""

    def test_default_returns_none(self):
        mock = MockVisionService({})
        assert mock.last_call_metrics is None

    def test_subclass_can_override(self):
        class CustomVision(VisionService):
            def analyze_screenshot(self, image_data):
                return None
            def find_app_entry(self, image_data, target):
                return None
            @property
            def last_call_metrics(self):
                return {"provider_id": "test", "input_tokens": 100}

        v = CustomVision()
        assert v.last_call_metrics == {"provider_id": "test", "input_tokens": 100}


class TestSpanNodeNewFields:
    """SpanNode page_id / element_count serialization."""

    def test_serialize_ai_call(self):
        s = SpanNode(span_type="ai_call", page_id="home/settings", element_count=5)
        d = s.to_dict()
        assert d["page_id"] == "home/settings"
        assert d["element_count"] == 5

    def test_deserialize_ai_call(self):
        data = {"span_type": "ai_call", "page_id": "home", "element_count": 3}
        s = SpanNode.from_dict(data)
        assert s.page_id == "home"
        assert s.element_count == 3

    def test_ai_call_without_new_fields(self):
        """Old trace without page_id/element_count should still parse."""
        data = {"span_type": "ai_call", "capability": "vision"}
        s = SpanNode.from_dict(data)
        assert s.page_id is None
        assert s.element_count is None

    def test_execution_does_not_serialize_new_fields(self):
        s = SpanNode(span_type="execution", action="click")
        d = s.to_dict()
        assert "page_id" not in d
        assert "element_count" not in d


class TestMockVisionElementsFix:
    """MockVisionService correctly reads elements from PageAnalyzer output."""

    def test_elements_parsed(self):
        mock = MockVisionService({
            "home": {
                "elements": [
                    {"text": "Settings", "element_type": "menu_item",
                     "bounds": {"x": 0.5, "y": 0.3},
                     "action_hint": "view", "metadata": {}},
                ],
                "page_type": "menu",
                "page_path": "home",
                "metadata": {},
            }
        })
        mock.set_path_context(["home"])
        result = mock.analyze_screenshot(b"")
        assert len(result.items) == 1
        assert result.items[0].name == "Settings"

    def test_empty_elements(self):
        mock = MockVisionService({
            "home": {"elements": [], "page_type": "menu", "page_path": "home", "metadata": {}}
        })
        mock.set_path_context(["home"])
        result = mock.analyze_screenshot(b"")
        assert len(result.items) == 0
