# Proposal: Semantic Run Popup Obstruction Integration

| Attribute | Value |
|-----------|-------|
| Change ID | `semantic-run-popup-obstruction-integration` |
| Status | Proposed |
| Type | Mechanism integration |
| Date | 2026-08-15 |
| Buyer | POPUP_INTERRUPTION |
| Gap | SEMANTIC_RUN_POPUP_HANDLING_GAP |

## Why

The graduated SemanticRun loop does not handle unexpected blocking popups/local obstructions. PlanRun already has this capability through `IsLocalObstructionHypothesis`, `CanHandleLocalObstruction`, and `TryAcceptLocalObstruction`. This change integrates the same semantics into SemanticRun.

## What

- Detect local obstruction in SemanticRun
- Handle bounded obstruction (dismiss/back)
- Require fresh Observation after handling
- Verify obstruction cleared
- Reconcile Container
- Continue SAME Goal
- Reject stale grounding

## Non-Goals

- New popup framework
- New recovery authority
- LLM/VLM
- Scroll/Perception changes
- Agent authority changes
