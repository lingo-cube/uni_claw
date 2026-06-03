"""
Trace visualization for V6 simulation.

Provides in-memory trace recording and multiple output formats.
"""

import json
import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional, Set


@dataclass
class TraceStep:
    """Single step in traversal trace."""

    step_number: int
    timestamp: datetime
    from_state: str
    to_state: str
    node_id: Optional[str] = None
    action: Optional[str] = None
    screen_info: Dict[str, Any] = field(default_factory=dict)
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary with field names matching TraceAsserter expectations."""
        # Map screen_info to target_info format expected by TraceAsserter
        target_info = {}
        if self.screen_info:
            target = self.screen_info.get("target", "")
            element_type = self.screen_info.get("element_type", "")

            # Build target_info in the format expected by TraceAsserter
            if target:
                target_info["element_id"] = target
                target_info["text"] = target
            if element_type:
                target_info["element_type"] = element_type

        # Map action to action_type
        action_type = self.action
        if not action_type or action_type == "unknown":
            # Try to infer action_type from screen_info
            if "restore" in self.screen_info:
                action_type = "click"  # Restore operations are considered clicks
            elif self.screen_info.get("element_type") in ["slider", "switch"]:
                action_type = "click"  # Toggle operations
            else:
                action_type = "click"  # Default action

        return {
            "step_number": self.step_number,
            "timestamp": self.timestamp.isoformat(),
            "from_state": self.from_state,
            "to_state": self.to_state,
            "action_type": action_type,
            "current_node": self.node_id,
            "target_info": target_info,
            "screen_info": self.screen_info,  # Keep for backward compatibility
            "metadata": self.metadata,
            "action": self.action,  # Keep for backward compatibility
            "node_id": self.node_id,  # Keep for backward compatibility
            "completion_reason": self.metadata.get("completion_reason", "") if self.metadata else "",
        }


@dataclass
class VisitedNode:
    """Node in visited tree."""

    node_id: str
    name: str
    node_type: str
    visited: bool = False
    restored: bool = False
    children: List[str] = field(default_factory=list)
    visit_time: Optional[datetime] = None
    expected_operation: Optional[str] = None  # 预期操作，如 "click: Wi-Fi"
    actual_action: Optional[str] = None  # 实际执行的操作


class InMemoryTracer:
    """
    In-memory trace recorder with visualization support.

    Records state transitions and supports multiple export formats.
    """

    def __init__(self):
        """Initialize the tracer."""
        self.steps: List[TraceStep] = []
        self.visited_tree: Dict[str, VisitedNode] = {}
        self._step_counter = 0
        self._start_time: Optional[datetime] = None

    def start_traversal(self, plan: Any) -> None:
        """
        Start a new traversal trace.

        Args:
            plan: TraversalPlan being executed
        """
        self.steps = []
        self.visited_tree = {}
        self._step_counter = 0
        self._start_time = datetime.now()

    def record_transition(
        self,
        transition: Any,
        screen_info: Optional[Dict[str, Any]] = None,
    ) -> None:
        """
        Record a state transition.

        Args:
            transition: StateTransition object or dict
            screen_info: Optional screen information
        """
        self._step_counter += 1

        # Handle both dict and object input
        if isinstance(transition, dict):
            # Dict input from GraphTraversalEngine
            from_state = transition.get("from_state", "")
            to_state = transition.get("to_state", "")
            node_id = transition.get("node_id")
            metadata = transition.get("metadata", {})
            timestamp_str = transition.get("timestamp")
            if timestamp_str:
                try:
                    timestamp = datetime.fromisoformat(timestamp_str)
                except:
                    timestamp = datetime.now()
            else:
                timestamp = datetime.now()
        else:
            # Object input (TraversalStateTransition)
            from_state = transition.from_state.value if hasattr(transition.from_state, "value") else str(transition.from_state)
            to_state = transition.to_state.value if hasattr(transition.to_state, "value") else str(transition.to_state)
            node_id = transition.node_id
            metadata = transition.metadata if hasattr(transition, 'metadata') else {}
            timestamp = datetime.now()

        step = TraceStep(
            step_number=self._step_counter,
            timestamp=timestamp,
            from_state=from_state,
            to_state=to_state,
            node_id=node_id,
            metadata=metadata,
            screen_info=screen_info or {},
        )

        self.steps.append(step)

        # Update visited tree
        if step.node_id:
            self._update_visited_tree(step)

    def _update_visited_tree(self, step: TraceStep) -> None:
        """Update visited tree with a new step."""
        if step.node_id not in self.visited_tree:
            self.visited_tree[step.node_id] = VisitedNode(
                node_id=step.node_id,
                name=step.node_id,  # Would use actual node name
                node_type="unknown",
            )

        node = self.visited_tree[step.node_id]

        # Mark as visited
        if not node.visited:
            node.visited = True
            node.visit_time = step.timestamp

        # Handle state-specific updates
        if step.to_state == "node_select":
            # Node being selected
            pass
        elif step.from_state == "execute" and step.to_state == "result_verify":
            # Node executed successfully
            pass

    def get_trace(self) -> List[TraceStep]:
        """Get copy of trace steps."""
        return self.steps.copy()

    def get_step_count(self) -> int:
        """Get total number of steps."""
        return len(self.steps)

    def get_elapsed_time(self) -> float:
        """Get elapsed time since start."""
        if self._start_time:
            return (datetime.now() - self._start_time).total_seconds()
        return 0.0

    # ========================================================================
    # Visualization Methods
    # ========================================================================

    def render_tree(self, max_depth: Optional[int] = None) -> str:
        """
        Render trace as ASCII tree.

        Args:
            max_depth: Maximum depth to render (None for unlimited)

        Returns:
            ASCII tree string
        """
        lines = []

        def render_node(node_id: str, depth: int, is_last: bool, prefix: str) -> None:
            if max_depth is not None and depth > max_depth:
                return

            node = self.visited_tree.get(node_id)
            if not node:
                return

            # Determine node marker
            visited_mark = "✓" if node.visited else "✗"
            restored_mark = " (已恢复)" if node.restored else ""

            # Build line
            connector = "└── " if is_last else "├── "
            node_line = f"{prefix}{connector}{node.name} [{node.node_type}] {visited_mark}{restored_mark}"

            # Add expected operation if present
            if node.expected_operation:
                node_line += f" → {node.expected_operation}"

            lines.append(node_line)

            # Render children
            children = node.children or []
            for i, child_id in enumerate(children):
                child_is_last = (i == len(children) - 1)
                child_prefix = prefix + ("    " if is_last else "│   ")
                render_node(child_id, depth + 1, child_is_last, child_prefix)

        # Find root nodes (no parents)
        all_children = set()
        for node in self.visited_tree.values():
            all_children.update(node.children or [])

        roots = [nid for nid in self.visited_tree if nid not in all_children]

        # Render each root
        for i, root_id in enumerate(roots):
            is_last = (i == len(roots) - 1)
            render_node(root_id, 0, is_last, "")

        return "\n".join(lines) if lines else "(empty)"

    def render_tree_with_reasons(self, max_depth: Optional[int] = None) -> str:
        """
        Render trace as ASCII tree with unvisited reasons.

        Shows why nodes were not visited by checking trace steps for SKIP or FAIL states.

        Args:
            max_depth: Maximum depth to render (None for unlimited)

        Returns:
            ASCII tree string with unvisited reasons
        """
        lines = []

        # Build a map of node_id -> skip reason
        skip_reasons = {}
        for step in self.steps:
            if step.to_state in ("SKIP", "FAIL", "PRECONDITION_FAILED"):
                if step.metadata:
                    skip_reasons[step.node_id] = step.metadata

        def render_node(node_id: str, depth: int, is_last: bool, prefix: str) -> None:
            if max_depth is not None and depth > max_depth:
                return

            node = self.visited_tree.get(node_id)
            if not node:
                return

            # Determine node marker
            if node.visited:
                visited_mark = "✓"
                restored_mark = " (已恢复)" if node.restored else ""
            else:
                visited_mark = "✗"
                restored_mark = ""

            # Build line
            connector = "└── " if is_last else "├── "
            node_line = f"{prefix}{connector}{node.name} [{node.node_type}] {visited_mark}{restored_mark}"

            # Add expected operation if present
            if node.expected_operation:
                node_line += f" → {node.expected_operation}"

            lines.append(node_line)

            # Add reason line if unvisited and reason exists
            if not node.visited and node_id in skip_reasons:
                reason = skip_reasons[node_id].get("reason", "unknown")
                details = skip_reasons[node_id].get("details", "")
                reason_prefix = prefix + ("    " if is_last else "│   ")
                lines.append(f"{reason_prefix}    ⚠️  {reason}")
                if details:
                    lines.append(f"{reason_prefix}    └─ {details}")

            # Render children
            children = node.children or []
            for i, child_id in enumerate(children):
                child_is_last = (i == len(children) - 1)
                child_prefix = prefix + ("    " if is_last else "│   ")
                render_node(child_id, depth + 1, child_is_last, child_prefix)

        # Find root nodes (no parents)
        all_children = set()
        for node in self.visited_tree.values():
            all_children.update(node.children or [])

        roots = [nid for nid in self.visited_tree if nid not in all_children]

        # Render each root
        for i, root_id in enumerate(roots):
            is_last = (i == len(roots) - 1)
            render_node(root_id, 0, is_last, "")

        return "\n".join(lines) if lines else "(empty)"

    def get_unvisited_summary(self) -> List[Dict[str, Any]]:
        """
        Get summary of unvisited nodes with reasons.

        Returns:
            List of dicts with node_id, name, reason, details
        """
        # Build a map of node_id -> skip reason
        skip_reasons = {}
        for step in self.steps:
            if step.to_state in ("SKIP", "FAIL", "PRECONDITION_FAILED"):
                if step.metadata:
                    skip_reasons[step.node_id] = step.metadata

        unvisited = []
        for node_id, node in self.visited_tree.items():
            if not node.visited:
                info = {
                    "node_id": node_id,
                    "name": node.name,
                    "node_type": node.node_type,
                }
                if node_id in skip_reasons:
                    info["reason"] = skip_reasons[node_id].get("reason", "unknown")
                    info["details"] = skip_reasons[node_id].get("details", "")
                else:
                    info["reason"] = "not_attempted"
                    info["details"] = "Node was not reached during traversal"
                unvisited.append(info)

        return unvisited

    def render_mermaid(self) -> str:
        """
        Render trace as Mermaid state diagram.

        Returns:
            Mermaid diagram string
        """
        lines = ["stateDiagram-v2", "    [*] --> NODE_SELECT"]

        # Add transitions
        for step in self.steps:
            from_label = step.from_state.upper().replace(" ", "_")
            to_label = step.to_state.upper().replace(" ", "_")
            lines.append(f"    {from_label} --> {to_label} : Step {step.step_number}")

        # Add termination
        if self.steps:
            last_state = self.steps[-1].to_state.upper().replace(" ", "_")
            lines.append(f"    {last_state} --> [*]")

        return "\n".join(lines)

    def render_html(self) -> str:
        """
        Render trace as HTML report.

        Returns:
            HTML document string
        """
        # Use enhanced tree with reasons
        tree_html = self.render_tree_with_reasons().replace("\n", "<br>\n")

        # Build operation comparison table
        total = len(self.visited_tree)
        visited = sum(1 for n in self.visited_tree.values() if n.visited)

        # Build table rows for state transitions
        table_rows = []
        for step in self.steps:
            action_badge = f'<span class="badge badge-action">{step.action}</span>' if step.action else '-'
            table_rows.append(f"""
                <tr>
                    <td>{step.step_number}</td>
                    <td>{step.from_state}</td>
                    <td>{step.to_state}</td>
                    <td>{step.node_id or '-'}</td>
                    <td>{action_badge}</td>
                    <td>{step.timestamp.strftime('%H:%M:%S.%f')[:-3]}</td>
                </tr>
            """)

        # Build operation comparison table
        comparison_rows = []
        for node_id, node in self.visited_tree.items():
            if node.expected_operation:
                actual = next((s.action for s in self.steps if s.node_id == node_id and s.action), None)
                actual_str = actual if actual else '未执行'
                status_class = 'success' if node.visited else 'warning'
                status_icon = '✅' if node.visited else '❌'
                comparison_rows.append(f"""
                    <tr class="{status_class}">
                        <td>{status_icon}</td>
                        <td>{node.name}</td>
                        <td><code>{node.expected_operation}</code></td>
                        <td><code>{actual_str}</code></td>
                    </tr>
                """)

        html = f"""
<!DOCTYPE html>
<html>
<head>
    <title>V6 遍历追踪报告</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            margin: 0;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background-color: white;
            padding: 30px;
            border-radius: 12px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }}
        h1 {{
            color: #333;
            border-bottom: 3px solid #4CAF50;
            padding-bottom: 15px;
            margin-bottom: 30px;
        }}
        h2 {{
            color: #555;
            margin-top: 35px;
            margin-bottom: 15px;
            font-size: 1.3em;
        }}
        .summary {{
            display: flex;
            gap: 20px;
            margin: 25px 0;
        }}
        .metric {{
            background: linear-gradient(135deg, #e8f5e8 0%, #f0f9f0 100%);
            padding: 20px;
            border-radius: 8px;
            flex: 1;
            text-align: center;
            border: 1px solid #c8e6c9;
        }}
        .metric-value {{
            font-size: 28px;
            font-weight: bold;
            color: #4CAF50;
        }}
        .metric-label {{
            color: #666;
            margin-top: 8px;
            font-size: 0.9em;
        }}
        .tree {{
            background-color: #fafafa;
            padding: 20px;
            border-radius: 8px;
            border-left: 4px solid #4CAF50;
            margin-bottom: 20px;
            font-family: 'Monaco', 'Menlo', monospace;
            font-size: 14px;
            line-height: 1.6;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            margin-top: 15px;
        }}
        th, td {{
            border: 1px solid #e0e0e0;
            padding: 12px;
            text-align: left;
        }}
        th {{
            background: linear-gradient(135deg, #4CAF50 0%, #45a049 100%);
            color: white;
            font-weight: 600;
        }}
        tr:nth-child(even) {{
            background-color: #f8f8f8;
        }}
        tr:hover {{
            background-color: #f0f0f0;
        }}
        tr.success {{
            background-color: #e8f5e8;
        }}
        tr.warning {{
            background-color: #fff3e0;
        }}
        code {{
            background-color: #f5f5f5;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 0.9em;
        }}
        .badge {{
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 0.85em;
            font-weight: 500;
        }}
        .badge-action {{
            background-color: #2196F3;
            color: white;
        }}
        .badge-skip {{
            background-color: #FF9800;
            color: white;
        }}
    </style>
</head>
<body>
    <div class="container">
        <h1>🎨 V6 遍历追踪报告</h1>

        <div class="summary">
            <div class="metric">
                <div class="metric-value">{len(self.steps)}</div>
                <div class="metric-label">总步骤数</div>
            </div>
            <div class="metric">
                <div class="metric-value">{visited}/{total}</div>
                <div class="metric-label">已访问节点 ({visited*100//total if total > 0 else 0}%)</div>
            </div>
            <div class="metric">
                <div class="metric-value">{total-visited}</div>
                <div class="metric-label">未访问节点</div>
            </div>
        </div>

        <h2>📊 访问树 (含预期操作和未访问原因)</h2>
        <div class="tree">
            <pre>{tree_html}</pre>
        </div>

        <h2>🔄 操作对比 (预期 vs 实际)</h2>
        <table>
            <thead>
                <tr>
                    <th>状态</th>
                    <th>节点名称</th>
                    <th>预期操作</th>
                    <th>实际执行</th>
                </tr>
            </thead>
            <tbody>
                {''.join(comparison_rows)}
            </tbody>
        </table>

        <h2>📋 状态转换追踪</h2>
        <table>
            <thead>
                <tr>
                    <th>步骤</th>
                    <th>从状态</th>
                    <th>到状态</th>
                    <th>节点 ID</th>
                    <th>操作</th>
                    <th>时间戳</th>
                </tr>
            </thead>
            <tbody>
                {''.join(table_rows)}
            </tbody>
        </table>
    </div>
</body>
</html>
        """

        return html

    def export_trace(self, format: str = "jsonl") -> str:
        """
        Export trace in specified format.

        Args:
            format: Export format ("jsonl" or "html")

        Returns:
            Exported trace string
        """
        if format == "jsonl":
            lines = [json.dumps(step.to_dict()) for step in self.steps]
            return "\n".join(lines)

        elif format == "html":
            return self.render_html()

        else:
            raise ValueError(f"Unknown format: {format}")

    def clear(self) -> None:
        """Clear all trace data."""
        self.steps = []
        self.visited_tree = {}
        self._step_counter = 0
        self._start_time = None
