# Tasks — phase3-scroll-identity-continuity

> 实施前必读: `proposal.md` + `design.md` + `specs/viewport-identity-continuity/spec.md` + `scenarios/SC-P3-003-viewport-identity-continuity.md`。
> 完成一项立即勾选 `- [x]`。一次只执行一个 Task ID；H4-3 每项完成后重新读取 repository truth 再决定下一项。
> SC-P3-003 已批准为 `SEMANTIC_PURCHASE_REQUIRED`；Production Model Delta Budget = exactly one immutable bounded-forward-viewport `DeviceAction` variant；fields/enums/interfaces/components/mutable state = 0；Ownership Delta = NONE；Authority Delta = NONE。
> 任一执行者若发现需要超出该 action variant 的 production model、需要改变 frozen ownership/authority/dependency boundary，或需要 Runtime refactor，立即停止并返回对应 Semantic/Architecture Gate。

## Dependency Order

```text
1.1 Viewport Action and Deterministic Fixture
→ 2.1 Runtime Continuity Behavior
→ 3.1 Formal Scenario Proof
→ 4.1 Independent Validation
```

## 1. Minimum Action Semantic and Deterministic Capability

- [x] 1.1 **Add the approved viewport action and deterministic Scenario fixture**
  - **Task ID:** 1.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** NONE
  - **Scenario Receipt:** SC-P3-003
  - **Goal:** 增加已批准的唯一 immutable bounded-forward-viewport `DeviceAction` variant，并扩展现有 ScriptedEnvironment/Fake，使其确定性表达：一次 targetless viewport action；A/B/C → D/E/F 同 Container 分支；stale evidence 分支；semantic-page-changed/identity-conflict 分支；相同输入确定性重放。
  - **Required Semantic:** viewport action 是一次有界 forward local movement；它不是 Tap、不是 semantic navigation、不是 Container identity、也不由 dispatch outcome 证明世界进展。Observation element-set change 是 snapshot evidence，不是 identity truth。
  - **Approved Production Purchase:** exactly one immutable `DeviceAction` variant；new fields/enums/interfaces/components/mutable state = 0；Production Behavior Change = NONE beyond the action value itself.
  - **Allowed Scope:** `src/UniClaw.Runtime/Model/Actions/DeviceAction.cs`；`tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironment.cs`、`ScriptedEnvironmentVariants.cs` 及 SC-P3-003 专用最小 fixture/helper/tests。只允许为新 variant 更新测试 Fake 的 action switch/description/effect scripting。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Agent/**`、`Container/**`、`Traversal/**`、`Recovery/**`；production Observation/Plan/Trap/Result 改动；除批准 variant 外的新 type/field/enum/interface/component/state；真实设备、gesture geometry 或 Runtime behavior；提前实现 Task 2.1。
  - **Required Assertions:** DeviceAction union 恰好新增一个无字段 immutable variant；Fake 可记录一次 targetless viewport action并独立配置 dispatch outcome 与 world transition；positive fixture 返回 strict-new Observation 且 visible elements 从 A/B/C 改为 D/E/F、semantic identity evidence 仍相容；stale fixture 不推进 sequence；identity-conflict fixture 返回可区分的新语义页；相同 RunId/config/action replay 到相同 ActionHistory/Observation/fixture evidence；现有 Fake variants 不回归。
  - **Verification:** 新 variant/Fake/fixture 定向测试；现有 `ScriptedEnvironmentTests`；`dotnet build src/UniClaw.Runtime.sln`；审计 production delta 恰好 one variant 且其余为 0。
  - **Deferred Boundary:** Traversal token/dispatch、Container current-observation progression、Agent continuity/escalation、formal Scenario proof、Fingerprint、direction/coordinates/distance/duration、reverse/repeated scroll、progress/end-of-list、ScrollManager/FSM、Runtime refactor。
  - **Return Contract:** `TASK_RESULT`；成功时 `Status: DONE`，完成后停止并由 H4-3 重新读取 repository truth。

## 2. Viewport Continuity Runtime Behavior

- [x] 2.1 **Dispatch one viewport action and preserve verified Container continuity**
  - **Task ID:** 2.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 1.1 DONE
  - **Scenario Receipt:** SC-P3-003
  - **Goal:** 在现有 Traversal/Container/Agent 控制流中实现最小批准行为：targetless viewport token → dispatch once → fresh Observe → existing semantic continuity verification；连续性成立时推进同一 Container.CurrentObservation 且保留 progress，否则产生 Container-scope evidence 并由 Agent处理 higher-scope outcome。
  - **Required Semantic:** Traversal owns deterministic Execute → Observe → Verify mechanics；Container owns local Observation/progress/identity continuity；Agent owns rebind/Recovery/GoalEvidence/final RunState；visible snapshot change 与 action dispatch 均不是 semantic success。
  - **Approved Production Purchase:** Task 1.1 的 one action variant；本任务 Production Model Delta = 0，仅允许 existing-class methods/control flow。Ownership/Authority Delta = NONE。
  - **Allowed Scope:** `src/UniClaw.Runtime/Traversal/Traversal.cs`、`src/UniClaw.Runtime/Container/Container.cs`、`src/UniClaw.Runtime/Agent/Agent.cs` 的最小方法/控制流，以及直接验证这些分支的既有 unit/Scenario tests。
  - **Forbidden Scope:** `src/UniClaw.Runtime/Model/**`（Task 1.1 variant 除外且本任务不得再改）、`Recovery/**`、production Environment interface；新 production type/field/enum/interface/component/mutable state；Runtime refactor/新 pipeline；ScrollManager/viewport component/FSM；generic retry/continuity/recovery framework。
  - **Required Assertions:** Scroll token 不执行 element Select 且 journal `SelectedElementIndex=null`、`DispatchedAction=viewport action`；exactly one dispatch；Rejected → failure without Observe/redispatch；accepted action → fresh Observe；stale sequence → failure without continuity；positive fresh evidence requires compatible foreground + `IsStillMine` + same reconciled semantic page，updates CurrentObservation without Bind/replacement and preserves prior ExecutedSteps；identity-conflict/new-page branch emits Container-scope Trap evidence, preserves original progress, and Agent alone rebinds/fails/recovers；no Goal completion from movement/continuity；SC-P2-002、SC-P3-001、SC-P3-002 regressions unchanged。
  - **Verification:** targeted Traversal/Container/Agent tests；SC-P3-001/002 regressions；`dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guards；`scripts/check-consistency.sh`；production delta audit。
  - **Deferred Boundary:** no reverse/repeated scroll, progress/end detection, coordinates/geometry, Fingerprint, real-device/Vision, multi-container progress, new Recovery semantics, or structural refactor.
  - **Return Contract:** `TASK_RESULT`；若 existing control flow cannot satisfy without extra semantic/ownership/authority/refactor purchase，返回正式 `BLOCKED_FOR_*`；完成后由 H4-3 重读 repository truth。

## 3. SC-P3-003 Formal Scenario Verification

- [x] 3.1 **Prove viewport identity continuity, escalation, progress preservation, and replay**
  - **Task ID:** 3.1
  - **Role:** runtime-coder
  - **Tier:** standard
  - **Depends On:** 2.1 DONE
  - **Scenario Receipt:** SC-P3-003
  - **Goal:** 使用 Task 1.1 fixture 与 Task 2.1 behavior 建立 SC-P3-003 positive、stale/identity-conflict escalation 和 deterministic replay 正式证明，不新增生产行为。
  - **Required Semantic:** different viewport snapshot != Container change；continuity 只由 fresh external evidence + existing semantic identity rules 证明；progress/Goal/final authority 边界保持。
  - **Approved Production Purchase:** Production Model Delta = 0；Production Behavior Delta = 0。本任务只购买正式 Scenario 测试证据。
  - **Allowed Scope:** `tests/UniClaw.Runtime.Tests/Scenario/**` 的 SC-P3-003 专用 formal tests、Task 1.1 fixture 与必要最小 helper；必要时仅更新本 tasks.md progress。
  - **Forbidden Scope:** `src/UniClaw.Runtime/**`，除非发现明确 implementation regression；OpenSpec semantic 修改；新 production artifact/state；Runtime refactor；其它 Phase 3 candidate。
  - **Required Assertions:** positive branch proves one targetless action, A/B/C → fresh D/E/F, same active Container, CurrentObservation progression, preserved pre-movement progress, continued execution, no rebind/Recovery, and Goal completion only from satisfied GoalEvidence；stale branch proves no fabricated progress/continuity/redispatch；identity-conflict branch proves Container-scope evidence, original progress preservation, and Agent higher authority；equal RunId/Environment/action replays ActionHistory/Observation/journal/Trace/identity evidence/progress/GoalEvidence/final state。
  - **Verification:** SC-P3-003 targeted tests/replay；SC-P1, SC-P2, SC-P3-001/002 regression；full build/test；Architecture Guards；consistency；Evidence Required 1–9 audit。
  - **Deferred Boundary:** no production semantic revisions, Fingerprint, geometry/direction/distance, repeated/reverse scroll, end detection, generic scroll/continuity framework, Runtime refactor, SC-P3-004 or Phase completion.
  - **Return Contract:** `TASK_RESULT`；完成后停止并由 H4-3 重读 repository truth。

## 4. Independent Regression and Boundary Validation

- [x] 4.1 **Independent SC-P3-003 slice acceptance**
  - **Task ID:** 4.1
  - **Role:** runtime-validator
  - **Tier:** standard
  - **Depends On:** 3.1 DONE
  - **Scenario Receipt:** SC-P3-003
  - **Goal:** fresh reload repository truth，独立验证 Task 1.1/2.1/3.1 实际 diff 满足 SC-P3-003，审计唯一 approved variant、其余零增量、frozen ownership/authority、deferred boundary、deterministic replay 及全部既有回归。
  - **Required Semantic:** viewport action/snapshot change 不等于 world/identity/Goal success；same Container 仅由 fresh evidence + existing identity rules 证明；Container local proof 与 Agent higher authority 不混合。
  - **Approved Production Purchase:** exactly one immutable action variant；new fields/enums/interfaces/components/mutable state = 0；Production Behavior Purchase = NONE for validation。
  - **Allowed Scope:** read-only production/test/OpenSpec/diff audit；运行 build/tests/guards/consistency/strict OpenSpec validation；全部 PASS 后仅可勾选本 Task 4.1。
  - **Forbidden Scope:** 修补 production/tests；修改 Scenario/Spec；接受 coder summary 替代 fresh evidence；新增 artifact/state/dependency；执行 Runtime refactor；开始其他 Scenario/Phase。
  - **Required Assertions:** SC-P3-003 Evidence Required 1–9 全部满足；approved action variant exactly one/no fields；other production deltas 0；positive/stale/conflict/replay branches正确；Ownership/Authority Delta NONE；Recovery boundary不变；H4 contracts and Phase 1/2/SC-P3-001/002 regressions pass；deferred capabilities absent；structural pressure recorded but no refactor executed。
  - **Verification:** `dotnet build src/UniClaw.Runtime.sln`；`dotnet test src/UniClaw.Runtime.sln`；Architecture Guard tests；`scripts/check-consistency.sh`；`openspec validate phase3-scroll-identity-continuity --strict`；production delta/determinism/evidence audit。
  - **Deferred Boundary:** future Scroll semantics, Runtime refactor, other Scenario candidates, Phase completion and automatic Scenario selection remain absent。
  - **Return Contract:** `VALIDATION_RESULT`；Verdict only `PASS | CONDITIONAL_PASS | FAIL`。PASS 后 H4-3 可执行 Scenario-specific capability closeout并停止。

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Model/` | `docs/system/constitution/runtime-architecture-contract.md` |
| `src/UniClaw.Runtime/Traversal/` | `docs/system/layers/traversal-runtime.md` |
| `src/UniClaw.Runtime/Container/` | `docs/system/layers/container-runtime.md` |
| `src/UniClaw.Runtime/Agent/` | `docs/system/layers/agent-runtime.md` |
| `tests/UniClaw.Runtime.Tests/Scenario/` | `docs/system/scenarios/03-scroll-identity.md` |
| `openspec/changes/phase3-scroll-identity-continuity/` | `openspec/changes/phase3-scroll-identity-continuity/design.md` |
