"""
Graph Model for uni-claw V4.0+

This module provides the unified node abstraction for traversal operations,
supporting both static graphs (pre-defined menu structures) and dynamic graphs
(runtime template matching).

V6 Additions:
- New enum types: ExitConditionType, FallbackAction, CompletionPolicyType, etc.
- New data classes: ExitCondition, CompletionPolicy, EntryPolicy, IntentSlots
- TraversalPlan: Top-level container for declarative traversal plans
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
    # V6 new exports
    ExitCondition,
    ExitConditionType,
    FallbackAction,
    CompletionPolicy,
    CompletionPolicyType,
    TargetFoundAction,
    MatchMode,
    EntryPolicy,
    EntryStrategy,
    TraversalMode,
    IntentSlots,
)

from .plan import TraversalPlan

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
    # V6 new enum types
    "ExitConditionType",
    "FallbackAction",
    "CompletionPolicyType",
    "TargetFoundAction",
    "MatchMode",
    "EntryStrategy",
    "TraversalMode",
    # V6 new data classes
    "ExitCondition",
    "CompletionPolicy",
    "EntryPolicy",
    "IntentSlots",
    # V6 plan model
    "TraversalPlan",
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
