"""Data types for AI capabilities."""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Literal
from enum import Enum


# ============ ParseToPlan Types ============

@dataclass
class NodeOperation:
    """Operation definition for a traversal node."""
    action: str  # click, back, swipe, input_text, no_action
    target: Optional[Dict[str, Any]] = None  # {"by": "text", "value": "..."}
    params: Optional[Dict[str, Any]] = None
    restore: Optional[Dict[str, Any]] = None


@dataclass
class NodeStrategy:
    """Strategy for processing node children."""
    type: str  # dynamic_match, static, none
    dynamic_rules: Optional[Dict[str, Any]] = None
    static_children: Optional[List[str]] = None


@dataclass
class TraversalNode:
    """Node definition for traversal plan."""
    node_id: str
    name: str
    node_type: str  # container, leaf, etc.
    operation: NodeOperation
    precondition: Optional[Dict[str, Any]] = None  # {"page_name": "...", "ui_condition": "...", or None}
    children_strategy: NodeStrategy = field(default_factory=lambda: NodeStrategy(type="none"))
    error_policy: Optional[Any] = None


@dataclass
class TraversalPlan:
    """Result of instruction parsing - a complete traversal plan."""
    entry_app: Optional[str]
    root_node: TraversalNode
    static_nodes: List[TraversalNode] = field(default_factory=list)
    template_registry: str = "default"
    mode: Literal["hybrid", "concrete", "dynamic"] = "hybrid"
    reasoning: Optional[str] = None
    confidence: float = 1.0


# ============ VerifyPageType Types ============

@dataclass
class MismatchDetails:
    """Details about page type mismatch."""
    missing_items: List[str] = field(default_factory=list)
    unexpected_items: List[str] = field(default_factory=list)
    type_conflict: Optional[str] = None


@dataclass
class Suggestion:
    """Suggestion for handling page type mismatch."""
    action: Literal["back", "retry", "skip", "close_popup", "renavigate"]
    target: Optional[str] = None
    reason: str = ""


@dataclass
class PageTypeVerification:
    """Result of page type verification."""
    is_match: bool
    confidence: float
    actual_type: Literal["menu_list", "settings_group", "dialog", "home_desktop", "leaf_page", "unknown"]
    reasoning: str = ""
    mismatch_details: Optional[MismatchDetails] = None
    suggestion: Optional[Suggestion] = None


# ============ ScreenSafety Types ============

@dataclass
class SafetyEvaluation:
    """Safety evaluation for a single element."""
    name: str
    safety_tag: Literal["safe", "caution", "skip", "unknown"]
    confidence: float
    reason: str
    context_dependency: Optional[str] = None
    task_relevance: Optional[str] = None


@dataclass
class PageLevelGuidance:
    """Page-level safety guidance."""
    overall_safe_to_proceed: bool
    recommended_max_parallel: int = 3
    special_precautions: List[str] = field(default_factory=list)
    task_suitability: Optional[str] = None


@dataclass
class SafetyScreeningResult:
    """Result of element safety screening."""
    evaluations: List[SafetyEvaluation]
    page_level_guidance: Optional[PageLevelGuidance] = None


# ============ ContextDecision Types ============

@dataclass
class ContextDecisionResult:
    """Result of context decision making."""
    result: Literal["success", "unsure", "give_up", "wait", "safe_mode"]
    action: Literal["click", "back", "swipe", "scroll_down", "wait", "skip", "no_action"]
    target: Optional[Dict[str, Any]] = None  # {"by": "text|coordinate", "value": "..."}
    params: Optional[Dict[str, Any]] = None
    reasoning: str = ""
    confidence: float = 1.0
    safety_verified: bool = True


__all__ = [
    "TraversalPlan",
    "TraversalNode",
    "NodeOperation",
    "NodeStrategy",
    "PageTypeVerification",
    "MismatchDetails",
    "Suggestion",
    "SafetyScreeningResult",
    "SafetyEvaluation",
    "PageLevelGuidance",
    "ContextDecisionResult",
]
