"""
Trace analysis for V6.3 distributed tracing.

Builds an in-memory tree from a flat list of trace nodes, backfills
step_end / session_end data, and offers extraction views for common
analysis patterns.
"""

from collections import defaultdict
from typing import Any, Dict, List, Optional, Set, Tuple

from .models import SessionNode, SpanNode, StepNode, TraceNode


# ── Tree building ────────────────────────────────────────────────────────────


def build_tree(nodes: List[TraceNode]) -> Optional[SessionNode]:
    """Build an in-memory tree from a flat list of trace nodes.

    Steps:
    1. Index all nodes by span_id
    2. Attach children via parent_span_id
    3. Backfill step_end → StepNode.result
    4. Backfill session_end → SessionNode.status / end_time
    5. Return the root SessionNode

    Returns None if no SessionNode is found.
    """
    if not nodes:
        return None

    # Index all nodes by span_id
    index: Dict[str, TraceNode] = {}
    root: Optional[SessionNode] = None

    for node in nodes:
        index[node.span_id] = node
        if isinstance(node, SessionNode):
            root = node

    if root is None:
        return None

    # Wire children
    children_map: Dict[str, List[TraceNode]] = defaultdict(list)
    for node in nodes:
        pid = node.parent_span_id
        if pid and pid in index:
            children_map[pid].append(node)

    for span_id, kids in children_map.items():
        parent = index.get(span_id)
        if parent and hasattr(parent, "children"):
            parent.children = kids  # type: ignore[attr-defined]

    # Backfill step_end → StepNode
    for node in nodes:
        if isinstance(node, SpanNode) and node.span_type == "step_end":
            if node.step_span_id and node.step_span_id in index:
                step = index[node.step_span_id]
                if isinstance(step, StepNode):
                    step.result = node.metadata.get("result") if node.metadata else None

    # Backfill session_end → SessionNode
    for node in nodes:
        if isinstance(node, SpanNode) and node.span_type == "session_end":
            root.status = node.status or root.status
            root.end_time = node.timestamp

    return root


# ── Trace analyzer ───────────────────────────────────────────────────────────


class TraceAnalyzer:
    """Extracts structured views from trace data.

    Usage::

        nodes = storage.read(trace_id)
        analyzer = TraceAnalyzer(nodes)
        pages = analyzer.extract_page_tree()
    """

    def __init__(self, nodes: List[TraceNode]):
        self._nodes = nodes
        self._root = build_tree(nodes)
        self._span_index: Dict[str, TraceNode] = {n.span_id: n for n in nodes}

    # -- page tree -----------------------------------------------------------

    def extract_page_tree(self) -> Dict[str, Any]:
        """Extract a nested page hierarchy from StepNode.page_path entries.

        Returns a tree structure with page names as keys and visit counts.
        """
        tree: Dict[str, Any] = {}
        visit_counts: Dict[str, int] = defaultdict(int)

        for node in self._nodes:
            if isinstance(node, StepNode) and node.page_path:
                path = node.page_path
                cursor = tree
                for segment in path:
                    if segment not in cursor:
                        cursor[segment] = {}
                    cursor = cursor[segment]
                visit_counts[path[-1]] += 1

        return {
            "tree": tree,
            "visit_counts": dict(visit_counts),
            "total_pages": len(tree),
        }

    # -- state sequence ------------------------------------------------------

    def extract_state_sequence(self) -> List[Dict[str, Any]]:
        """Extract a time-ordered list of state transitions."""
        transitions: List[Dict[str, Any]] = []
        for node in self._nodes:
            if (
                isinstance(node, SpanNode)
                and node.span_type == "state_transition"
            ):
                transitions.append({
                    "from_state": node.from_state,
                    "to_state": node.to_state,
                    "timestamp": node.timestamp,
                    "span_id": node.span_id,
                    "parent_span_id": node.parent_span_id,
                })
        transitions.sort(key=lambda t: t["timestamp"])
        return transitions

    # -- span chain ----------------------------------------------------------

    def extract_span_chain(self, span_id: str) -> List[Dict[str, Any]]:
        """Return the full call chain from root to the given span_id."""
        chain: List[Dict[str, Any]] = []
        current = self._span_index.get(span_id)
        while current:
            chain.append({
                "span_id": current.span_id,
                "node_type": current.node_type,
                "timestamp": current.timestamp,
            })
            pid = current.parent_span_id
            current = self._span_index.get(pid) if pid else None
        chain.reverse()
        return chain

    # -- AI calls ------------------------------------------------------------

    def extract_ai_calls(self) -> List[Dict[str, Any]]:
        """Extract all AI call spans."""
        calls: List[Dict[str, Any]] = []
        for node in self._nodes:
            if isinstance(node, SpanNode) and node.span_type == "ai_call":
                calls.append({
                    "span_id": node.span_id,
                    "capability": node.capability,
                    "provider_id": node.provider_id,
                    "success": node.success,
                    "latency_ms": node.latency_ms,
                    "input_tokens": node.input_tokens,
                    "output_tokens": node.output_tokens,
                    "timestamp": node.timestamp,
                    "parent_span_id": node.parent_span_id,
                })
        calls.sort(key=lambda c: c["timestamp"])
        return calls

    # -- action sequence -----------------------------------------------------

    def extract_action_sequence(self) -> List[Dict[str, Any]]:
        """Extract all execution (action) spans in time order."""
        actions: List[Dict[str, Any]] = []
        for node in self._nodes:
            if isinstance(node, SpanNode) and node.span_type == "execution":
                actions.append({
                    "span_id": node.span_id,
                    "action": node.action,
                    "status": node.status,
                    "target": node.target,
                    "page_before": node.page_before,
                    "page_after": node.page_after,
                    "duration_ms": node.duration_ms,
                    "timestamp": node.timestamp,
                })
        actions.sort(key=lambda a: a["timestamp"])
        return actions

    # -- error statistics ----------------------------------------------------

    def extract_error_statistics(self) -> Dict[str, Any]:
        """Aggregate error spans into statistics."""
        errors: List[SpanNode] = [
            n
            for n in self._nodes
            if isinstance(n, SpanNode) and n.span_type == "error"
        ]

        by_type: Dict[str, int] = defaultdict(int)
        by_severity: Dict[str, int] = defaultdict(int)
        by_page: Dict[str, int] = defaultdict(int)
        error_list: List[Dict[str, Any]] = []

        for e in errors:
            by_type[e.error_type or "unknown"] += 1
            by_severity[e.severity or "unknown"] += 1
            # Try to infer page from parent step context
            parent_page = self._parent_page_for(e)
            if parent_page:
                by_page[parent_page] += 1
            error_list.append({
                "span_id": e.span_id,
                "error_type": e.error_type,
                "error_message": e.error_message,
                "severity": e.severity,
                "timestamp": e.timestamp,
                "page": parent_page,
            })

        return {
            "total_errors": len(errors),
            "by_type": dict(by_type),
            "by_severity": dict(by_severity),
            "by_page": dict(by_page),
            "errors": error_list,
        }

    def _parent_page_for(self, span: SpanNode) -> Optional[str]:
        """Walk up to find the enclosing step's page context."""
        pid = span.parent_span_id
        while pid:
            parent = self._span_index.get(pid)
            if parent is None:
                break
            if isinstance(parent, StepNode) and parent.page_path:
                return parent.page_path[-1] if parent.page_path else None
            pid = parent.parent_span_id
        return None

    # -- time analysis -------------------------------------------------------

    def extract_time_analysis(self) -> Dict[str, Any]:
        """Compute timing statistics across the trace."""
        timestamps = sorted(
            n.timestamp for n in self._nodes if n.timestamp > 0
        )
        if not timestamps:
            return {"total_duration_ms": 0, "step_count": 0}

        step_nodes = [n for n in self._nodes if isinstance(n, StepNode)]
        ai_spans = [
            n
            for n in self._nodes
            if isinstance(n, SpanNode) and n.span_type == "ai_call"
        ]
        exec_spans = [
            n
            for n in self._nodes
            if isinstance(n, SpanNode) and n.span_type == "execution"
        ]

        durations = [
            s.latency_ms
            for s in ai_spans + exec_spans
            if s.latency_ms is not None and s.latency_ms > 0
        ]

        total_ms = (timestamps[-1] - timestamps[0]) * 1000

        # Identify slowest spans
        all_duration_spans = [(s, s.latency_ms or 0) for s in ai_spans + exec_spans]
        all_duration_spans.sort(key=lambda x: x[1], reverse=True)

        def percentile(vals: List[float], p: float) -> float:
            if not vals:
                return 0.0
            vals_sorted = sorted(vals)
            idx = int(len(vals_sorted) * p / 100.0)
            return vals_sorted[min(idx, len(vals_sorted) - 1)]

        return {
            "total_duration_ms": total_ms,
            "step_count": len(step_nodes),
            "ai_call_count": len(ai_spans),
            "execution_count": len(exec_spans),
            "avg_latency_ms": sum(durations) / len(durations) if durations else 0,
            "p50_latency_ms": percentile([d for _, d in all_duration_spans], 50),
            "p95_latency_ms": percentile([d for _, d in all_duration_spans], 95),
            "slowest": [
                {"span_id": s.span_id, "latency_ms": d}
                for s, d in all_duration_spans[:5]
            ],
        }

    # -- coverage analysis ---------------------------------------------------

    def extract_coverage_analysis(self) -> Dict[str, Any]:
        """Analyze page and node coverage."""
        step_nodes = [n for n in self._nodes if isinstance(n, StepNode)]
        visited_pages: Set[str] = set()
        visited_nodes: Set[str] = set()

        for s in step_nodes:
            if s.page_path:
                visited_pages.update(s.page_path)
            if s.node_id:
                visited_nodes.add(s.node_id)

        # Count unique pages from page_path
        page_visits: Dict[str, int] = defaultdict(int)
        for s in step_nodes:
            if s.page_path:
                for p in s.page_path:
                    page_visits[p] += 1

        total_pages = len(visited_pages)
        total_nodes = len(visited_nodes)

        # Unvisited pages are those in page_path but not visited
        # (in this context, visited_pages == all_pages since we count from steps)

        return {
            "total_pages": total_pages,
            "total_nodes": total_nodes,
            "page_visits": dict(page_visits),
            "visit_percent": 100.0 if total_pages > 0 else 0.0,
            "most_visited": sorted(
                page_visits.items(), key=lambda x: x[1], reverse=True
            )[:10],
        }

    def extract_operation_tree(self) -> Dict[str, Any]:
        """Build an element-level operation tree from step + execution spans.

        Uses node_id naming convention to infer hierarchy:
          root
          └── menu_container-Wi-Fi-0-root          (type=menu_container, name=Wi-Fi, parent=root)
              ├── switch_leaf-ON-0-menu_container-… (type=switch, name=ON, parent=menu_container-Wi-Fi)
              └── menu_container-HomeNetwork-1-…    (type=menu_container, name=HomeNetwork, parent=…)

        Execution spans attach as actions under their step.
        """
        from collections import defaultdict

        step_nodes = [n for n in self._nodes if hasattr(n, 'step_type')]
        span_nodes = [n for n in self._nodes if hasattr(n, 'span_type')]

        # Parse node_id → {name, parent_node_id}
        def _parse_node_id(nid: str) -> tuple:
            """Return (name, parent_node_id) from encoded node_id."""
            if not nid or nid == "root":
                return ("root", None)
            parts = nid.split("-")
            if len(parts) < 3:
                return (nid, "root")
            # Find index position: the segment that is a bare digit followed
            # by the parent node_id. E.g. ...-Wi-Fi-0-root
            # Search backwards: last segment is the final part of parent.
            # Walk forward looking for a digit-only segment as the index.
            idx_pos = None
            for i, p in enumerate(parts):
                if p.isdigit() and i > 0:
                    idx_pos = i
                    break
            if idx_pos is None:
                return (nid, "root")
            name = "-".join(parts[0:idx_pos])  # re-join multi-word names like Wi-Fi
            parent = "-".join(parts[idx_pos + 1:])
            # Strip type prefix to get display name
            # e.g. "switch_leaf-ON" → "ON", "menu_container-Dark mode" → "Dark mode"
            name_parts = name.split("-", 1)
            display = name_parts[1] if len(name_parts) > 1 else name_parts[0]
            return (display, parent if parent else "root")

        # Build node lookup: node_id → {name, parent, children, actions, visited}
        nodes_by_id: Dict[str, Dict] = {}
        for s in step_nodes:
            nid = s.node_id or ""
            name, parent = _parse_node_id(nid)
            if nid not in nodes_by_id:
                nodes_by_id[nid] = {
                    "name": name,
                    "node_id": nid,
                    "parent": parent,
                    "page_path": list(s.page_path) if s.page_path else [],
                    "children": [],
                    "actions": [],
                    "visited": False,
                }
            nodes_by_id[nid]["visited"] = True

        # Attach execution spans to their owning step via parent_span_id.
        step_span_ids = {s.span_id: s.node_id for s in step_nodes}
        for sp in span_nodes:
            # Only include genuine execution spans
            if getattr(sp, 'span_type', '') != 'execution':
                continue
            action = getattr(sp, 'action', '') or ''
            if not action or action in ('entry_strategy', 'back', 'no_action'):
                continue
            pid = getattr(sp, 'parent_span_id', None) or ""
            if pid in step_span_ids:
                owner_nid = step_span_ids[pid]
                if owner_nid in nodes_by_id:
                    target = getattr(sp, 'target', None)
                    # Clean up target string: extract value from Target(...)
                    target_str = str(target) if target else None
                    if target_str and "Target(" in target_str:
                        import re
                        m = re.search(r"value='([^']+)'", target_str)
                        if m:
                            target_str = m.group(1)
                    nodes_by_id[owner_nid]["actions"].append({
                        "action": action,
                        "target": target_str,
                        "status": getattr(sp, 'status', '?') or '?',
                    })

        # Ensure root node exists (no_action container may not have a step)
        if "root" not in nodes_by_id:
            nodes_by_id["root"] = {
                "name": "root",
                "node_id": "root",
                "parent": None,
                "page_path": [],
                "children": [],
                "actions": [],
                "visited": True,
            }

        # Wire children
        for nid, node in nodes_by_id.items():
            parent = node.get("parent")
            if parent and parent in nodes_by_id and parent != nid:
                nodes_by_id[parent]["children"].append(node)

        # Find roots (nodes whose parent is not in the set, or parent is root/None)
        roots = [
            n for nid, n in nodes_by_id.items()
            if n.get("parent") is None
            or n.get("parent") == "root"
            or n.get("parent") not in nodes_by_id
        ]

        def _dedup_children(children: List[Dict]) -> List[Dict]:
            """Deduplicate children by node_id, merge + dedup actions."""
            seen: Dict[str, Dict] = {}
            for c in children:
                nid = c["node_id"]
                if nid not in seen:
                    seen[nid] = dict(c)
                    seen[nid]["children"] = list(c["children"])
                    # Dedup actions within this node
                    action_keys = set()
                    unique_actions = []
                    for a in seen[nid]["actions"]:
                        k = (a["action"], a["target"], a["status"])
                        if k not in action_keys:
                            action_keys.add(k)
                            unique_actions.append(a)
                    seen[nid]["actions"] = unique_actions
                else:
                    existing = {(a["action"], a["target"], a["status"]) for a in seen[nid]["actions"]}
                    for a in c["actions"]:
                        k = (a["action"], a["target"], a["status"])
                        if k not in existing:
                            seen[nid]["actions"].append(a)
                            existing.add(k)
            for v in seen.values():
                if v["children"]:
                    v["children"] = _dedup_children(v["children"])
            result = sorted(seen.values(), key=lambda x: (not x["children"], x.get("name", "")))
            return result

        # Build tree: attach children to root, dedup, return root's children
        tree = _dedup_children(roots)

        # If root has children, use root as single top-level
        root_node = nodes_by_id.get("root")
        if root_node and root_node.get("children"):
            root_node["children"] = _dedup_children(root_node["children"])
            tree = [root_node]
        elif not tree:
            tree = [root_node] if root_node else []

        all_actions = []
        def _collect_actions(branches):
            for b in branches:
                all_actions.extend(b.get("actions", []))
                if b.get("children"):
                    _collect_actions(b["children"])
        _collect_actions(tree)

        success = sum(1 for a in all_actions if a.get("status") == "success")
        fail = sum(1 for a in all_actions if a.get("status") == "failed")

        return {
            "tree": tree,
            "stats": {
                "total_steps": len(step_nodes),
                "total_actions": len(all_actions),
                "success_count": success,
                "fail_count": fail,
            },
        }
