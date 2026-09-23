# SIM-001 — 仿真 Host 插件化补全（SeamOverrides）

lifecycle_state: persisted · disposition: none · depth: standard · base: 87b2247

## Intent

SimulationHost 工厂面的 5 个缝 + Agent 脚本全部 hard-code——
组件构造器虽可注入但公开工厂无旋钮。补全为 SeamOverrides record，
使仿真侧可按需替换任何外部缝（与 Product Host 同缝可互换的仿真面）。

## Scope

- `SeamOverrides` record（SimContract.cs）：6 个可选参数
  · `IAssociationStrategy?`（默认 SeedingAssociationStrategy）
  · `IUiObservationStrategy?`（默认 ReplayFrameObservationStrategy）
  · `IFreshnessEvaluator?`（默认 SatisfyingFreshness）
  · `IEffectDriver?`（默认 DeterministicEffectDriver）
  · `IContinuityStrategy?`（默认 RoleContinuityStrategy）
  · `ScriptedUniAgent?`（默认 bundle 内脚本构造的实例）
- `RunOptions` 加 `SeamOverrides? Seams = null`（单一引用，不混入行为开关）
- `SimulationHost` 工厂方法读取 Seams，null 走既有默认

## Out of Scope

- DI 容器 / 注册机制（无买家）
- 动态场景生成 / ScenarioBuilder（等 F9）
- 环境演化模拟 / 对抗者（等 A47）

## Decisions

- D1 全开 6 缝：增量 ≈ 5 行/个，一步到位（grill Q1 甲）
- D2 SeamOverrides 独立 record：测试行为开关（phased/duplicate）与
  组件注入旋钮分离（grill Q2 乙）
- D3 Agent 注入在 ScriptedUniAgent 级别（非 AgentScriptStep）：
  覆盖 RUN-004 多轮 double 需求；简单场景仍可传脚本步骤
- D4 确定性纪律：不额外加 guard——既有 digest 可复现测试
  间接执法（注入非确定性 driver → digest 不一致 → 测试红）

## Acceptance

1. 全部既有测试零变更通过（Seams=null = 现行为，向后兼容）
2. 新增测试：注入自定义 Freshness → 断言被使用；注入自定义 Driver → 断言被使用
3. RunOptions/SeamOverrides 编译为 internal（不进公开面）

## Verification

（IMPLEMENT 后四元组回填）

## Status log

- 2026-09-22 · created·persisted · grill 三问落定（全开/独立 record/独立 change）
