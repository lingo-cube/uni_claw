# Proposal: runtime-agent-directive-capability

> Change ID: `runtime-agent-directive-capability`
> Status: Proposed
> Type: Capability extension (additive, no contract/invariant change)
> Baseline verified: 2026-08-21, branch `uni-agent`, build 0 errors / 0 warnings
> Authority decision: caller-configured decomposition (preserves frozen v1 / Contract
> I-1..I-14 / charter authority boundaries; Runtime gains no scenario knowledge).

## Why

The RuntimeAgent already owns a proven, evidence-driven DFS execution engine
(`Agent.RunOpenWorldAsync`) that realizes the mission's back-half pipeline —
discover candidates → evaluate authorization → expand authorized branches → execute →
verify parent return → update WorldBelief → stop on GoalEvidence — and it is heavily
validated (SC-U2-MUS-001, SC-OW-TD-001, SETTINGS-TREE-01 TREE-1..TREE-20, bounded
candidate safety, external boundary, etc.).

What is **missing** is the **front of the pipeline**: there is no production path that
turns an abstract, bounded exploration directive ("Explore Settings safely") into the
open-world execution inputs (`TypeLevelTraversalSpecification` + type-directed `Goal`
evaluators). Today `IntentCompiler` compiles wording only into closed-world
`SemanticGoalInput` (enable/disable X); every `TypeLevelTraversalSpecification` is
manually constructed by a test/fixture (20+ sites). The production wire surface
(`RunStartRequest`) carries only `SemanticGoalInput` and explicitly forbids a Plan or
precompiled steps. So the "Directive → Goal Decomposition → Runtime-local Plan
Hypothesis" segment is unimplemented for exploration directives, even though the
downstream execution it would feed already exists and is verified.

## What Changes

- **NEW** immutable `Directive` model expressing a bounded exploration intent: declared
  scope (application + semantic root), safety boundary, completion requirement, and a
  caller-injected strategy rule set (candidate authorization, branch inventory,
  viewport exploration, category classification). It carries no Plan, no coordinates,
  no `DeviceAction`, and no natural-language execution detail.
- **NEW** stateless `DirectiveDecomposer` — a deterministic, caller-configured
  transform from a `Directive` into a `TypeLevelTraversalSpecification` plus a `Goal`
  evaluator assembly. It mirrors the existing `IntentCompiler` discipline: no world
  observation, no scenario strings, no decision authority. It only projects
  already-authoritative caller input into the existing execution-input shapes.
- **NEW** bounded execution entry that feeds the decomposed inputs to the existing
  `IntentExecution.RunOpenWorldAsync` → `Agent.RunOpenWorldAsync` seam. The DFS engine,
  `Agent`, `Container`, `Traversal`, `Recovery`, and all contracts are **unchanged**.
- **NEW** deterministic tests: directive parsing/validation, decomposition → spec
  shape, authorization-boundary preservation, no-authority-escalation, and regression
  guard that existing SETTINGS-TREE-01 / open-world suites remain green.
- **NOT changed**: Architecture v1 invariants, Protocol v1, Contract I-1..I-14, charter,
  `RunStartRequest` (closed-world wire surface stays as-is), the DFS engine, or any
  frozen decision. No new state owner, no new decision authority, no new component with
  architecture meaning beyond the additive model + stateless transform.

## Capabilities

### New Capabilities
- `runtime-agent-directive-decomposition`: bounded exploration `Directive` model +
  stateless, caller-configured `DirectiveDecomposer` that projects a directive into the
  existing `TypeLevelTraversalSpecification` + type-directed `Goal` evaluator assembly,
  feeding the existing open-world DFS execution seam. Owns decomposition shape only;
  owns no world state and no decision authority.

### Modified Capabilities
<!-- None. The downstream open-world execution capabilities
(u2-open-world-settings-traversal, bounded-cross-page-discovery,
open-world-traversal-identity-safety, bounded-candidate-safety) are unchanged; this
change only adds an upstream entry that produces their existing inputs. -->

## Impact

- **Code**: NEW `src/UniClaw.Runtime/Model/Directive.cs`; NEW
  `src/UniClaw.Runtime/Planning/DirectiveDecomposer.cs`; small additive entry in
  `src/UniClaw.Runtime/Planning/IntentExecution.cs` (or a new sibling seam) for the
  exploration-directive path. `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`,
  `Traversal/`, `Recovery/`, `World/` — **unchanged**.
- **APIs**: additive only. The closed-world `RunStartRequest` /
  `Agent.RunSemanticGoalAsync` surface is untouched. The open-world
  `Agent.RunOpenWorldAsync` signature is untouched; the new entry calls it.
- **Dependencies**: none new. Stays inside `UniClaw.Runtime` (ArchitectureGuardTests
  Guard 1: zero ProjectReference; Guard 2: no legacy namespace). Depends only on
  `Model/` immutable types already in use.
- **Authority**: NONE. The RuntimeAgent keeps sole run-level semantic/execution
  authority; strategy *rules* remain caller-injected (same boundary as `IntentCompiler`
  and the existing open-world `Goal` evaluators). The Runtime gains no scenario
  knowledge and no new authority ownership — verified against v1 invariants 2-4 and
  Contract I-12.
- **Tests**: NEW deterministic tests under `tests/UniClaw.Runtime.Tests/`; existing
  open-world / SETTINGS-TREE-01 suites must remain green (regression guard).
- **Risk**: Low for the decomposition front-end (additive model + stateless transform,
  mechanically guarded). The open-world plan-revision/recovery integration (Phase 4) is
  out of scope for this change and tracked separately if a real buyer appears.
