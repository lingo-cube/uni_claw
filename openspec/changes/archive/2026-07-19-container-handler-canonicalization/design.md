## Context

`ContainerHandler` (D-16: 3-subcomponent pipeline — `CompletionDetector` 5-priority chain / `FallbackDecider` / `ContainerActionExecutor`, pure functions, unit-test covered) **is dormant** — zero production call sites, only unit tests exercise it. Production container completion is handled by `InterceptionHandler` instead (`FrameCompleted` scattered across 9 sites, interleaved with navigation/scroll detection, ad-hoc judgments). This is **duplicate container-completion logic** — one good implementation (dormant), one live (ad-hoc).

Change A (`plancompiler-default-alignment`, already landed) defined the intent layer (`CompletionPolicy` semantics, `IntentSlots.Depth/Entry`, `PlanCompiler` derivation) — dormant-safe. **This change (Change B)** makes the engine **consume** the intent layer: wire ContainerHandler, connect `IntentSlots.Depth`, preserve `TraversalResult.Reason` fidelity, delete `ExitCondition`, rename `CompletionPolicyType.None → Exhaustive`. **Engine-side, behavioral change** — it modifies the live frame completion path, not dormant-safe.

The boundary split:
- **InterceptionHandler** = event detection + produce facts (nav, scroll, child count, fingerprint). No longer sets `FrameCompleted`.
- **ContainerHandler** = completion decision (sole authority). 5-priority chain → `FrameCompleted` decision + `CompletionReason` + fallback action.

## Goals / Non-Goals

**Goals:**
- Wire ContainerHandler into the engine (dormant → live), making it the sole authority for container completion
- Strip completion judgment from InterceptionHandler, delegating to ContainerHandler
- Connect `IntentSlots.Depth` via priority `min(config.MaxDepth, intent.Depth)` into `CompletionContext.MaxDepth`
- Preserve `TraversalResult.Reason` in 4 tiers (Achieved / Constraint-pruned / Anomaly / External); invariant: anomaly never masquerades as AllVisited
- Delete `ExitCondition` record, `ExitConditionType` enum, `TraversalNode.ExitCondition` field, `CompletionContext.ExitConditionFallback` field
- Rename `CompletionPolicyType.None → Exhaustive` + sync engine L286 guard
- Route nav-subframe AutoEscape through context detection (NodeType/Meta), not `ExitCondition.Fallback` field

**Non-Goals:**
- Adding new fields to `TraversalResult` for constraint context (D-86 reads `IntentSlots.Depth` from plan instead)
- Changing `FallbackAction` enum — retained for FallbackDecider
- Adding new `NodeType` enum values — nav-subframe detected via Meta flag to avoid enum changes
- Modifying `CompletionDetector` 5-priority chain logic — already correct, just dormant

## Decisions

### Decision 1: InterceptionHandler delegates to ContainerHandler (not the reverse)

ContainerHandler is the completion authority. InterceptionHandler produces facts → ContainerHandler decides. This keeps completion logic in one place (5-priority chain) rather than scattered across 9 hook sites.

**Alternatives considered:**
- Keep ContainerHandler as a helper called by InterceptionHandler ad-hoc → defeats the purpose, still scattered
- Merge both into one class → violates single responsibility; event detection and completion judgment are fundamentally different concerns

### Decision 2: `ContainerActionResult → FrameCompleted` translation at call site

`HandleContainer` returns `ContainerActionResult` (Action + Success), not a boolean. The caller translates: `Back`/`AutoEscape`/`Skip` → `FrameCompleted = true`; `Abort` → no `FrameCompleted` (engine error/abort path, produces Error reason).

**Rationale:** Decouples completion detection from frame lifecycle action. The same completion reason can trigger different frame actions depending on context.

### Decision 3: Depth priority — `min(config.MaxDepth, intent.Depth)`

`effective_depth = min(config.MaxDepth, plan.IntentSlots.Depth ?? ∞)`. Config is the deploy-level hard ceiling; intent tightens within it. When depth bites, it's expected (constraint in effect) → Layer C (plan carries Depth) + global AllVisited. No separate anomaly depth tier; runaway is caught by AntiLoop + MaxSteps.

### Decision 4: Reason 4-tier classification

| Tier | Reasons | Meaning |
|------|---------|---------|
| Achieved | AllVisited, TargetFound | Normal completeness proof |
| Constraint-pruned | MaxSteps, Timeout | Scoped: over-cap/budget elements out-of-scope |
| Anomaly | AntiLoop, Error | Hard failure, completeness not claimed |
| External | Cancelled | User abort |

**Key invariant:** Anomaly never masquerades as AllVisited. MaxDepth/ScrollEnd are Layer A per-container events (cascade-aggregated to global AllVisited), NOT Layer B global reasons.

### Decision 5: Delete ExitCondition entirely (not deprecate)

Once ContainerHandler is wired, `ExitCondition` has zero live consumers. Full deletion (record + enum + field + CompletionContext field) rather than `[Obsolete]` — the type is internal, no public API surface to protect.

### Decision 6: Nav-subframe AutoEscape via Meta flag (not NodeType)

Nav subframes are detected via a Meta flag on the node, not a new `NodeType` enum value. This avoids modifying the locked `NodeType` enum and keeps the detection local to the frame context.

### Decision 7: Exhaustive rename (deferred from Change A)

`CompletionPolicyType.None → Exhaustive`. Change A already clarified the semantics (None = exhaustive intent); this change executes the rename + syncs engine L286 guard `policy.Type != None` → `policy.Type != Exhaustive`.

## Risks / Trade-offs

- **[Behavioral change → baseline breakage]** ContainerHandler's 5-priority chain may not be equivalent to InterceptionHandler's ad-hoc judgments. ~20 baselines directly affected. Mitigation: triage each red baseline — if ContainerHandler is more correct, fix the test; if legitimate difference, record decision. This is value, not pure risk — it surfaces true completion semantics.

- **[Two changes must compound for end-to-end]** Change A (plan-side) + Change B (engine-side) together make model B work end-to-end. Each lands independently but semantic closure requires both. Mitigation: both changes designed, B can land before or after A.

- **[ExitCondition deletion is breaking]** 12 test files reference `ExitCondition` in `TraversalNode` constructor. Mitigation: grep for zero references before deletion; migrate all call sites in the same change.

- **[FallbackDecider default for AllVisited]** AllVisited → Back is now the FallbackDecider default (not a field read from `ExitConditionFallback`). If plan-influenced exit actions are needed later, add to FallbackDecider, not a field on CompletionContext.
