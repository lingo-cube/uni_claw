# Proposal: Perception Actionable Toggle Evidence Reality Repair

| Attribute | Value |
|-----------|-------|
| Change ID | `perception-actionable-toggle-evidence-reality-repair` |
| Status | Proposed |
| Type | Capability repair |
| Date | 2026-08-15 |
| Previous Change | `perception-actionable-toggle-evidence` |
| Previous Maturity | PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED |
| Current Status | INVALIDATED_BY_LIVE_REALITY_EVIDENCE |

## Why

The previous graduation was falsified by live API35 reality. The YOLO model on the API35 Developer Options page outputs only `text_block` candidates. The previous fusion heuristic required an existing icon/empty-text control candidate to infer toggle type, which does not exist on the real page. This repair adds raw-pixel toggle candidate generation to the Python Perception pipeline.

## What

- Extend Python fusion pipeline to access raw image pixels
- Add bounded raw-pixel structural search for toggle-like candidates
- Generate toggle candidate bounds without requiring YOLO control detections
- Preserve canonical type mapping (switch -> toggle)
- Keep C# ImageSwitchStateProvider as the sole switch-state authority
- Add reality-focused regression tests

## Non-Goals

- YOLO training
- General perception framework
- Runtime semantic model changes
- Adapter contract changes
- Binding changes
- StateBeliefReducer changes
- LLM/VLM
- Second screenshot/pass
