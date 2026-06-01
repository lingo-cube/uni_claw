"""
Graph Model for uni-claw V4.0

This module provides the unified node abstraction for traversal operations,
supporting both static graphs (pre-defined menu structures) and dynamic graphs
(runtime template matching).
"""

from .node import (
    TraversalNode,
    NodeType,
    Operation,
    Target,
    Precondition,
    ChildrenStrategy,
    ChildrenStrategyType,
    DynamicRule,
    ErrorPolicy,
    RestoreAction,
)

from .template import (
    Template,
    TemplateRegistry,
    TemplateRegistryError,
    PlaceholderResolver,
    TemplateInstantiator,
    TemplateValidator,
)

from .matcher import (
    DynamicMatcher,
    MatchResult,
    MatchCondition,
    MatchAction,
)

__all__ = [
    # Node types and related classes
    "TraversalNode",
    "NodeType",
    "Operation",
    "Target",
    "Precondition",
    "ChildrenStrategy",
    "ChildrenStrategyType",
    "DynamicRule",
    "ErrorPolicy",
    "RestoreAction",
    # Template system
    "Template",
    "TemplateRegistry",
    "TemplateRegistryError",
    "PlaceholderResolver",
    "TemplateInstantiator",
    "TemplateValidator",
    # Dynamic matching
    "DynamicMatcher",
    "MatchResult",
    "MatchCondition",
    "MatchAction",
]
