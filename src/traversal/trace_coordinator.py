"""Centralized trace span creation.

Wraps TraceRecorder with convenience methods for recording state
transitions, actions, AI calls, errors, decisions, and step boundaries.
"""

import time
from typing import Any, Dict, List, Optional

from src.trace.models import (
    SpanNode,
    StepNode,
    PageTransitionSpan,
    DynamicNodeLifecycleSpan,
    StateDecisionSpan,
)
from src.trace.recorder import TraceRecorder
from src.trace.context import TraversalRuntimeContext


class TraceCoordinator:
    """Creates and records spans for all engine events.

    All methods are no-ops when trace_recorder is None or has no trace_id.
    """

    def __init__(
        self,
        recorder: Optional[TraceRecorder],
        plan: Any = None,
        context: Optional[TraversalRuntimeContext] = None,
    ):
        self._recorder = recorder
        self._plan = plan
        self._context = context

    @property
    def active(self) -> bool:
        return self._recorder is not None and self._recorder.trace_id is not None

    # -- trace level helpers ---------------------------------------------------

    def _get_trace_level(self) -> str:
        if self._plan and self._plan.entry_config:
            return self._plan.entry_config.trace_level
        if self._plan:
            return self._plan.meta.get("trace_level", "standard")
        return "standard"

    def should_record_entry_attempt(self) -> bool:
        return self._get_trace_level() in ("standard", "detailed")

    def should_record_vision_call(self) -> bool:
        return self._get_trace_level() == "detailed"

    # -- state transitions -----------------------------------------------------

    def record_state_transition(self, from_state: str, to_state: str) -> None:
        if not self.active:
            return
        span = SpanNode(
            span_type="state_transition",
            from_state=from_state,
            to_state=to_state,
            state_machine="traversal_fsm",
        )
        self._recorder.record_span(span)

    def record_root_node_pushed(self, node_id: str) -> None:
        if not self.active:
            return
        span = SpanNode(
            span_type="state_transition",
            from_state="INITIALIZING",
            to_state="TRAVERSING",
            state_machine="graph_engine",
        )
        self._recorder.record_span(span)

    # -- page analysis ---------------------------------------------------------

    def record_page_analysis(self, page_analysis: Any) -> None:
        if not self.active:
            return
        elements = []
        try:
            if hasattr(page_analysis, "items"):
                for item in page_analysis.items:
                    coord_x, coord_y = 0.5, 0.5
                    if hasattr(item, "coordinate") and item.coordinate:
                        if hasattr(item.coordinate, "x"):
                            coord_x = item.coordinate.x
                        elif isinstance(item.coordinate, dict):
                            coord_x = item.coordinate.get("x", 0.5)
                    elements.append({
                        "name": item.name if hasattr(item, "name") else str(item),
                        "type": item.type.value if hasattr(item.type, "value") else str(item.type),
                        "coordinate": {"x": coord_x, "y": coord_y},
                        "expected_action": item.expected_action.value if hasattr(item, "expected_action") and hasattr(item.expected_action, "value") else "",
                    })
        except Exception:
            elements = []

        current_path = list(self._context.current_path) if self._context and self._context.current_path else []
        page_id = current_path[-1] if current_path else "unknown"
        span = SpanNode(
            span_type="page_snapshot",
            metadata={
                "page_id": page_id,
                "page_path": current_path,
                "timestamp": time.time(),
                "element_count": len(elements),
                "elements": elements,
            },
        )
        self._recorder.record_span(span)

    # -- action execution ------------------------------------------------------

    def record_action_execution(
        self, action: str, target: Any, success: bool,
        page_context: Optional[Dict[str, Any]] = None,
    ) -> None:
        if not self.active:
            return
        element_id = None
        if target:
            if isinstance(target, str):
                element_id = target
            elif isinstance(target, dict):
                element_id = target.get("element_id") or target.get("value")
            elif hasattr(target, "id"):
                element_id = getattr(target, "id", None)

        page_id = None
        if page_context:
            page_id = page_context.get("page_id")
        elif self._context and self._context.current_path:
            page_id = self._context.current_path[-1] if self._context.current_path else None

        span = SpanNode(
            span_type="execution",
            action=action,
            target=element_id or str(target) if target else None,
            status="success" if success else "failed",
            metadata={
                "page_id": page_id,
                "page_context": page_context or {},
            },
        )
        self._recorder.record_span(span)

    # -- metrics ---------------------------------------------------------------

    def record_metrics_as_spans(self, metrics: Optional[Dict[str, Any]]) -> None:
        if not self.active or not metrics:
            return
        ai_calls = metrics.get("ai_call")
        if ai_calls:
            items = ai_calls if isinstance(ai_calls, list) else [ai_calls]
            for ai in items:
                if ai:
                    self.record_ai_call_span(ai)

        executions = metrics.get("execution")
        if executions:
            items = executions if isinstance(executions, list) else [executions]
            for ex in items:
                if ex:
                    self.record_execution_span(ex)

        error = metrics.get("error")
        if error:
            self.record_error_span(
                error.get("error_type", "UnknownError"),
                error.get("error_message", ""),
                error.get("severity", "error"),
            )

    # -- individual span types -------------------------------------------------

    def record_skip_span(self, match_result) -> None:
        if not self.active:
            return
        from src.graph.matcher import MatchAction
        if not match_result.matched:
            reason = "no_match"
        elif match_result.action != MatchAction.GENERATE_CHILD:
            reason = f"action_{match_result.action.value}"
        else:
            reason = "unknown"
        item_info = {}
        if match_result.menu_item:
            item_info = {
                "type": match_result.menu_item.get("type"),
                "text": match_result.menu_item.get("text"),
                "index": match_result.menu_item.get("index"),
            }
        span = SpanNode(
            span_type="dynamic_matching",
            action="skip_element",
            target=item_info.get("text"),
            metadata={"reason": reason, "element": item_info},
        )
        self._recorder.record_span(span)

    def record_execution_span(self, ex: Dict[str, Any]) -> None:
        if not self.active:
            return
        span = SpanNode(
            span_type="execution",
            action=ex.get("action", "unknown"),
            status=ex.get("status", "success"),
            target=ex.get("target"),
            duration_ms=ex.get("duration_ms"),
        )
        self._recorder.record_span(span)

    def record_ai_call_span(self, ai: Dict[str, Any]) -> None:
        if not self.active:
            return
        span = SpanNode(
            span_type="ai_call",
            capability=ai.get("capability", "vision"),
            provider_id=ai.get("provider_id"),
            success=ai.get("success", True),
            latency_ms=ai.get("latency_ms", 0),
            input_tokens=ai.get("input_tokens"),
            output_tokens=ai.get("output_tokens"),
            page_id=ai.get("page_id"),
            element_count=ai.get("element_count"),
        )
        self._recorder.record_span(span)

    def record_error_span(
        self, error_type: str, error_message: str,
        severity: str = "error", stack_trace: Optional[str] = None,
    ) -> None:
        if not self.active:
            return
        span = SpanNode(
            span_type="error",
            error_type=error_type,
            error_message=error_message,
            severity=severity,
            stack_trace=stack_trace,
        )
        self._recorder.record_span(span)

    # -- decisions & lifecycle -------------------------------------------------

    def record_decision(self, decision: str, ctx: Dict[str, Any]) -> None:
        if not self.active:
            return
        span = SpanNode(
            span_type="decision",
            action=decision,
            metadata={
                "stack_depth": self._context.get_current_depth() if self._context else 0,
                "current_path": list(self._context.current_path) if self._context else [],
                **ctx,
            },
        )
        self._recorder.record_span(span)

    def record_page_transition(
        self, from_path: List[str], to_path: List[str],
        transition: Any = None,
    ) -> None:
        if not self.active:
            return
        if not from_path or not to_path or from_path == to_path:
            return

        trigger_element = None
        trigger_action = None
        if transition is not None and hasattr(transition, 'metadata'):
            trigger_element = transition.metadata.get('trigger_element')
            trigger_action = transition.metadata.get('trigger_action')

        span = PageTransitionSpan(
            from_page=from_path[-1] if from_path else None,
            to_page=to_path[-1] if to_path else None,
            trigger_element=trigger_element,
            trigger_action=trigger_action,
        )
        self._recorder.record_span(span)

    def record_dynamic_lifecycle(
        self, event: str, node_id: str, parent_id: Optional[str] = None,
        match_rule_id: Optional[str] = None, element_id: Optional[str] = None,
        **extra,
    ) -> None:
        if not self.active:
            return
        span = DynamicNodeLifecycleSpan(
            event=event,
            node_id=node_id,
            parent_id=parent_id,
            match_rule_id=match_rule_id,
            element_id=element_id,
            metadata=extra,
        )
        self._recorder.record_span(span)

    def record_state_decision(
        self, decision: str, node_id: str,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> None:
        if not self.active:
            return
        span = StateDecisionSpan(
            decision=decision,
            node_id=node_id,
            metadata=metadata or {},
        )
        self._recorder.record_span(span)

    # -- step boundaries -------------------------------------------------------

    def record_step_start(self, node_id: str, page_path: List[str]) -> None:
        if not self.active:
            return
        step_node = StepNode(
            node_id=node_id,
            step_type="NODE_SELECT",
            page_path=list(page_path),
        )
        self._recorder.record_step_start(step_node)

    def record_step_end(
        self, step_span_id: str, result: Optional[Dict[str, Any]] = None,
    ) -> None:
        if not self.active:
            return
        self._recorder.record_step_end(step_span_id, result)
