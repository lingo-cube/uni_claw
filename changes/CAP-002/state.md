# CAP-002 — Capability executable contract and Hub resolution
lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）
让 Capability Hub 管理的不只是静态描述，也能验证并解析实际的 Capability 实例。共享运行时能力通过 `ICapability` 自描述；Semantic Perception 与 UI Element Perception 通过派生接口表达。DSH Tool/Skill 暂不纳入本 Change。

## Scope
- 增加 `ICapability` 根接口和 `ICapabilityHub` 管理面。
- 让感知协议接口继承 `ICapability`。
- 支持实例注册、实例解析和协议声明与实现接口的一致性校验。
- 保留信任域、生命周期事实和 Descriptor-only 声明路径的既有语义。
- 补充确定性契约测试和 Capability Hub 文档。

## Out of Scope
- DSH Tool 注册、Tool adapter、Skill Registry 和 Agent Profile。
- Semantic/UI Element 的具体 payload 执行方法和模型链路。
- Evidence、WorldModel、Control、Assurance 或 Effect authority 变更。

## Decisions
1. `ICapability` 只暴露不可变 `CapabilityDescription`；健康和生命周期仍由 Hub/Registry 管理。
2. `ISemanticPerception` 与 `IUiElementPerception` 是 Product protocol marker，具体执行 payload 后续按协议纵向实现。
3. Product perception 的声明协议必须与实例实现的派生接口一一对应；不匹配时 fail closed，且不发布 lifecycle fact。
4. DSH Tool 是 Host/DSH Adapter 的投影，不进入 Kernel Capability 接口。

## Acceptance
1. `CapabilityRegistry` 实现 `ICapabilityHub`，可注册并解析实例。
2. Semantic/UI Element 协议接口继承 `ICapability`。
3. 感知 Descriptor 与实例接口不一致时注册失败且 Registry 无新增事实。
4. 现有 Descriptor、信任域、生命周期和全量回归保持通过。
5. DSH Tool/Skill 文件和现有 DSH 工具面不发生变化。

## Constraints
- Capability Hub 不拥有 Product 结果或业务 authority。
- 不把 DSH transport、scope、ToolDefinition 或 Skill 语义写入 Kernel。
- 任何后续执行协议必须经独立的 payload/Buyer 评审后再加入派生接口。

## Verification
level: DETERMINISTIC
method: `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter 'FullyQualifiedName~CapabilityHubTests'`; full Kernel/Host and existing DSH suites
expected: 实例注册、解析和 fail-closed 校验通过；既有测试无回归
actual: 5 CapabilityHub tests passed; Kernel 794 passed; Host 142 passed; DSH Workbench 64 passed; `git diff --check` passed
evidence: current verification turn command output

## Status log
- 2026-10-04 · RESOLVE → PERSIST → PLAN · 用户决定先完成 Capability Hub，暂缓 DSH Skill/Tool 注册。
- 2026-10-04 · PLAN → IMPLEMENT · 增加 `ICapability`、`ICapabilityHub`、实例注册/解析和协议实现一致性校验。
- 2026-10-04 · IMPLEMENT → VERIFY · 定向测试首次暴露 Kernel public surface whitelist 未同步；补充四个公开接口名单后复跑。
- 2026-10-04 · VERIFY → CLOSED · Kernel 794、Host 142、DSH Workbench 64 全部通过，差异检查通过；DSH Skill/Tool 保持未触碰。
