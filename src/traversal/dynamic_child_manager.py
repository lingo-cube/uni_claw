"""Dynamic child generation, caching, invalidation, and deduplication."""

from typing import Any, Callable, Dict, List, Optional, Set

from src.graph.node import (
    ChildrenStrategyType,
    DynamicRule,
    Precondition,
    TraversalNode,
)
from src.graph.matcher import DynamicMatcher, MatchAction
from src.trace.context import TraversalRuntimeContext
from .page_snapshot_manager import PageSnapshotManager


class DynamicChildManager:
    """Manages dynamic child node lifecycle.

    Responsibilities:
    - Generate children from page analysis via DynamicMatcher
    - Cache generated children per container
    - Track (page_fingerprint, element_name) pairs to prevent
      recursive generation on the same page
    - Invalidate cache when page changes
    """

    def __init__(
        self,
        dynamic_matcher: Optional[DynamicMatcher],
        node_registry: Dict[str, TraversalNode],
        trace: Optional[Any] = None,
    ):
        self._dynamic_matcher = dynamic_matcher
        self._node_registry = node_registry
        self._trace = trace

        self._dynamic_children: Dict[str, List[TraversalNode]] = {}
        self._generated_pairs: Set[tuple] = set()

    # -- public ----------------------------------------------------------------

    def get_next_unvisited_child(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> Optional[str]:
        if node.node_id not in context.visited_children:
            context.visited_children[node.node_id] = set()

        visited = context.visited_children[node.node_id]
        strategy = node.children_strategy

        if not strategy:
            return None

        if strategy.type == ChildrenStrategyType.STATIC:
            for child_id in strategy.static_children:
                if child_id not in visited:
                    visited.add(child_id)
                    return child_id
            return None
        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            if node.node_id not in self._dynamic_children:
                self.generate(node, context)
            children = self._dynamic_children.get(node.node_id, [])
            for child in children:
                if child.node_id not in visited:
                    visited.add(child.node_id)
                    return child.node_id
            return None
        elif strategy.type == ChildrenStrategyType.NONE:
            return None
        return None

    def has_unvisited(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> Optional[bool]:
        if not node.children_strategy:
            return False
        if node.children_strategy.type == ChildrenStrategyType.NONE:
            return False
        visited = context.visited_children.get(node.node_id, set())
        if node.children_strategy.type == ChildrenStrategyType.STATIC:
            for child_id in node.children_strategy.static_children:
                if child_id not in visited:
                    return True
            return False
        elif node.children_strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            child_id = self.get_next_unvisited_child(node, context)
            return child_id is not None
        raise ValueError(
            f"Unsupported children_strategy type: {node.children_strategy.type}"
        )

    def generate(
        self, node: TraversalNode, context: TraversalRuntimeContext
    ) -> None:
        if not self._dynamic_matcher:
            self._dynamic_children[node.node_id] = []
            return

        page_fp = PageSnapshotManager.fingerprint(context.current_page_analysis)

        # 1. Convert rules
        rules = {}
        if node.children_strategy and node.children_strategy.dynamic_rules:
            for rule_id, rule in node.children_strategy.dynamic_rules.items():
                if isinstance(rule, dict):
                    rule_dict = rule
                else:
                    rule_dict = {
                        "match_condition": rule.match_condition,
                        "child_template": rule.child_template,
                        "action": rule.action if isinstance(rule.action, str) else rule.action.value,
                    }
                rules[rule_id] = rule_dict

        if rules:
            self._dynamic_matcher.load_rules(rules)

        # 2. Extract items from page analysis
        items = []
        page_analysis = context.current_page_analysis
        if page_analysis and hasattr(page_analysis, "items") and page_analysis.items:
            for idx, item in enumerate(page_analysis.items):
                item_type = item.type.value if hasattr(item.type, "value") else str(item.type)
                coord_x = 0.5
                coord_y = 0.5
                if hasattr(item, "coordinate") and item.coordinate:
                    coord_x = getattr(item.coordinate, "x", 0.5) or 0.5
                    coord_y = getattr(item.coordinate, "y", 0.5) or 0.5
                items.append({
                    "type": item_type,
                    "text": getattr(item, "name", ""),
                    "index": idx,
                    "coordinate_x": coord_x,
                    "coordinate_y": coord_y,
                })

        # 3. Match and instantiate
        results = self._dynamic_matcher.match_all(items, parent_node=node)
        children = []

        for r in results:
            if r.matched and r.action == MatchAction.GENERATE_CHILD:
                child = self._dynamic_matcher.instantiate_match(r)
                if child:
                    if not child.precondition:
                        child.precondition = Precondition(page_name=None, timeout_seconds=5.0)
                    if child.precondition.page_name is None:
                        child.precondition.page_name = None

                    if self._trace:
                        self._trace.record_dynamic_lifecycle(
                            event="created",
                            node_id=child.node_id,
                            parent_id=node.node_id,
                            match_rule_id=getattr(r, "rule_id", None),
                            element_id=getattr(r, "element_id", None),
                        )

                    # Dedup: skip same (page, element) pair
                    pair = (page_fp, child.name)
                    if pair in self._generated_pairs:
                        continue
                    self._generated_pairs.add(pair)

                    child.precondition.path = list(context.current_path) + [child.name]
                    self._node_registry[child.node_id] = child
                    children.append(child)
            else:
                if self._trace:
                    self._trace.record_skip_span(r)

        self._dynamic_children[node.node_id] = children

    def invalidate(self, node_id: str) -> None:
        self._dynamic_children.pop(node_id, None)

    def get_children(self, node: TraversalNode) -> List[str]:
        strategy = node.children_strategy
        if not strategy:
            return []
        if strategy.type == ChildrenStrategyType.STATIC:
            return strategy.static_children.copy()
        elif strategy.type == ChildrenStrategyType.DYNAMIC_MATCH:
            return []
        elif strategy.type == ChildrenStrategyType.NONE:
            return []
        return []
