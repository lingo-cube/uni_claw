"""Tree formatter for traversal results visualization."""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional
from enum import Enum


class NodeType(Enum):
    """Type of tree node."""

    ROOT = "root"
    FOLDER = "folder"
    ITEM = "item"
    ACTION = "action"
    ERROR = "error"
    SKIP = "skip"


@dataclass
class TreeNode:
    """A node in the traversal result tree."""

    id: str
    name: str
    type: NodeType
    level: int
    children: List["TreeNode"] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict:
        """Convert to dictionary."""
        return {
            "id": self.id,
            "name": self.name,
            "type": self.type.value,
            "level": self.level,
            "children": [child.to_dict() for child in self.children],
            "metadata": self.metadata,
        }

    def to_markdown(self, indent: str = "") -> str:
        """Convert to markdown format.

        Args:
            indent: Current indentation string

        Returns:
            Markdown representation
        """
        lines = []
        prefix = "  " * (self.level - 1)

        # Icon based on type
        icons = {
            NodeType.ROOT: "📱",
            NodeType.FOLDER: "📁",
            NodeType.ITEM: "📄",
            NodeType.ACTION: "⚡",
            NodeType.ERROR: "❌",
            NodeType.SKIP: "⏭️",
        }

        icon = icons.get(self.type, "•")

        # Build node line
        metadata_str = ""
        if self.metadata.get("duration_ms"):
            metadata_str = f" ({self.metadata['duration_ms']:.0f}ms)"
        if self.metadata.get("coordinate"):
            coord = self.metadata["coordinate"]
            metadata_str += f" @({coord.get('x', 0):.2f}, {coord.get('y', 0):.2f})"

        lines.append(f"{prefix}{icon} {self.name}{metadata_str}")

        # Add children
        for child in self.children:
            lines.append(child.to_markdown())

        return "\n".join(lines)


class TraversalTreeBuilder:
    """Builder for traversal result trees."""

    def __init__(self):
        """Initialize tree builder."""
        self.root: Optional[TreeNode] = None
        self.node_map: Dict[str, TreeNode] = {}

    def build_from_visited_items(self, visited_items: List[Dict]) -> TreeNode:
        """Build tree from visited items list.

        Args:
            visited_items: List of visited item dictionaries

        Returns:
            Root tree node
        """
        # Create root node
        self.root = TreeNode(
            id="root",
            name="Traversal Results",
            type=NodeType.ROOT,
            level=0,
        )
        self.node_map["root"] = self.root

        node_id_counter = 1

        for item in visited_items:
            path = item.get("path", [])
            item_name = item.get("name", "Unknown")
            item_type = item.get("type", "item")

            # Build path nodes
            current_node = self.root
            for i, path_part in enumerate(path):
                path_key = "|".join(path[:i+1])
                if path_key not in self.node_map:
                    folder_node = TreeNode(
                        id=f"node_{node_id_counter}",
                        name=path_part,
                        type=NodeType.FOLDER,
                        level=i + 1,
                    )
                    node_id_counter += 1
                    self.node_map[path_key] = folder_node
                    current_node.children.append(folder_node)
                    current_node = folder_node
                else:
                    current_node = self.node_map[path_key]

            # Add item node
            item_node = TreeNode(
                id=f"node_{node_id_counter}",
                name=item_name,
                type=NodeType.ITEM,
                level=current_node.level + 1,
                metadata={
                    "type": item_type,
                    "coordinate": item.get("coordinate"),
                    "duration_ms": item.get("duration_ms"),
                }
            )
            node_id_counter += 1
            current_node.children.append(item_node)

        return self.root

    def build_from_execution_log(self, execution_log: Dict) -> TreeNode:
        """Build tree from execution log.

        Args:
            execution_log: Execution result dictionary

        Returns:
            Root tree node
        """
        # Create root node
        self.root = TreeNode(
            id="root",
            name="Traversal Session",
            type=NodeType.ROOT,
            level=0,
            metadata={
                "total_steps": execution_log.get("total_steps", 0),
                "screens_analyzed": execution_log.get("screens_analyzed", 0),
                "duration_ms": execution_log.get("duration_ms"),
            }
        )

        # Add visited items as children
        visited_items = execution_log.get("visited_items", [])
        visited_folder = TreeNode(
            id="visited",
            name=f"Visited Items ({len(visited_items)})",
            type=NodeType.FOLDER,
            level=1,
        )
        self.root.children.append(visited_folder)

        for item in visited_items:
            item_node = TreeNode(
                id=f"visited_{item.get('name', '').replace(' ', '_')}",
                name=item.get("name", "Unknown"),
                type=NodeType.ITEM,
                level=2,
                metadata={
                    "type": item.get("type"),
                    "path": item.get("path"),
                    "coordinate": item.get("coordinate"),
                }
            )
            visited_folder.children.append(item_node)

        # Add skipped items
        skipped_items = execution_log.get("skipped_dangerous", [])
        if skipped_items:
            skipped_folder = TreeNode(
                id="skipped",
                name=f"Skipped Items ({len(skipped_items)})",
                type=NodeType.SKIP,
                level=1,
            )
            self.root.children.append(skipped_folder)

            for item_name in skipped_items:
                item_node = TreeNode(
                    id=f"skipped_{item_name.replace(' ', '_')}",
                    name=item_name,
                    type=NodeType.SKIP,
                    level=2,
                    metadata={"reason": "safety_check"},
                )
                skipped_folder.children.append(item_node)

        # Add error if present
        if execution_log.get("error"):
            error_node = TreeNode(
                id="error",
                name="Execution Error",
                type=NodeType.ERROR,
                level=1,
                metadata={"error": execution_log.get("error")},
            )
            self.root.children.append(error_node)

        return self.root

    def to_json(self) -> str:
        """Export tree to JSON string.

        Returns:
            JSON string representation
        """
        import json
        return json.dumps(self.root.to_dict(), indent=2, ensure_ascii=False)

    def to_markdown(self) -> str:
        """Export tree to markdown string.

        Returns:
            Markdown string representation
        """
        if not self.root:
            return "# Empty Tree"
        return f"# Traversal Results\n\n{self.root.to_markdown()}"

    def to_html(self) -> str:
        """Export tree to HTML string.

        Returns:
            HTML string representation
        """
        def build_html(node: TreeNode) -> str:
            """Build HTML for a node recursively."""
            icons = {
                NodeType.ROOT: "📱",
                NodeType.FOLDER: "📁",
                NodeType.ITEM: "📄",
                NodeType.ACTION: "⚡",
                NodeType.ERROR: "❌",
                NodeType.SKIP: "⏭️",
            }

            icon = icons.get(node.type, "•")
            metadata_html = ""

            if node.metadata.get("duration_ms"):
                metadata_html = f' <span class="metadata">({node.metadata["duration_ms"]:.0f}ms)</span>'
            if node.metadata.get("coordinate"):
                coord = node.metadata["coordinate"]
                metadata_html += f' <span class="coordinate">@({coord.get("x", 0):.2f}, {coord.get("y", 0):.2f})</span>'

            html = f'<li class="node {node.type.value}"><span class="icon">{icon}</span> <span class="name">{node.name}</span>{metadata_html}'

            if node.children:
                html += "<ul>"
                for child in node.children:
                    html += build_html(child)
                html += "</ul>"

            html += "</li>"
            return html

        if not self.root:
            return "<div class='tree'>Empty Tree</div>"

        return f"""<div class="tree">
<h2>Traversal Results</h2>
<ul>
{build_html(self.root)}
</ul>
</div>"""


class CorrelationEngine:
    """Engine for correlating traces with results."""

    def __init__(self, trace_analyzer, metrics_collector):
        """Initialize correlation engine.

        Args:
            trace_analyzer: TraceAnalyzer instance
            metrics_collector: MetricsCollector instance
        """
        self.trace_analyzer = trace_analyzer
        self.metrics_collector = metrics_collector

    def correlate_session(self, trace_id: str, result: Dict) -> Dict:
        """Correlate a trace session with execution result.

        Args:
            trace_id: Trace ID
            result: Execution result dictionary

        Returns:
            Correlated data dictionary
        """
        session = self.trace_analyzer.get_session(trace_id)

        correlated = {
            "trace_id": trace_id,
            "result": result,
            "session": None,
            "timeline": [],
            "metrics": {},
        }

        if session:
            correlated["session"] = {
                "trace_id": session.trace_id,
                "duration_ms": session.duration_ms,
                "span_count": session.span_count,
                "event_count": len(session.events),
            }
            correlated["timeline"] = self.trace_analyzer.get_trace_timeline(trace_id)

        # Add metrics
        correlated["metrics"] = {
            "ai_calls": self.metrics_collector.get_ai_metrics_summary(),
            "traversal": self.metrics_collector.get_traversal_metrics_summary(),
        }

        return correlated

    def build_correlation_tree(self, trace_id: str, result: Dict) -> TreeNode:
        """Build a correlation tree with trace data.

        Args:
            trace_id: Trace ID
            result: Execution result

        Returns:
            Tree node with correlation data
        """
        builder = TraversalTreeBuilder()
        tree = builder.build_from_execution_log(result)

        # Add trace metadata to root
        session = self.trace_analyzer.get_session(trace_id)
        if session:
            tree.metadata["trace_id"] = trace_id
            tree.metadata["trace_duration_ms"] = session.duration_ms
            tree.metadata["span_count"] = session.span_count

        # Add performance metrics
        component_stats = self.trace_analyzer.analyze_component_performance()
        if component_stats:
            perf_node = TreeNode(
                id="performance",
                name="Performance Metrics",
                type=NodeType.FOLDER,
                level=tree.level + 1,
                metadata=component_stats,
            )
            tree.children.append(perf_node)

        return tree


__all__ = [
    "NodeType",
    "TreeNode",
    "TraversalTreeBuilder",
    "CorrelationEngine",
]
