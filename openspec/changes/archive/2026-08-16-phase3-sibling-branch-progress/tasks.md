# Tasks — phase3-sibling-branch-progress

> 实施前必读: `proposal.md` + `design.md` + `specs/sibling-branch-progress/spec.md` + `scenarios/SC-P3-CAND-004-sibling-branch-progress.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> SC-P3-CAND-004 已批准为 `SEMANTIC_PURCHASE_REQUIRED`；Production Delta Budget = exactly one immutable `BranchProgressEvidence` type + three immutable value fields + one Agent-owned state field；enums/interfaces/components/new mutable-state owners = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现需要扩大该预算、改变 frozen ownership/authority/dependency boundary、引入新 navigation/recovery/safety abstraction，或让 fixed Plan 成为 world truth，立即停止并返回对应 Semantic/Architecture Gate。

## Dependency Order

```text
1.1 Deterministic Hierarchical World Fixture
→ 2.1 Branch Progress Model and Agent Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Deterministic Hierarchical Scenario Capability

- [x] 1.1 **Script parent, sibling, local-completion, return, and replay evidence**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-P3-CAND-004
  - **Goal:** 建立纯测试侧确定性 world/fixture，使现有 Fake/Harness 能表达 P 的完整 approved sibling inventory A/B、A/B 各自 local work、existing Tap parent-return、A 完成而 B 未访问、A revisit、stale parent evidence、wrong-parent/identity conflict，以及相同输入重放。
  - **Required Semantic:** Fake 只拥有外部页面、可见元素、action outcome、world transition 与 Observation；它不得宣称 branch complete、parent complete、progress owner、Goal success 或 Agent 应如何解释。Plan 是批准边界/hypothesis，不是 sibling existence/completion truth。
  - **Approved Production Purchase:** Production Changes = NONE；Production Model Delta = 0。本任务只购买 deterministic proof capability。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/Fakes/**`、`ScriptedEnvironmentTests.cs`、SC-P3-CAND-004 专用 fixture/helper/tests。允许最小扩展现有 ScriptedEnvironment data variant，但不得改变既有 variant 行为。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`；OpenSpec semantic；生产 type/field/enum/interface/component/state；Runtime branch-progress behavior；新 test framework；提前实现 Task 2.1；Capstone、Recovery resume 或 autonomous safety。
  - **Required Assertions:** fresh P Observation 可见 A/B；Tap A/B 分别进入可区分 child semantic page；每个 child local-work action 与 parent-return action 可独立 deterministic replay；A local work 后返回 P 时 B 仍未发生任何 world transition；early return fixture 不发生 child local-work effect；A revisit 可重放但不会由 Fake 输出“new progress”；stale P fixture 不推进 sequence；wrong-parent fixture 返回 fresh 可区分 semantic page；ActionHistory/Observation/world state 在相同配置与动作序列下相等；现有 variants 不回归。
  - **Verification:** fixture/ScriptedEnvironment 定向 tests；existing `ScriptedEnvironmentTests`；`dotnet build src/UniClaw.Runtime.sln`；确认 `src/UniClaw.Runtime/**` 零本任务改动。
  - **Deferred Boundary:** BranchProgressEvidence、Agent state/update behavior、subtree completion、formal end-to-end proof、graph/stack/tree/hierarchy/visited-set semantic、new Back、manager/FSM、Recovery resume、autonomous discovered-candidate safety、Capstone。
  - **Return Contract:** `TASK_RESULT`；成功时 `Status: DONE` 且 `Production Delta: NONE`。完成后停止并由 H4-3 重读 repository truth。

## 2. Branch Progress Model and Agent Behavior

- [x] 2.1 **Add the approved progress evidence value and enforce honest sibling completion**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-004
  - **Goal:** 在严格批准预算内增加 immutable `BranchProgressEvidence` 与唯一 Agent-owned state field，并在现有 Agent/Container/Traversal 协作中实现：fresh P inventory → A local proof → return P preserves A/B pending → B local proof → return P → bounded subtree evidence complete；revisit/stale/conflict 不伪造进度。
  - **Required Semantic:** Agent owns cross-Container progress and high-level completion；Container owns page-local completion and must already be locally complete before return；Traversal journal/action dispatch/Observation are mechanics/evidence only；Plan restricts approved actions but does not prove branches exist or complete；Goal completion remains Agent + GoalEvidence only。
  - **Approved Production Purchase:** exactly one immutable production model type with exactly three semantic fields (parent identity, approved sibling-inventory evidence, proven sibling-completion evidence) + exactly one Agent-owned state field；new enums/interfaces/components/mutable-state owners = 0；Ownership/Authority Delta = NONE。
  - **Allowed Scope:** one new file under `src/UniClaw.Runtime/Model/` for the approved value；`src/UniClaw.Runtime/Agent/Agent.cs` minimum field/private helpers/control flow；direct unit/Scenario tests under `tests/`；必要时只更新本 tasks.md progress。可以只读使用现有 Container local-completion/identity and Traversal journal surfaces，不得改变其 ownership。
  - **Forbidden Scope:** 修改 `Container`/`Traversal`/`Environment`/`Recovery` production contracts unless an explicit existing-behavior implementation defect blocks the approved path；four-field budget expansion；new Plan/Goal/Observation/GoalEvidence/Trace field；graph/stack/tree/hierarchy/visited-set semantic type；TraversalContext/ResumeToken/manager/FSM/workflow engine；new Back action；Recovery-progress resume；autonomous safety/discovery；Runtime refactor。
  - **Required Assertions:** type is immutable and validates nonblank parent/branch identities, nonnegative source sequences, and completed ⊆ approved；Agent exposes immutable progress snapshots without sharing mutable state；inventory accepted only from fresh P evidence and bounded approved candidates actually present；A completion recorded only if A was locally complete before return and fresh post-return evidence reconciles to P；early return does not complete A；A completion survives P → B；A revisit is idempotent；stale/wrong-parent evidence cannot replace or mutate P progress；P/subtree incomplete with B pending and derived complete only after A/B proof；no child/local/subtree fact directly sets Completed；existing SC-P1/P2/P3-001/002/003 behavior unchanged。
  - **Verification:** targeted Model/Agent tests plus Task 1.1 fixture integration；full `dotnet build` + `dotnet test`；8 Architecture Guards；`scripts/check-consistency.sh`；exact production-delta and ownership/authority audit。
  - **Deferred Boundary:** no generalized autonomous traversal/safety, graph/navigation framework, Recovery resume/invalidation, Capstone, real device/Vision, new identity algorithm, new component/interface/enum/owner, or structural refactor。
  - **Return Contract:** `TASK_RESULT`；若 exact budget/ownership/authority 无法满足，返回正式 `BLOCKED_FOR_SEMANTIC_REVIEW` 或 `BLOCKED_FOR_ARCHITECTURE_REVIEW`。完成后停止并由 H4-3 重读 repository truth。

## 3. SC-P3-CAND-004 Formal Scenario Verification

- [x] 3.1 **Prove sibling progress, honest completion, negative branches, and replay**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-004
  - **Goal:** 使用 Task 1.1 deterministic world 与 Task 2.1 production behavior 建立正式 end-to-end SC-P3-CAND-004 evidence，覆盖 positive、A-only incomplete、early return、revisit、stale/conflict 与 replay，不新增生产行为。
  - **Required Semantic:** current child complete != parent/subtree complete；returned parent != all siblings processed；revisit != new progress；some proven branches != all approved siblings；Container local progress != Agent cross-Container progress；Goal completion remains separate。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买 formal Scenario evidence。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` 的 SC-P3-CAND-004 formal tests、Task 1.1 fixture、ScenarioHarness/Goals/Plans 最小测试侧组合，以及本 tasks.md Task 3.1 progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`，除非发现明确 implementation regression；修改 approved semantic/budget；新 production artifact/state；Runtime refactor；Recovery resume、autonomous safety、Capstone 或其他 Scenario。
  - **Required Assertions:** positive trace proves fresh P inventory A/B, A local proof before return, A complete/B pending after first return, preserved A while B executes, A/B complete only after second proof, existing Tap returns, and final Goal only from satisfied GoalEvidence；negative branches prove A-only cannot complete P/Goal, early return leaves A incomplete, A revisit does not increase distinct completion, stale/wrong-parent evidence leaves valid progress unchanged, conflicting identity attaches no progress, and child local completion alone cannot complete Run；same RunId/world/Plan/action replay yields equal progress snapshots/ActionHistory/Observation/journal/Trace/GoalEvidence/final state。
  - **Verification:** SC-P3-CAND-004 targeted/replay tests；all SC-P1/P2/P3-001/002/003 regression；full build/test；Architecture Guards；consistency；Evidence Required 1–8 audit。
  - **Deferred Boundary:** no production change, recovery-progress validity, autonomous branch/safety semantics, Capstone, graph/stack/tree/manager/FSM, new Back, new Recovery semantic, or refactor。
  - **Return Contract:** `TASK_RESULT`；完成后停止并由 H4-3 重读 repository truth。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-CAND-004 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-CAND-004
  - **Goal:** fresh reload repository truth，独立审计 Task 1.1/2.1/3.1 实际 diff、formal evidence、exact production budget、ownership/authority、deferred boundary、deterministic replay 与全部既有回归。
  - **Required Semantic:** Agent-owned cross-Container evidence distinguishes inventory/completion；Container remains page-local；Traversal/Environment/Recovery do not gain semantic authority；parent completion derives only from full evidence coverage；Goal completion remains Agent/GoalEvidence。
  - **Approved Production Purchase:** exactly one immutable `BranchProgressEvidence` type + three immutable value fields + one Agent state field；enums/interfaces/components/new mutable-state owners = 0；Ownership/Authority Delta = NONE；validation behavior delta = 0。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；run build/tests/guards/consistency/strict OpenSpec validation；全部 PASS 后仅勾选 Task 4.1。
  - **Forbidden Scope:** 修补 production/tests/spec；接受 coder summary 代替 fresh evidence；新增 artifact/state/dependency；改变 frozen architecture；开始 Recovery research、Capstone、其他 Scenario 或 Phase completion。
  - **Required Assertions:** Evidence Required 1–8 全部满足；exact 1 type/4 fields and no other production delta；positive and all negative branches correct；Agent/Container/Traversal/Environment/Recovery ownership unchanged；no false/duplicate completion；deterministic replay pass；Phase 1/2 and SC-P3-001/002/003 pass；no graph/stack/tree/hierarchy/visited-set semantic、manager、FSM、new Back、Recovery resume、autonomous safety/discovery、Capstone or refactor leakage。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-sibling-branch-progress --strict`；production-delta/ownership/authority/determinism/evidence audit。
  - **Deferred Boundary:** all capabilities outside bounded sibling progress remain absent；structural pressure may be recorded but not repaired。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 may create Scenario-specific closeout and then stop frozen。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Model/` | `docs/system/constitution/runtime-architecture-contract.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `src/UniClaw.Runtime/Container/` | `docs/system/layers/container-runtime.md` |
| `src/UniClaw.Runtime/Traversal/` | `docs/system/layers/traversal-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `docs/system/scenarios/s0-roadmap-coverage.md` |
| `openspec/changes/phase3-sibling-branch-progress/` | `openspec/changes/phase3-sibling-branch-progress/design.md` |
