# Proposal: Perception Actionable Toggle Evidence

| Attribute | Value |
|-----------|-------|
| Change ID | `perception-actionable-toggle-evidence` |
| Status | Proposed |
| Type | Capability baseline |
| Date | 2026-08-15 |
| Prerequisite | `physical-scroll-container-semantic-traversal` (GRADUATED, PHYSICAL_SCROLL_SEMANTIC_MECHANISM_DETERMINISTICALLY_VERIFIED) |
| Buyer | LIVE PHYSICAL SEMANTIC ACTIONABILITY (toggle evidence for Binding + StateBeliefReducer) |

## Why

The graduated Scroll mechanism (F5 fix, DEFERRED_BOUNDED) cannot be proven live because the YOLO perception model on Android 15 / API 35 does not classify control elements. All perception candidates have `perception_type = empty` (YOLO labels are not detected). Without `perception_type = "toggle"`, the existing BindingAnalysis cannot find toggle elements, and StateBeliefReducer cannot determine switch states.

This change establishes the minimum Perception capability required for the existing Runtime semantic mechanism to consume toggle evidence.

## What

- Extend Python fusion heuristics to infer toggle type from structural/geometric evidence when YOLO does not provide the label
- Ensure canonical perception_type = "toggle" survives through the entire pipeline to ObservedElement
- Add deterministic falsifiers (PER-T1..T12)
- Add integration tests through Binding and StateBeliefReducer
- Capture API 35 reality assets
- Do NOT train YOLO, do NOT modify Runtime semantic model, do NOT modify Adapter contracts

## Non-Goals

- YOLO training
- General perception rewrite
- LLM/VLM
- New adapter contracts
- Runtime semantic model changes
- Scroll mechanism changes
- Second screenshot/pass
