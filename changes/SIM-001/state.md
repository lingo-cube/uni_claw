# SIM-001 — 仿真 Host 插件化补全（SeamOverrides）

lifecycle_state: closed · disposition: none · depth: standard · base: 87b2247

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
  · `IContinuityStrategy?`（null = 不注入透传：WorldModel 既有 null 语义——
    continuity demand 时 fail-closed；组合根无 RoleContinuityStrategy 兜底）
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

```yaml
level: DETERMINISTIC
method: >
  focused（SeamOverride* + SeamDefaultComposition*，行为级：Freshness
  WasCalled / Driver Calls 计数 + 产品 facts 对账 / Agent seam）+
  Simulation.Tests 全套 + 七套件全量 + git diff --check +
  scenario_certify.py --check（HEAD=f37fb994 时点，2026-09-27 复跑）
expected: >
  六缝全部真实进入 Compose；注入件被实际调用（非仅 Assert.Same）；
  null 默认路径行为不变；internal 保持；全量零失败
actual: >
  focused 7/7 · Simulation 184/184 · 全量 1029/1029 · diff-check CLEAN ·
  certification PASS（29 files, 0 violations）；生产代码零改动
  （改动仅 2 个测试文件 + 本 state）；旧 105/27 基线已废弃（当时为
  本机环境 flake 时代记录，见 PER-009 status log）
evidence: >
  SeamOverrideTests.cs（.Reason 修正 + 行为断言）+
  SeamDefaultCompositionTests.cs（六默认组合证明 + 默认 freshness
  行为级）+ SimulationHost.cs:198-234 六缝接线 + 本文件 Review Gate 记录
```

## Review Gate（2026-09-27，5.3 正式 review）

```text
R1 六缝真实接入 Compose: PASS（SimulationHost.cs:200/201/202/203-204/205-206/218-228；
   Continuity null 透传 = 既有语义，接线正确）
R2 Freshness 行为级证据: PASS（InjectedFreshness_IsUsed WasCalled；默认
   SatisfyingBasis 经 RuntimeAssurance.Judge 进 owner log）
R3 Driver 行为级证据: PASS（InjectedDriver Calls 计数 + EffectReceipts/
   EffectDeliveryCount 与产品 owner facts 同源）
R4 null/默认兼容: PASS（184 个既有场景全绿 = 默认路径行为不变 + 组合级
   证明测试；F3 注释不实已更正，行为零变更）
R5 internal 保持: PASS（SimContract.cs:472/:505；IVT 链在案）
R6 新增 authority/parallel runtime: NONE（test-assembly 旋钮，产品面零扩张）
R7 全量干净: PASS（1029/1029 · certification PASS · diff-check CLEAN）
```

## Status log

- 2026-09-27 · verified→closed · Owner final closure PASS（六缝接线 /
  Freshness+Driver 行为级 / null 兼容 / internal / 确定性无偏离全 PASS；
  闭门前两处文档措辞更正：Continuity 默认 = null 透传语义、
  SeamDefaultCompositionTests 注释 1→2 consultations）；closure-only，
  Product code 零改动。验证证据与 Review Gate 记录保留（上节未动）。
- 2026-09-27 · implemented→reviewed→verified · Review/Verify 执行
  （Flash 补行为级/组合级测试 7 例 + Leader R1–R7 review 全 PASS）；
  F1 WIP 编译错（.Basis→.Reason）、F2 轮次断言（1→2）、F3 Continuity
  注释不实（更正为实际 null 语义）三 finding 处置在案；Verification
  四元组刷新为当前事实（1029/1029）。OWNER_GATE 到达，待 Owner 终裁。
- 2026-09-22 · created·persisted · grill 三问落定（全开/独立 record/独立 change）
- 2026-09-22 · implemented · SeamOverrides 6 缝落地 + SimulationHost 工厂
  读取 + EffectDriver 类型改接口 + EffectDeliveryCount 便捷面 +
  8 文件 DeliveryCount 批量迁移（AsyncPerceptionHost 回退）；
  4/4 新测试绿，105/27 精确同前（零回归）
