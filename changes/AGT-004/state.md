# AGT-004 — Real Android Settings traversal verification closure
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 7b49038d

## Intent（WHAT/WHY）

把 Real Android Settings traversal 从“真实设备完成了两步动作”推进到可判定的
逐步验证：导航动作允许页面路由变化，同时仍要求动作前目标唯一、动作后产生新的
路由证据；Agent 生成的目标状态只能使用受支持的 typed state，任意自由文本必须
在 effect 之前 fail closed。所有结果要能从决策、路由观察、投影和验证检查回溯。

## Scope

- 为 live Settings hierarchy 投影稳定的 `ui.screen.route` fingerprint，并保持
  `ui.screen` 作为稳定 Settings container identity。
- 在 post-action assurance 中区分合法导航导致的目标消失与 grounding 失败。
- 对 Agent act/policy 的 `desiredState` 做受支持值校验；导航目标省略该字段。
- 更新 DSH decision prompt、回归测试、场景认证和人类可读证据说明。
- 在可用的专用 Android 环境中复跑真实 Settings traversal；不得操作共享 3080。

## Out of Scope

- 改变 Product effect authority、Kernel lifecycle 或 Settings traversal 的完成语义。
- 用预置坐标、模型自由文本或单一最终截图替代 route/effect verification。
- 启动或回收共享 DSH 3080 服务。

## Acceptance

1. 动作前目标唯一、动作后目标消失且 `ui.screen.route` 改变时，post-action
   verification 通过并记录 navigation transition；路由未改变时仍 fail closed。
2. act/policy 的不受支持 `desiredState` 在 effect 前被拒绝；typed state 和省略
   desiredState 的导航目标保持现有行为。
3. Settings trace 包含 screen route fingerprint、decision correlation、effect
   receipt 和 verification checks，能够追溯到同一 Product Session/Run。
4. Kernel/Host/DSH/plugin 回归、solution build/test、29 个场景认证均通过。
5. 有注册 Android 环境时，真实遍历覆盖 Settings 根页、至少一个一级入口和一个
   二级页，并逐步给出 route/effect/verification 证据；环境不可用时明确记录阻塞，
   不把短路径证据升级为完整遍历 PASS。

## Verification

```yaml
level: ENVIRONMENT
method: focused route/assurance/desiredState tests; Host trace tests; DSH plugin tests; solution build/test; scenario certification; dedicated Android rerun when emulator is available
expected: navigation target disappearance is verified only with a changed route fingerprint; unsupported desiredState fails closed; all deterministic checks pass; live evidence is either complete or explicitly environment-blocked
actual: "deterministic verification PASS: solution build 0 errors; solution tests 1108/1108 (Kernel 700, Simulation 184, Host 53, Agent.Dsh 131, Core 14, Agent 17, FileSystem 9); DSH plugin 21/21; scenario certification/check 29/29; git diff --check PASS. Final live rerun under an escalated dedicated session used API 35 emulator-5556, dedicated DSH 3081, zai-coding-cn/glm-5.3-flash, configured title 遍历设置菜单 and workspace reuse. It completed with Completion, two DeliveryCompleted receipts, completedSteps 2/2, Fast and hierarchy available in 3/3 cycles, and route fingerprints Settings→Internet→T-Mobile."
evidence: evidence/real-settings-traversal-20260930/; tests/UniClaw.Kernel.Tests/World/Per009RemediationTests.cs; tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverTests.cs; tests/UniClaw.Kernel.Tests/Runtime/KernelRunDriverPolicyValidationTests.cs; tests/UniClaw.Host.Tests/SettingsTraversalOutcomeTests.cs; dsh/uniclaw-decision-channel/src/index.js
```

## Status log

- 2026-09-30 · persisted→implemented · Located first divergence: post-action assurance treated a legitimate route change and target disappearance as grounding failure; arbitrary model `desiredState` was silently unmapped.
- 2026-09-30 · implemented · Added route fingerprint projection, navigation-aware assurance, fail-closed desiredState validation, DSH prompt constraint, and focused regression coverage.
- 2026-09-30 · implemented→verifying · Deterministic verification is in progress; Android rerun remains contingent on a registered emulator/device.
- 2026-10-01 · verifying→verified · Confirmed the original emulator error was caused by sandbox CPU-feature/lifecycle restrictions; with an escalated keepalive session, corrected the web profile provider to zai-coding-cn, aligned project/session title and workspace reuse, and completed the real two-step Settings→Wi-Fi path with 2/2 verified steps.

## Gate disposition

本 change 的导航验证和真实短路径验收已通过。证据证明的是 Settings 根页到 Wi-Fi
状态的两步闭环；没有执行 Settings 全部菜单的全树覆盖，因此“全树遍历”若仍是
额外目标，应另立覆盖范围明确的 change，不把本次短路径结果扩大解释。
- 2026-10-02 · verified→closed · 验收四元组齐备（确定性+ENVIRONMENT 两级）；其声明的全树覆盖缺口已由 AGT-005（closed）按覆盖边界显式承接并完成，负路径由 AGT-006/007 承接，无未授权改动、文档已同步，关门。
