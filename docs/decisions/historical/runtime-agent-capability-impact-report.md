# RuntimeAgent Architecture Impact Report

> Phase 0 deliverable for the "RuntimeAgent Capability Autonomous Development Loop" mission.
> Authority: READ-ONLY architecture inspection. This is a working artifact, NOT an
> Architecture Decision, OpenSpec spec, or contract amendment. Frozen baselines
> (Architecture v1, Protocol v1, Contract I-1..I-14, charter) govern and are not
> redefined here.
> Date: 2026-08-21 | Inspector: Leader (GLM-5.2) | Baseline build: 0 errors / 0 warnings

---

## Current Architecture

The RuntimeAgent closed loop is **already implemented** for both closed-world and
open-world execution:

```
Closed-world (production wire surface):
  RunStartRequest(SemanticGoalInput, Objects, Capabilities, Device)
    → Agent.RunSemanticGoalAsync
    → READ belief → DECIDE → ACT → OBSERVE → UPDATE → RE-EVALUATE
    → GoalEvidence completion (I-10), assistance consult, recovery integrated

Open-world (type-directed DFS — Agent.OpenWorld.cs):
  TypeLevelTraversalSpecification (scope/depth/safety/completion=ExhaustiveWithinScope/
    entry/dispatchPolicy) + Goal(BranchInventoryEvaluator, CandidateAuthorizationEvaluator,
    ViewportExplorationEvaluator, CategoryClassifier)
    → IntentExecution.RunOpenWorldAsync
    → Agent.RunOpenWorldAsync
    → DFS loop: discover candidates (viewport exploration + source normalization +
      inventory completeness) → evaluate authorization (3-way) → expand authorized
      branches → execute (type-directed dispatch) → verify parent return → update
      WorldBelief → stop on GoalEvidence completion criteria
    + External Boundary disposition (EBD) + bounded source revisit + identity safety
```

Key existing pieces (all in `src/UniClaw.Runtime/`):
- `Planning/IntentCompiler.cs` — stateless caller-side compilation of business wording →
  closed-world `SemanticGoalInput`. No world observation, no scenario strings.
- `Planning/IntentSemanticEnvelope.cs` — immutable projection; `OpenWorldTypeLevel`
  variant carries a `TypeLevelTraversalSpecification`.
- `Planning/IntentExecution.cs` — bounded execution seam: forwards already-resolved
  envelopes to `Agent.RunSemanticGoalAsync` / `Agent.RunOpenWorldAsync`.
- `Planning/TypeLevelTraversalSpecification.cs` — the bounded open-world directive
  representation (scope, target categories, max depth, safety boundary, completion,
  entry boundary, dispatch policy).
- `Agent/Agent.OpenWorld.cs:37-706` — the DFS exploration engine.
- `Agent/Agent.SemanticRun.cs` — closed-world semantic loop + recovery/assistance.
- `Model/Plan.cs`, `Model/Goal.cs`, `Model/SemanticGoalInput.cs` — revisable hypothesis
  model (Plan = hypothesis per I-5); Goal carries caller-injected evaluators.

Authority boundary (frozen, established):
- **Caller (UniAgent / composition)** supplies the directive representation AND the
  strategy evaluators (what counts as a candidate, what is authorized, what categories
  exist, what completion means). Compilation is caller-side; the Runtime never decodes
  natural language and never hardcodes scenario strings (裁决 3/11; Agent docstring).
- **RuntimeAgent** owns world-state, grounding, action authorization, execution,
  fresh verification, bounded recovery, Run-terminal outcome (v1 invariants 2-4; I-3).

---

## Missing Capability (evidence-verified)

The mission's pipeline
`Directive → Goal Decomposition → Runtime-local Plan Hypothesis → Authorization
Expansion → Evidence-driven Execution → Verification → Plan Revision/Recovery →
Run Outcome` is **already realized for steps "Authorization Expansion … Verification"**
by `Agent.RunOpenWorldAsync` + its Goal evaluators, and heavily proven by:
- `U2OpenWorldExecutionTests`, `U2OpenWorldSettingsFormalScenarioTests` (SC-U2-MUS-001)
- `OpenWorldTypeDirectedScenarioTests` (SC-OW-TD-001)
- `SettingsTreeCapstoneTests` (TREE-1..TREE-20; real-device Phase5 = SETTINGS-TREE-01)
- `BoundedCandidateSafety*`, `BoundedCrossPageDiscovery*`, `ExternalBoundaryTests`, etc.

The **genuine gap** is the **front of the pipeline** — there is no production path that
turns an abstract bounded exploration directive into the open-world execution inputs:

1. **No `Directive` type.** v1 defines Directive as a concept ("Directive carries a
   bounded Runtime goal; currently `SemanticGoalInput` belongs to this layer") but it
   is realized only as `SemanticGoalInput` (closed-world) and caller-constructed
   `TypeLevelTraversalSpecification` (open-world). The production wire surface
   (`RunStartRequest`) carries only `SemanticGoalInput` and *explicitly forbids* a Plan,
   coordinates, or precompiled steps.
2. **No exploration-directive compilation.** `IntentCompiler` compiles wording only into
   closed-world `SemanticGoalInput` (enable/disable X). It has **zero** references to
   `TypeLevelTraversalSpecification` or `OpenWorldTypeLevel`. Every
   `TypeLevelTraversalSpecification` instance in the repo is **manually constructed by a
   test/fixture** (20+ sites). No code path compiles "Explore Settings safely" into a
   spec + type-directed Goal.
3. **Plan revision/recovery NOT integrated into the open-world path.**
   `Agent.RunOpenWorldAsync` never calls the Recovery subsystem (only reads
   `_recoveryAnchor`); failures fail closed. Phase 4 (Run-local revision) is partially a
   gap, although the closed-world semantic run DOES integrate recovery/assistance.

---

## Minimal Extension Point

The minimal, invariant-preserving extension is a **deterministic, caller-configured
decomposition** — structurally identical to the existing `IntentCompiler` pattern
(stateless, caller-side, no world observation, no scenario strings):

```
Bounded Exploration Directive (new immutable model)
    ↓  DirectiveDecomposer (new stateless transform, caller-configured rules)
TypeLevelTraversalSpecification   (existing)
  + Goal evaluator factory         (caller-injected knowledge, existing evaluator shape)
    ↓  IntentExecution.RunOpenWorldAsync (existing seam)
Agent.RunOpenWorldAsync            (existing DFS engine, UNCHANGED)
```

This reuses ~100% of the existing DFS execution and authority. The "RuntimeAgent
generates a local strategy" is realized as: RuntimeAgent **accepts** a bounded directive
and **expands** it into a Plan Hypothesis via caller-injected strategy rules, then
executes via the existing loop. Strategy *generation* is a deterministic transform; the
Runtime gains **no** scenario knowledge and **no** new decision authority.

Files affected (minimal):
- NEW `Model/Directive.cs` (or `Planning/`) — immutable bounded directive representation.
- NEW `Planning/DirectiveDecomposer.cs` — stateless directive → (spec + evaluator factory).
- NEW deterministic tests — directive parsing, decomposition → spec, authorization
  boundary preservation, no authority escalation.
- POSSIBLE small extension to `IntentExecution` for the exploration-directive entry.
- UNCHANGED: `Agent.OpenWorld.cs`, `Agent.cs`, Contract, Traversal, Container.

---

## Authority Impact

**NONE — *if* the decomposition stays caller-side/stateless and the Runtime gains no
scenario knowledge**, mirroring `IntentCompiler`. The Agent remains the sole run-level
semantic/execution authority; the DFS engine is unchanged; no new state owner.

**REQUIRED REVIEW — *if* "RuntimeAgent generates the strategy internally"** is read
literally (RuntimeAgent owns strategy generation = scenario knowledge = new authority).
That reading conflicts with:
- v1 invariants 2-4 (RuntimeAgent owns execution/grounding/verification, not scenario
  knowledge generation).
- Contract I-12 (no complexity without a real buyer) + the Agent's own "不硬编码场景
  字符串" rule (裁决 3/11).
- The established "caller-side compilation; Runtime never decodes NL" stance
  (docs/decisions evidence).
- `.ai/model-routing.yaml`: "new interface with architecture meaning" → expert
  escalation; "new authority ownership" → Human Gate.

This is the decision point that gates Phases 1-4.

---

## Architecture Risk

- **Low** for the caller-configured deterministic decomposition (Option A): it is a
  new model + a stateless transform, mechanically guarded by ArchitectureGuardTests
  (zero ProjectReference, no legacy namespace) and the consistency checker. It extends
  contracts, does not replace them.
- **High** if the mission-literal "RuntimeAgent internally generates the strategy" is
  pursued: it would require the Runtime to own scenario/strategy generation authority,
  breaking the frozen caller/Runtime authority boundary and I-12 — an architecture
  redesign, which the mission explicitly forbids ("Do not redesign existing
  architecture").
- **Medium** for Phase 4 (open-world plan revision/recovery integration): wiring the
  existing `Recovery/` subsystem into `RunOpenWorldAsync`'s fail-closed paths is a
  behavior change to a proven loop and must be scenario-validated, but it does not
  change authority (Recovery mechanism stays in `Recovery/`; decision stays in Agent).
