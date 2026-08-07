# Tasks — phase1-deterministic-runtime

> 实施前必读: proposal.md + design.md + specs/**。完成一项立即勾选 `- [x]`。
> 实施遵守: 宪章 §54（Responsibility → Authority → State Owner → Interfaces → Implementation → Verification）、§48（核心类九个问题）、§49（接口证明价值）。

## Phase A — A. Architecture Proposal + B. Minimum Contracts（本 change 已完成）

- [x] A1. Architecture Proposal（design.md: component model / ownership / dependency / state model / normal lifecycle / trap lifecycle / structure / deferred decisions）
- [x] A2. Minimum Contracts（specs/run-lifecycle、specs/environment、specs/container-traversal、specs/normal-wifi-scenario）
- [x] A3. 实施清单（本 tasks.md）；AGENTS.md 导航更新（根 + src/UniClaw.Runtime 目录表）

## Phase B — C. Fake Environment + D. Normal WiFi Scenario 实施

- [ ] B1. **模型层**（Model/，全部不可变 sealed record + ImmutableArray 约定）:
      Observation / ScreenElement / Fingerprint / WorldBelief / RecoveryAnchor /
      Goal / Plan / DeviceAction（LaunchApp | Tap）/ ActionResult / Trap / TraceEvent / RunState
- [ ] B2. **端口**: `Environment/IEnvironment.cs`（ObserveAsync / ExecuteAsync，均带 CancellationToken）
- [ ] B3. **Fake**: `tests/UniClaw.Runtime.Tests/Scenario/Fakes/ScriptedEnvironment.cs`
      （Screen 配置驱动: Screen A + Click X → Screen B；确定性、可重放）
- [ ] B4. **Startup**（Startup/）: Attach → Launch → Observe → Resolve Initial Semantic World
      → Establish Initial Container → Establish RecoveryAnchor → Ready
      （配合 World/ 的 Reconcile 初建 WorldBelief，含 Confidence/Evidence/Timestamp）
- [ ] B5. **Container**（Container/）: Semantic Identity 显式规则注入 / 局部状态 /
      still-mine 判断 / 局部完成判断
- [ ] B6. **Traversal**（Traversal/）: Select → Check → Execute → Observe → Verify → Branch +
      step journal + Trap 结果类型（TargetNotFound / ActionFailed / UnexpectedPage）
- [ ] B7. **Agent**（Agent/）: RunState 生命周期 + WorldBelief 代持 + Active Container Stack +
      Plan 驱动（bind/traverse/navigate 循环）+ Goal Evidence 完成判定（I-10）
- [ ] B8. **Observability**（Observability/）: Trace（RunId/ContainerId/StepId/ActionId 因果链，只写不改业务）
- [ ] B9. **场景测试**（tests/UniClaw.Runtime.Tests/Scenario/NormalWifiScenarioTests.cs）:
      §34 生命周期顺序断言 + Goal Evidence 断言 + Trace 因果链断言 + 确定性重复运行断言
- [ ] B10. **验证**: `dotnet build` 0 警告 0 错误；`dotnet test` 全绿；
      `scripts/check-consistency.sh` ALL PASS；ArchitectureGuardTests 保持通过

## Phase C — F. Architecture Review（实施完成后）

- [ ] C1. §60-F Review checklist: 无 God Object / 无重复 authority / Runtime State 与 World State 未混淆 /
      Plan 未当 Reality / 无不必要 FSM / 每个状态 owner 可解释 / 无需真实手机与 LLM 完成核心测试
- [ ] C2. 评审通过后归档本 change（/opsx:archive），提取 decisions
