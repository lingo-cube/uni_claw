# Tasks: semantic-perception-layer-baseline

> System of record. THIS GATE IS BASELINE ONLY (decision + proposal/design/spec/
> tasks + validation). Implementation tasks are pending the APPLY gate
> (`PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_LAYER_BASELINE`).

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/tasks)
- [x] Slice 1 — Decision document: `docs/decisions/semantic-perception-layer-baseline.md`
- [x] Slice 2 — Architecture freeze: Semantic belongs to Perception Layer; Vision and
      Semantic are parallel
- [x] Slice 3 — SemanticEvidence-only output contract (allowed: candidate,
      confidence, evidence source, explanation; forbidden: action decision, goal
      completion, world state mutation, planning, autonomous behavior)
- [x] Slice 4 — Runtime sole authority freeze (Evidence Fusion, ContainerIdentity,
      Binding, Belief update, Action authority)
- [x] Slice 5 — Semantic input boundary freeze (allowed: Current Observation,
      Visible Elements, Container History, Previous Verified Identity; forbidden:
      Goal, Action command, Expected state, Planning context)
- [x] Slice 6 — Phase 1 scope freeze: Container Identity Recovery only; excludes
      ElementRelation, Binding Semantic, Memory Learning, LLM reasoning, Vector
      database
- [x] Slice 7 — Fast/Slow evolution freeze (Fast vector retrieval; Slow LLM async
      checkpoint evidence, non-blocking)
- [x] Slice 8 — Memory boundary freeze (read-only; no Runtime vector writes;
      future pipeline independent)
- [x] Slice 9 — Non-goals freeze (no Agent, Planner, Memory system, LLM controller,
      Action generator)
- [x] Slice 10 — Validation: `openspec validate semantic-perception-layer-baseline
      --strict`, `openspec validate --changes --strict`, `scripts/check-consistency.sh`

## Implementation plan (APPLY gate — NOT EXECUTED)

- [ ] A1 — Define SemanticEvidence DTO shape for Container Identity Recovery (no
      production code in this gate)
- [ ] A2 — Define Fast Semantic retrieval interface boundary (no production code)
- [ ] A3 — Define Slow Semantic async checkpoint evidence boundary (no production
      code)
- [ ] A4 — Define Runtime fusion/identity consumption seam (no production code)
- [ ] A5 — Tests for identity recovery falsifiers (future APPLY)

## Falsifier mapping

- [x] F1 — Semantic-as-Agent → Semantic owns Decision, Goal, Planning, or Action
      authority (spec: Semantic output + input boundary requirements)
- [x] F2 — Semantic-not-perception → Semantic inside Agent instead of Perception
      Layer (spec: Semantic is a Perception Layer capability)
- [x] F3 — Vision-subsumes-Semantic → Vision and Semantic not parallel (spec:
      Semantic is a Perception Layer capability)
- [x] F4 — Decision output → Semantic produces Decision instead of SemanticEvidence
      (spec: Semantic output is SemanticEvidence only)
- [x] F5 — Runtime authority diluted → Runtime loses sole authority over fusion,
      identity, binding, belief, or action (spec: Runtime remains the sole
      authority)
- [x] F6 — input-boundary violation → Semantic receives Goal, Action command,
      Expected state, or Planning context (spec: Semantic input boundary is narrow)
- [x] F7 — phase-1 expansion → Phase 1 implements beyond Container Identity
      Recovery (spec: Phase 1 scope is Container Identity Recovery only)
- [x] F8 — slow blocking → Slow Semantic blocks Runtime main control flow (spec:
      Slow Semantic is non-blocking async checkpoint evidence)
- [x] F9 — memory learning → Runtime auto-learns or writes into Vector in this
      change (spec: Memory remains read-only in this change)
- [x] F10 — production mutation → baseline modifies Runtime/Vision/Assistance/DSH
      production code (spec: This change creates no Agent-like capability)

## Validation record

- [x] `openspec validate semantic-perception-layer-baseline --type change --strict --no-interactive` — PASS
- [x] `openspec validate --changes --strict --no-interactive` — new change PASS; pre-existing unrelated failure remains for `trace-capture-scenario-catalog-foundation` (no deltas)
- [x] `scripts/check-consistency.sh` — ALL PASS
