## Why

SC-P3-CAND-008 can discover and complete a required branch whose concrete target is absent from the immutable Plan. SC-P3-CAND-005 can revalidate historical progress after Recovery only when the durable effect criterion is carried by a PlanStep. A discovered non-Plan branch therefore has historical completion provenance but no guaranteed criterion carrier for evaluating its effect against fresh recovered-world evidence.

SC-P3-CAND-009 purchases one bounded immutable branch-scoped carrier so Agent can distinguish revalidated, contradicted, and unresolved effects without fabricating retained progress, blindly redispatching work, mutating Plan, or introducing navigation infrastructure.

## What Changes

- Add one immutable `BranchEffectCriterion` value associating one bounded semantic branch identity with one deterministic Observation-only three-way evaluator.
- Add one optional immutable `Goal.DiscoveredBranchEffectCriterion` field carrying exactly one such association for the bounded Scenario.
- Require Agent to match the carrier against independently proven inventory and historical progress under the same parent scope.
- Require evaluation only from fresh evidence obtained after verified Agent Recovery.
- Preserve historical provenance separately from recovered-world effect validity.
- Keep `true` / `false` / `null` derived rather than stored as lifecycle or validity state.
- Preserve Agent ownership and authority and keep Recovery mechanism-only.

## Capabilities

### New Capabilities

- `discovered-branch-effect-revalidation`: Defines bounded identity-to-criterion association and fresh post-Recovery effect revalidation for one freshly discovered non-Plan branch.

### Modified Capabilities

None. SC-P3-CAND-004 continues to define progress provenance; SC-P3-CAND-005 continues to define PlanStep-carried effect revalidation; SC-P3-CAND-006 continues to define authorization; SC-P3-CAND-008 continues to define required-work inventory.

## Impact

- Expected production surface: one immutable two-field `BranchEffectCriterion` value and one optional immutable Goal field.
- Expected verification surface: positive, contradicted, unresolved, absent/mismatched carrier, stale evidence, no-blind-redispatch, and deterministic replay cases.
- Production budget: model types +1; fields +3; enums +0; interfaces +0; components/services +0; mutable-state fields +0; mutable-state owners +0.
- Ownership delta: none.
- Authority delta: none.
- No Plan mutation, BranchProgress/BranchInventory/GoalEvidence reinterpretation, registry, route/frontier state, generalized routing, new Recovery behavior, Runtime refactor, Capstone execution, or S0/Phase completion is purchased.
