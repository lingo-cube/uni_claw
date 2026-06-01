"""
State Machine System for uni-claw V4.0

This module provides the three-layer state machine system:
- GlobalStateMachine: Manages traversal task lifecycle
- TraversalStateMachine: Handles individual node execution
- NodeStack: Maintains depth-first traversal context
- StateMachineOrchestrator: Coordinates all components
"""

from .global_fsm import GlobalStateMachine, GlobalState, GlobalStateTransition
from .traversal_fsm import TraversalStateMachine, TraversalState
from .node_stack import NodeStack, StackFrame
from .interaction import (
    StateMachineOrchestrator,
    TraversalContext,
    NavigationResult,
)

__all__ = [
    # Global state machine
    "GlobalStateMachine",
    "GlobalState",
    "GlobalStateTransition",
    # Traversal state machine
    "TraversalStateMachine",
    "TraversalState",
    # Node stack
    "NodeStack",
    "StackFrame",
    # Interaction
    "StateMachineOrchestrator",
    "TraversalContext",
    "NavigationResult",
]
