# SC-P3-CAND-004 — Multi-Page Sibling Branch Progress and Honest Completion

> Phase 3 | Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`
> Approved Production Model Delta: one immutable `BranchProgressEvidence` type
> Production Fields: `+4` total — three immutable value fields plus one Agent-owned state field
> Enums: `+0` | Interfaces: `+0` | Components: `+0` | New Mutable-State Owners: `+0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/sibling-branch-progress/spec.md`

## Goal

Prove that completing one child Container does not complete its parent subtree while another approved sibling remains unproven, and that Agent can preserve evidence-backed cross-Container progress through child → parent → sibling navigation without fabricating or duplicating completion.

## Given

- Runtime is Running with parent semantic Container P active.
- A fresh P Observation exposes the complete approved bounded sibling affordances A and B.
- The bounded approved Plan contains the necessary local work and visible navigation affordances for A, B, and parent return; Plan remains a hypothesis rather than proof.
- Child A and child B each require local work before their Container may report local completion.
- Agent owns an initially empty immutable cross-Container progress state.

## When

Runtime traverses P → A, proves A locally complete, returns to P, traverses P → B, proves B locally complete, and returns/reconciles to P.

## Then

1. Fresh P evidence establishes exactly the approved sibling inventory A and B under parent identity P.
2. A is recorded complete only from valid A-local completion evidence that exists before the parent-return step.
3. Returning to P preserves A's evidence and leaves B explicitly unproven.
4. P/subtree completion and final Goal completion remain forbidden while B lacks proof.
5. Runtime enters B without erasing or fabricating A progress.
6. B is recorded complete only from valid B-local completion evidence that exists before its parent-return step.
7. Only after fresh reconciliation to P with valid A and B completion evidence may higher-level evidence support bounded P/subtree completion.
8. Final `RunState.Completed` remains controlled only by Agent evaluation of GoalEvidence.
9. Child-to-parent return uses existing approved visible affordances and existing action semantics.

## Negative Branches

1. A complete, B unvisited → P/subtree and Goal completion are forbidden.
2. Return from A before A-local completion → A remains incomplete.
3. Revisit A after valid completion → no new sibling identity or duplicate distinct completion.
4. Stale/absent P evidence → approved sibling inventory is not proven or replaced.
5. Conflicting parent/child semantic identity → progress is not attached to P and valid prior evidence is not erased.
6. Local child completion alone → no final Agent Goal completion.

## Evidence Required

1. Progress snapshots associate parent P with approved siblings A/B and source Observation sequences.
2. After A completes and Runtime returns to P, the snapshot contains A complete and B incomplete.
3. ActionHistory and journal show existing actions for local work and parent return; no new Back action exists.
4. Revisiting A leaves the number of approved and distinctly completed siblings unchanged.
5. Stale and conflicting inputs leave valid progress unchanged and produce no fabricated completion.
6. After B completes, the snapshot contains valid completion evidence for both A and B under P.
7. GoalEvidence and final RunState remain Agent-controlled throughout.
8. Equal RunId, bounded world input, Plan, and action sequence replay to equal progress, ActionHistory, Observations, journal, Trace, GoalEvidence, and final state.

## Ownership and Authority

- Agent owns the immutable cross-Container progress state, active Container transitions, high-level interpretation, GoalEvidence evaluation, and final RunState.
- Container owns only its semantic page, current Observation, and local completion/progress.
- Traversal owns local deterministic execution and journal evidence only.
- Environment reports external Observation and dispatch outcomes only.
- Recovery retains its frozen mechanism ownership and receives no progress or navigation authority.

## Backtracking Boundary

Parent return is execution mechanics inside this Scenario. The deterministic world exposes a visible approved return affordance and existing Tap performs it. No Back action, navigation graph/stack/tree, or manager is purchased.

## Explicitly Deferred

- Post-Recovery progress validity/resume.
- Autonomous discovered-candidate safety and generalized autonomous branch discovery.
- SC-S0-CAPSTONE-001 implementation or completion.
- NavigationGraph, PageGraph, TraversalGraph, stack/tree/hierarchy model, visited-set semantic type, TraversalContext, ResumeToken, BranchManager, ProgressManager, NavigationManager, FSM, or workflow engine.
- New Recovery semantics, Container hierarchy, real-device/Vision work, or Runtime structural refactor.
