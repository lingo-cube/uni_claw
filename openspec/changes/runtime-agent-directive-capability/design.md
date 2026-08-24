# Design: runtime-agent-directive-capability

> HOW to implement the bounded exploration directive → runtime-local plan hypothesis
> decomposition. See `proposal.md` for motivation and `specs/runtime-agent-directive-decomposition/spec.md`
> for the behavior contract. This design adds an immutable model + a stateless transform
> and reuses the existing open-world DFS engine unchanged.

## Context

The RuntimeAgent already owns a proven, evidence-driven open-world DFS engine
(`Agent.RunOpenWorldAsync`, `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs:37-706`)
fed by `TypeLevelTraversalSpecification` + a type-directed `Goal` (caller-injected
evaluators). The seam `Planning/IntentExecution.RunOpenWorldAsync` already forwards an
`IntentSemanticEnvelope.OpenWorldTypeLevel` to that engine after validating
`ExhaustiveWithinScope` + scope/entry match. The closed-world compiler
`Planning/IntentCompiler` is the established pattern: a **stateless, caller-side**
transform from business wording to a `SemanticGoalInput`, with no world observation and
no scenario strings.

The gap is the front of the exploration pipeline: every `TypeLevelTraversalSpecification`
today is manually constructed by a test/fixture (20+ sites), and there is no
production representation of an abstract exploration directive nor a deterministic path
from such a directive to the open-world execution inputs. Constraints that shape this
design:

- Architecture v1 invariants 2-4: RuntimeAgent owns execution/grounding/verification,
  not scenario-knowledge generation.
- Contract I-12 (YAGNI) + the Agent "不硬编码场景字符串" rule (裁决 3/11): the Runtime
  gains no scenario knowledge; strategy *rules* stay caller-injected.
- Contract I-1..I-3: one mutable-state owner, one decision authority, dependency
  direction Agent → Container → Traversal → Environment (unchanged here).
- ArchitectureGuardTests Guard 1/2: zero ProjectReference, no legacy namespace.

## Goals / Non-Goals

**Goals:**
- Provide an immutable `Directive` model for a bounded exploration intent (scope, entry,
  depth, safety, completion, caller-injected strategy rules).
- Provide a stateless `DirectiveDecomposer` that projects a `Directive` into the
  existing `TypeLevelTraversalSpecification` + type-directed `Goal` evaluator assembly.
- Feed the decomposed inputs to the existing `IntentExecution.RunOpenWorldAsync` seam so
  the proven DFS engine runs unchanged.
- Deterministic tests proving directive parsing, decomposition shape, authorization
  preservation, no authority escalation, and existing-suite regression.

**Non-Goals:**
- Natural-language parsing of "Explore Settings safely" into a `Directive`. Wording →
  directive compilation stays caller-side (same boundary as `IntentCompiler` wording →
  `SemanticGoalInput`). The `Directive` is the already-resolved bounded intent.
- Wiring `Directive` into the closed-world `RunStartRequest` wire surface. The
  production wire surface stays `SemanticGoalInput`; the directive path is an additive
  execution entry, not a replacement.
- Open-world plan revision / recovery integration (the mission's Phase 4). The existing
  `RunOpenWorldAsync` fail-closed behavior is unchanged here; recovery integration is
  tracked separately if a real buyer appears.
- Global planner, static navigation graph, universal UI knowledge, hardcoded Settings
  tree, or LLM inside the traversal loop — explicitly excluded by the mission and the
  frozen invariants.

## Decisions

### Decision 1: `Directive` is an immutable model in `Model/`, not a new component
**Choice:** `src/UniClaw.Runtime/Model/Directive.cs` — a sealed record carrying scope,
entry, depth, safety, completion, and a strategy-rule-set record. No methods beyond
construction-time validation.
**Rationale:** Matches `Model/`'s role (pure immutable models, no owner) and the
existing `TypeLevelTraversalSpecification`/`Goal`/`SemanticGoalInput` placement. Avoids
introducing a new component with architecture meaning (which would trigger expert
escalation). Validation mirrors `TypeLevelTraversalSpecification`'s ctor guards.
**Alternatives considered:** placing it under `Planning/` (rejected — it is a model, not
a planning procedure); making it a union of closed+open directive (rejected —
`SemanticGoalInput` already owns closed-world; a union would widen the surface and risk
I-13 God-Context drift).

### Decision 2: `DirectiveDecomposer` is a stateless static transform mirroring `IntentCompiler`
**Choice:** `src/UniClaw.Runtime/Planning/DirectiveDecomposer.cs` — a `static` class with
a `Decompose(Directive)` method returning a result union (`Resolved` | `Insufficient`),
never throwing for bad input and never observing the world.
**Rationale:** Structurally identical to `IntentCompiler.Compile` (stateless, caller-side,
deterministic, world-free, no scenario strings). This is the discipline the frozen
boundary already mandates ("caller-side compilation; Runtime never decodes NL"). A
stateless static class adds no state owner and no decision authority.
**Alternatives considered:** an instance decomposer injected into `Agent` (rejected — it
would imply the Agent owns/delegates decomposition, blurring the caller/Runtime
boundary); an interface `IDirectiveDecomposer` (rejected — YAGNI, no second
implementation and no test-seam need since it is a pure function).

### Decision 3: Decomposition outputs feed the existing seam, not a new engine path
**Choice:** The new additive execution entry calls
`IntentExecution.RunOpenWorldAsync(agent, envelope, runId, ct)` with an
`IntentSemanticEnvelope.Resolved` wrapping `OpenWorldTypeLevel(decomposedSpec)` and the
decomposed `Goal`. No new `Agent` method; `Agent.RunOpenWorldAsync` is untouched.
**Rationale:** The seam already validates `ExhaustiveWithinScope` + scope/entry match and
forwards primitives to the proven DFS engine. Reusing it means ~100% of the execution
capability is reused and the regression risk is confined to the new front-end.
**Alternatives considered:** calling `Agent.RunOpenWorldAsync` directly from the new
entry (rejected — it would bypass the seam's validation and duplicate the
envelope-destructure logic); adding a new `Agent.RunDirectiveAsync` (rejected — new
public Agent surface = new architecture-meaningful interface, avoidable).

### Decision 4: Strategy rules are carried on the `Directive`, not re-derived
**Choice:** The `Directive` carries the caller-injected `Goal` evaluator delegates
(candidate authorization, branch inventory, viewport exploration, category classifier)
and the optional `TypeLevelDispatchPolicy`. The decomposer projects them onto the `Goal`
1:1; it derives only the `TypeLevelTraversalSpecification` shape from the directive's
boundary fields.
**Rationale:** Keeps the authority boundary intact: the caller supplies strategy
*knowledge*, the RuntimeAgent owns execution *authority*. The decomposer never invents a
rule, so authorization cannot be widened (spec: Authorization-boundary preservation).
**Alternatives considered:** the decomposer constructing default evaluators (rejected —
invents scenario knowledge, violates I-12 and the no-hardcoded-scenario rule).

## Risks / Trade-offs

- **[Risk] Decomposer silently widens authorization** → Mitigation: the decomposer is a
  1:1 projection of caller-injected rules; a dedicated test asserts the rejected
  candidate stays rejected and no synthesized rule appears (spec scenario). The
  decomposer has no path to construct an evaluator the caller did not supply.
- **[Risk] New model drifts toward a God Context (I-13)** → Mitigation: `Directive` is a
  narrow sealed record with only boundary + rule-set fields; it carries no
  Observation/WorldBelief/RuntimeState/Memory. It is consumed once and projected; it is
  not threaded through the loop.
- **[Risk] Duplication between `Directive` and `TypeLevelTraversalSpecification`** →
  Mitigation: the decomposer is the single projection; `TypeLevelTraversalSpecification`
  remains the execution-input shape consumed by the seam. `Directive` adds the
  strategy-rule set that the spec/`Goal` split currently requires manual assembly for.
  If the two converge completely later, a consolidation decision can supersede this —
  not now (YAGNI).
- **[Risk] Regression to the proven DFS engine** → Mitigation: the engine is untouched;
  the new entry calls the existing seam. The regression guard (SETTINGS-TREE-01,
  SC-U2-MUS-001, SC-OW-TD-001) must stay green; a test enforces this.
- **[Trade-off] No NL → directive compilation in this change** → Acceptable: it preserves
  the caller-side compilation boundary and the closed-world `RunStartRequest` surface.
  NL compilation, if ever needed, is a separate caller-side concern.

## Migration Plan

- Additive only; no removal or rename. No existing API signature changes.
- Deploy: build `src/UniClaw.Runtime.sln`; run `dotnet test`. Existing suites must pass
  unchanged. New deterministic tests cover the new model + decomposer.
- Rollback: delete the two new files and the new entry; the Runtime is byte-for-byte the
  prior state (no shared mutable state, no contract change). `openspec archive --revert`
  not required for an additive, un-consumed-by-production capability.
