# Perception Actionable Toggle Evidence - Reality Falsification Record

## Previous Graduation

- **Change**: perception-actionable-toggle-evidence
- **Maturity**: PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED
- **Decision date**: 2026-08-15

## Live Falsification

A real Android 15 / API35 Developer Options run falsified the graduation claim.

### Observed production reality

- YOLO candidates: 34 total
- Candidate types: `text_block` only
- Absent: `icon`, `switch`, `toggle`, empty-text control candidate

### Root cause

**PRIMARY**: RAW_CONTROL_CANDIDATE_GENERATION_GAP

**Specifically**:
- YOLO_CONTROL_CLASS_GAP: current YOLO weights do not detect control classes on API35 Developer Options page
- FUSION_DEPENDENCY_ON_PREEXISTING_CONTROL_CANDIDATE: the previous heuristic required an existing icon/empty-text candidate to infer a toggle from, but real API35 YOLO output contains no such candidates

### Synthetic fixture blind spot

The previous graduation relied on synthetic fixtures that manually supplied `icon` control-like candidates. The real API35 Perception pipeline does NOT produce such candidates on the Developer Options page. Therefore, the previous tests passed but did not reflect real production reality.

### Authority unchanged

- Toggle TYPE / candidate generation: Python Perception (owns candidate existence, bounds, type)
- Toggle STATE: C# ImageSwitchStateProvider (owns ON/OFF/UNKNOWN from current screenshot pixels)
- Binding, StateBeliefReducer, Agent, Traversal: unchanged

### Repair scope

The repair must extend the Python Perception production pipeline so toggle candidate generation does NOT require YOLO to have already emitted an icon/control candidate. The fusion layer must receive access to the already-loaded current frame pixels and perform bounded raw-pixel structural search.

### Archive retention

The archived original change is retained as historical evidence. It is NOT modified. The falsification is recorded in this separate decision document.

### Current effective status

REPAIR_REQUIRED
