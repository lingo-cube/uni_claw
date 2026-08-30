# RuntimeAgent Directive Capability — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME_AGENT_DIRECTIVE_CAPABILITY` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-agent-directive-capability/`
> Authority: Runtime Architecture Contract I-1..I-14 and Architecture v1 remain the governing baselines; decomposition remains a caller-configured, stateless projection and the Runtime gains no scenario knowledge; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** per proposal.md Why — UniClaw RuntimeAgent front-of-pipeline: a production path that deterministically turns an abstract, bounded exploration directive into the existing open-world execution inputs (`TypeLevelTraversalSpecification` + type-directed `Goal` evaluators), so the RuntimeAgent can run evidence-driven exploration without the caller manually constructing those inputs (today 20+ manual test/fixture sites; `RunStartRequest` carries only closed-world `SemanticGoalInput`).

This receipt claims only that:

1. a NEW immutable `Directive` model expresses a bounded exploration intent as declared task scope (application identity + semantic root), entry boundary, maximum semantic depth, safety boundary, completion requirement, and a caller-injected strategy rule set (candidate authorization, branch inventory, viewport exploration, element category classification); it carries no `Plan`, no element coordinates, no `DeviceAction`, no element index, and validates at construction (per proposal.md What Changes and `specs/runtime-agent-directive-decomposition/spec.md` Requirement: Bounded exploration directive representation);
2. a NEW stateless `DirectiveDecomposer` deterministically projects a `Directive` into exactly one `TypeLevelTraversalSpecification` and one type-directed `Goal` evaluator assembly suitable for the existing `IntentExecution.RunOpenWorldAsync` seam; it is world-free, never invents strategy rules, and returns an explicit insufficiency result rather than guessing (per proposal.md What Changes, design.md Decisions 2 and 4, and spec Requirement: Stateless directive decomposition);
3. a NEW bounded execution entry feeds the decomposed inputs through the existing `IntentExecution.RunOpenWorldAsync` → `Agent.RunOpenWorldAsync` seam; `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/`, Architecture v1 invariants, Protocol v1, Contract I-1..I-14, the charter, and `RunStartRequest` are unchanged (per proposal.md What Changes and tasks.md 3.2);
4. the decomposed `Goal`'s candidate-authorization evaluator is exactly the caller-injected rule — no widening, relaxation, or synthesis; the capability adds no new decision authority and no new state owner (per spec Requirements: Authorization-boundary preservation and No authority escalation);
5. deterministic tests cover directive parsing/validation, decomposition shape, authorization-boundary preservation, no-authority-escalation, and regression guarding the existing open-world / SETTINGS-TREE-01 suites (per proposal.md What Changes and tasks.md 4.1-4.5, 5.2).

No claim is made for: natural-language parsing of exploration wording into a `Directive` (wording → directive compilation stays caller-side); wiring `Directive` into the closed-world `RunStartRequest` wire surface (stays `SemanticGoalInput`; the directive path is an additive execution entry, not a replacement); open-world plan revision / recovery integration (mission Phase 4); a global planner, static navigation graph, universal UI knowledge, hardcoded Settings tree, or LLM inside the traversal loop; or any change to the downstream open-world execution capabilities (u2-open-world-settings-traversal, bounded-cross-page-discovery, open-world-traversal-identity-safety, bounded-candidate-safety) — per proposal.md (What Changes, Modified Capabilities: none) and design.md Non-Goals.

## 2. Validation evidence

- tasks.md records all 19 tasks complete ([x] 1.1-1.3, 2.1-2.4, 3.1-3.2, 4.1-4.5, 5.1-5.3, 6.1-6.2).
- tasks.md 5.1 records `dotnet build src/UniClaw.Runtime.sln` — 0 errors, 0 warnings.
- tasks.md 5.2 records `dotnet test src/UniClaw.Runtime.sln` — all existing suites green, including the SETTINGS-TREE-01 capstone (TREE-1..TREE-20), SC-U2-MUS-001, SC-OW-TD-001, bounded candidate safety, and cross-page discovery.
- tasks.md 5.3 records ArchitectureGuardTests pass (Guard 1: zero ProjectReference; Guard 2: no legacy namespace) and `scripts/check-consistency.sh` passes.
- tasks.md 6.1 records `openspec validate runtime-agent-directive-capability --strict` passes.
- tasks.md 3.2 records a diff review confirming `Agent.OpenWorld.cs`, `Agent.cs`, `Container/`, `Traversal/`, `Recovery/`, `World/` are byte-unchanged.
- Named deterministic test classes recorded in tasks.md 4.1-4.5: `DirectiveTests` (construction exposes only task-level declarations; rejects empty safety and negative depth; no Plan/coordinates/DeviceAction), `DirectiveDecomposerTests` (valid directive → spec shape + Goal evaluators match caller rules; determinism; world-free), `DirectiveDecomposerAuthorizationTests` (rejected candidate stays rejected; no synthesized authorization; forbidden category stays forbidden), `DirectiveDecomposerAuthorityTests` (no mutable state, no decision participation; Fake-environment end-to-end proves the existing DFS path executes), `DirectiveDecomposerInsufficientTests` (missing required rule → `Insufficient`, no execution inputs, no fabricated rule).
- design.md documents the additive, contract-preserving design: immutable `Directive` in `Model/` (Decision 1), stateless static decomposer mirroring the `IntentCompiler` discipline (Decision 2), reuse of the existing seam (Decision 3), caller-carried strategy rules (Decision 4), and an additive-only migration/rollback plan (delete the two new files + the new entry; no shared mutable state, no contract change).
- proposal.md records the baseline verification: 2026-08-21 on branch `uni-agent`, build 0 errors / 0 warnings.

The change's files record these build/test/guard results as completed task checkboxes in tasks.md; there is no `evidence/` directory with independent probe/build logs, so verification evidence is limited to the task records and design.md cited above.

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (there is no `evidence/` directory); design.md records Risks / Trade-offs with planned mitigations rather than executed falsifier results: decomposer silently widens authorization (mitigated by 1:1 projection + dedicated authorization test), `Directive` drifts toward God-Context I-13 (mitigated by a narrow sealed record carrying no Observation/WorldBelief/RuntimeState/Memory), duplication between `Directive` and `TypeLevelTraversalSpecification` (mitigated by single projection), and regression to the proven DFS engine (mitigated by the untouched engine + regression guard test that SETTINGS-TREE-01, SC-U2-MUS-001, SC-OW-TD-001 stay green). Rejection/negative requirements are defined in `specs/runtime-agent-directive-decomposition/spec.md`:

- "The `Directive` MUST NOT carry a `Plan`, element coordinates, a `DeviceAction`, a `TraversalStep`, an element index, or any precompiled physical step." (Requirement: Bounded exploration directive representation)
- "The decomposer MUST NOT observe the world, MUST NOT select a UI target, MUST NOT construct a concrete route, and MUST NOT invent strategy rules beyond those the caller injected on the `Directive`." (Requirement: Stateless directive decomposition)
- "the decomposer MUST NOT widen, relax, or synthesize authorization", and "no decomposition output authorizes an interaction the caller's safety boundary forbids" (Requirement: Authorization-boundary preservation, scenarios: rejected candidate stays rejected, decomposer grants no authority)
- "The decomposition MUST NOT create a new decision authority or a new state owner", and the capability "introduces no new state owner, no global planner, no static navigation graph, and no LLM inside the traversal loop" (Requirement: No authority escalation, scenarios: runtime agent keeps sole execution authority, no new architecture component)

## 4. Deferred scope

The following remain outside this graduation and require separate authorization:

- Natural-language parsing of exploration wording ("Explore Settings safely") into a `Directive` — wording → directive compilation stays caller-side (design.md Non-Goals).
- Wiring `Directive` into the closed-world `RunStartRequest` wire surface — the production wire stays `SemanticGoalInput` (design.md Non-Goals).
- Open-world plan revision / recovery integration (mission Phase 4) — `RunOpenWorldAsync` fail-closed behavior unchanged here; tracked separately if a real buyer appears (design.md Non-Goals; proposal.md Risk).
- Global planner / static navigation graph / universal UI knowledge / hardcoded Settings tree / LLM inside the traversal loop — explicitly excluded by the mission and frozen invariants (design.md Non-Goals).
- Modified capabilities: none — the downstream open-world execution capabilities are unchanged by this change (proposal.md Modified Capabilities).

## 5. Final conclusion

**GRADUATED.** The bounded claim — an additive immutable `Directive` model plus a stateless, caller-configured decomposition feeding the existing open-world execution seam, with no authority/state/contract change — is supported by the records in tasks.md (all 19 tasks complete, including build 0 errors / 0 warnings, full-suite regression, architecture guards, and strict OpenSpec validation) and by design.md's additive design decisions. Archival is performed on 2026-08-30 as a separate lifecycle operation in this batch.
